using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using GH = Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Windows.Forms;
using System.Linq;
using Grasshopper.Kernel.Special;
using System.Diagnostics;

namespace Colibri.Grasshopper
{
    public class Iterator_LG : GH_Component
    {
        GH_Document doc = null;
        List<GH_NumberSlider> allConnectedSliders = new List<GH_NumberSlider>();
        List<string> sliderNames = new List<string>();
        int[] sliderSteps;
        int[] sliderStepsPositions;
        List<string> computedValues = new List<string>();
        int totalLoops;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Iterator_LG()
          : base("Iterator_LG", "Iterator_LG",
              "Generates design iterations from a collection of sliders.",
              "TT Toolbox", "Colibri")
        {
        }

        public override GH_Exposure Exposure { get { return GH_Exposure.primary; } }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Sliders", "Sliders",
                "Sliders to iterate over.  Sliders must be plugged directly into this input.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Steps", "Steps", "Number of steps to take on each slider.  This should be a list of integers (each of which must be greater than one) of the same length as the list of sliders plugged into the Sliders input.\n\nIf no input data is provided, we'll use every tick on every slider as a step.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Fly?", "Fly?", "Tell Colibri to fly!  Provide a button here, and click it once you are ready for Colibri to fly around your definition.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Inputs", "Inputs",
                "Colibri's Inputs object.  Plug this into the Colibri aggregator downstream.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //get a reference to the current doc the first time through
            if (doc == null)
            {
                doc = GH.Instances.ActiveCanvas.Document;
            }


            //catch grasshopper inputs
            List<double> sliderValues = new List<double>();
            List<int> tempSteps = new List<int>();
            bool _fly = false;
            DA.GetDataList(0, sliderValues);
            DA.GetDataList(1, tempSteps);
            DA.GetData(2, ref _fly);


            //manage sliders

            //get a handle on the connected sliders
            List<GH_NumberSlider> connectedSliders = getConnectedSliders();

            //wipe out any event handlers that might already exist
            foreach (GH_NumberSlider slider in allConnectedSliders)
            {
                slider.ObjectChanged -= Slider_ObjectChanged;
            }

            //refresh our global list of sliders
            allConnectedSliders.Clear();
            allConnectedSliders.AddRange(connectedSliders);

            //slider names
            sliderNames = new List<string>();
            foreach (GH_NumberSlider slider in connectedSliders)
            {
                if (slider.NickName != "")
                {
                    sliderNames.Add(slider.NickName);
                }
                else
                {
                    sliderNames.Add(slider.ImpliedNickName);
                }
            }

            //if no steps input was provided, use the slider's ticks as steps
            if (tempSteps.Count == 0)
            {
                foreach (GH_NumberSlider slider in connectedSliders)
                {
                    tempSteps.Add(slider.TickCount + 1);
                }
            }


            //run all defense before Colibri is told to fly
            if (!_fly)
            {
                //make sure globals are clear
                sliderSteps = null;
                sliderStepsPositions = null;
                totalLoops = 1;

                //check that the number of steps equals the number of sliders
                if (tempSteps.Count != sliderValues.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of connected sliders must be equal to the number of items in the Steps input list.");
                    return;
                }
                //check that the number of steps is greater than 1 and less than the number of ticks in the matching slider
                for (int i = 0; i < connectedSliders.Count; i++)
                {
                    if (tempSteps[i] < 2 || tempSteps[i] > connectedSliders[i].TickCount + 1)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps values must be greater than 1 and less than the number of steps defined in the associated slider.  The first offending slider / step combo is at index: " + i.ToString());
                        return;
                    }
                }
            }




            //get slider steps and names once - the first time we are told to fly
            if (sliderSteps == null && _fly)
            {
                //get the number of steps per slider
                sliderSteps = tempSteps.ToArray(); //tempSteps.Select(x => x - 1).ToArray();

                //populate our dictionary of sliders / current step positions
                //sliderStepsPositions = Enumerable.Repeat(1, sliderSteps.Length).ToArray();
            }

            //listen for slider changes
            foreach (GH_NumberSlider slider in allConnectedSliders)
            {
                slider.ObjectChanged += Slider_ObjectChanged;
            }


            //output 'inputs' object
            Dictionary<string, double> inputs = new Dictionary<string, double>();
            for (int i = 0; i < sliderValues.Count; i++)
            {
                try
                {
                    inputs.Add(sliderNames[i], sliderValues[i]);
                }
                catch (ArgumentException ex)
                {
                    if (ex.ToString().Contains("key"))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Your sliders must have unique nicknames!  Set them all and try again.");
                        return;
                    }
                    else
                    {
                        throw ex;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            DA.SetDataList(0, inputs);



            //don't touch this stuff!  this is what makes the magic happen down below.
            if (!_fly)
                return;

            if (_running)
                return;

            _run = true;

            doc.SolutionEnd += OnSolutionEnd;
        }




        //methods below copied in from Ladybug's Fly component
        private bool _run = false;
        private bool _running = false;


        private void OnSolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            // Unregister the event, we don't want to get called again.
            e.Document.SolutionEnd -= OnSolutionEnd;

            // If we're not supposed to run, abort now.
            if (!_run)
                return;

            // If we're already running, abort now.
            if (_running)
                return;

            // Reset run and running states.
            _run = false;
            _running = true;

            try
            {
                // Find the Guid for connected slides
                List<System.Guid> guids = new List<System.Guid>(); //empty list for guids
                GH.Kernel.IGH_Param selSlidersInput = this.Params.Input[0]; //ref for input where sliders are connected to this component
                IList<GH.Kernel.IGH_Param> sources = selSlidersInput.Sources; //list of things connected on this input
                bool isAnythingConnected = sources.Any(); //is there actually anything connected?


                // Find connected
                GH.Kernel.IGH_Param trigger = this.Params.Input[2].Sources[0]; //ref for input where a boolean or a button is connected
                GH.Kernel.Special.GH_BooleanToggle boolTrigger = trigger as GH.Kernel.Special.GH_BooleanToggle;

                if (isAnythingConnected)
                { //if something's connected,
                    foreach (var source in sources) //for each of these connected things:
                    {
                        IGH_DocumentObject component = source.Attributes.GetTopLevel.DocObject; //for this connected thing, bring it into the code in a way where we can access its properties
                        GH.Kernel.Special.GH_NumberSlider mySlider = component as GH.Kernel.Special.GH_NumberSlider; //...then cast (?) it as a slider
                        if (mySlider == null) //of course, if the thing isn't a slider, the cast doesn't work, so we get null. let's filter out the nulls
                            continue;
                        guids.Add(mySlider.InstanceGuid); //things left over are sliders and are connected to our input. save this guid.
                                                          //we now have a list of guids of sliders connected to our input, saved in list var 'mySlider'
                    }
                }

                // Find all sliders.
                List<GH.Kernel.Special.GH_NumberSlider> sliders = new List<GH.Kernel.Special.GH_NumberSlider>();
                foreach (IGH_DocumentObject docObject in doc.Objects)
                {
                    GH.Kernel.Special.GH_NumberSlider slider = docObject as GH.Kernel.Special.GH_NumberSlider;
                    if (slider != null)
                    {
                        // check if the slider is in the selected list
                        if (isAnythingConnected)
                        {
                            if (guids.Contains(slider.InstanceGuid)) sliders.Add(slider);
                        }
                        else sliders.Add(slider);
                    }
                }
                if (sliders.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show("No sliders could be found", "<harsh buzzing sound>", MessageBoxButtons.OK);
                    return;
                }

                //we now have all sliders
                //ask the user to give a sanity check


                string popupMessage = "";


                int dummyCounter = 0;
                foreach (GH_NumberSlider slider in sliders)
                {
                    totalLoops *= (sliderSteps[dummyCounter]);
                    popupMessage += slider.ImpliedNickName;
                    popupMessage += "\n";
                    dummyCounter++;
                }
                if (MessageBox.Show(sliders.Count + " slider(s) connected:\n" + popupMessage +
                  "\n" + totalLoops.ToString() + " iterations will be done. Continue?" + "\n\n (Press ESC to pause during progressing!)", "Start?", MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    SetBooleanToFalse(boolTrigger);
                    this.Message = "Release the Colibri!";

                    //wipe out colibri variables and compute a new solution
                    sliderNames = null;
                    sliderSteps = null;
                    sliderStepsPositions = new int[sliderNames.Count];
                    //computedValues = new List<string>();
                    e.Document.NewSolution(false);
                    Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

                    return;
                }

                // Set all sliders back to first tick
                foreach (GH_NumberSlider slider in sliders)
                    slider.TickValue = 0;

                //start a stopwatch
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                // Start a giant loop in which we'll permutate our way across all slider layouts.
                RunPermutations(sliderSteps, boolTrigger, sliders, e);
            }
            catch(Exception ex)
            {
                // "something went wrong!";
            }
            finally
            {
                // Always make sure that _running is switched off.
                _running = false;
            }
        }

        object RunPermutations(int[] lengths, GH_BooleanToggle boolTrigger, List<GH_NumberSlider> sliders, GH_SolutionEventArgs e, params object[] objs)
        {
            // create progress bar by dots and |
            string pb = ".................................................."; //50 of "." - There should be a better way to create this in C# > 50 * "." does it in Python!

            char[] pbChars = pb.ToCharArray();

            Stopwatch sw = Stopwatch.StartNew();
            int counter = 0;

            foreach (int[] idxs in new IterateNDArray(lengths))
            {
                // let the user cancel the process
                if (GH_Document.IsEscapeKeyDown())
                {
                    if (System.Windows.Forms.MessageBox.Show("Do you want to stop the process?\nSo far " + counter.ToString() +
                      " out of " + totalLoops.ToString() + " iterations are done!", "Stop?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        // cancel the process by user input!
                        SetBooleanToFalse(boolTrigger);
                        this.Message += "\nCanceled by user! :|";
                        return null;
                    }
                }

                for (int i = 0; i < sliders.Count; i++)
                {
                    GH_NumberSlider slider = sliders[i];

                    //look up the current slider's current sliderStepsPosition and target number
                    int totalNumberOfSteps = sliderSteps[i];
                    int steps = slider.TickCount / sliderSteps[i];
                    slider.TickValue = steps * idxs[i];
                    //int numTicksToAddAsInt = slider.TickCount / totalNumberOfSteps;
                    //double numTicksToAddAsDouble = (double)slider.TickCount / (double)totalNumberOfSteps;

                    ////find the closest tick
                    //int closestTick = 0;
                    //if (currentSliderStepsPosition + numTicksToAddAsInt >= sliderMidStep)
                    //{
                    //    closestTick = (int)Math.Ceiling(numTicksToAddAsDouble * currentSliderStepsPosition);
                    //}
                    //else
                    //{
                    //    closestTick = (int)Math.Floor(numTicksToAddAsDouble * currentSliderStepsPosition);
                    //}

                    // Increment the slider.



                }

                // We've just got a new valid permutation. Solve the new solution.
                counter++;
                e.Document.NewSolution(false);
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                UpdateProgressBar(counter, totalLoops, sw, pbChars);
            }

            // study is over!
            SetBooleanToFalse(boolTrigger);
            sw.Stop(); //stop start watch
            UpdateProgressBar(counter, totalLoops, sw, pbChars);
            this.Message += "\nFinished at " + DateTime.Now.ToShortTimeString();

            //wipe out colibri variables
            sliderNames = new List<string>();
            sliderSteps = new int[sliderNames.Count];
            sliderStepsPositions = new int[sliderNames.Count];
            computedValues = new List<string>();
            e.Document.NewSolution(false);
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            return null;
        }

        private bool MoveToNextPermutation_LG(ref int index, List<GH.Kernel.Special.GH_NumberSlider> sliders)
        {
            if (index >= sliders.Count)
                return false;

            GH.Kernel.Special.GH_NumberSlider slider = sliders[index];
            if (slider.TickValue < slider.TickCount)
            {
                //Figure out which step to fly to...

                //look up the current slider's current sliderStepsPosition and target number
                int totalNumberOfSteps = sliderSteps[index];
                int currentSliderStepsPosition = sliderStepsPositions[index];
                int sliderMidStep = slider.TickCount / 2;
                int numTicksToAddAsInt = slider.TickCount / totalNumberOfSteps;
                double numTicksToAddAsDouble = (double)slider.TickCount / (double)totalNumberOfSteps;

                //find the closest tick
                int closestTick = 0;
                if (currentSliderStepsPosition + numTicksToAddAsInt >= sliderMidStep)
                {
                    closestTick = (int)Math.Ceiling(numTicksToAddAsDouble * currentSliderStepsPosition);
                }
                else
                {
                    closestTick = (int)Math.Floor(numTicksToAddAsDouble * currentSliderStepsPosition);
                }

                // Increment the slider.
                slider.TickValue = closestTick;

                //Increment the current step position
                sliderStepsPositions[index]++;

                //have we already computed this upcoming combination?  If so, move on to the next one without expiring the solution
                if (computedValues.Contains(GetSliderVals(sliders)))
                {
                    return MoveToNextPermutation(ref index, sliders);
                }


                return true;
            }
            else
            {
                // The current slider is already at the maximum value. Reset it back to zero.
                slider.TickValue = 0;

                //set our slider steps position back to 0
                sliderStepsPositions[index] = 0;

                // Move on to the next slider.
                index++;

                // If we've run out of sliders to modify, we're done permutatin'
                if (index >= sliders.Count)
                    return false;

                return MoveToNextPermutation(ref index, sliders);
            }
        }

        private bool MoveToNextPermutation(ref int index, List<GH.Kernel.Special.GH_NumberSlider> sliders)
        {
            if (index >= sliders.Count)
                return false;

            GH.Kernel.Special.GH_NumberSlider slider = sliders[index];
            if (slider.TickValue < slider.TickCount)
            {
                //Figure out which step to fly to...

                //look up the current slider's current sliderStepsPosition and target number
                int totalNumberOfSteps = sliderSteps[index];
                int currentSliderStepsPosition = sliderStepsPositions[index];
                int sliderMidStep = slider.TickCount / 2;
                int numTicksToAddAsInt = slider.TickCount / totalNumberOfSteps;
                double numTicksToAddAsDouble = (double)slider.TickCount / (double)totalNumberOfSteps;

                //find the closest tick
                int closestTick = 0;
                if (currentSliderStepsPosition + numTicksToAddAsInt >= sliderMidStep)
                {
                    closestTick = (int)Math.Ceiling(numTicksToAddAsDouble * currentSliderStepsPosition);
                }
                else
                {
                    closestTick = (int)Math.Floor(numTicksToAddAsDouble * currentSliderStepsPosition);
                }

                // Increment the slider.
                slider.TickValue = closestTick;

                //Increment the current step position
                sliderStepsPositions[index]++;

                //have we already computed this upcoming combination?  If so, move on to the next one without expiring the solution
                if (computedValues.Contains(GetSliderVals(sliders)))
                {
                    return MoveToNextPermutation(ref index, sliders);
                }


                return true;
            }
            else
            {
                // The current slider is already at the maximum value. Reset it back to zero.
                slider.TickValue = 0;

                //set our slider steps position back to 0
                sliderStepsPositions[index] = 0;

                // Move on to the next slider.
                index++;

                // If we've run out of sliders to modify, we're done permutatin'
                if (index >= sliders.Count)
                    return false;

                return MoveToNextPermutation(ref index, sliders);
            }
        }

        private string GetSliderVals(List<GH_NumberSlider> sliders)
        {
            string sliderVals = "";
            foreach (GH_NumberSlider slider in sliders)
            {
                sliderVals += slider.CurrentValue.ToString() + ",";
            }
            return sliderVals;
        }

        private void SetBooleanToFalse(GH.Kernel.Special.GH_BooleanToggle boolTrigger)
        {
            if (boolTrigger == null) return;

            boolTrigger.Value = false; //set trigger value to false
            boolTrigger.ExpireSolution(true);
        }

        private void UpdateProgressBar(int counter, int totalLoops, System.Diagnostics.Stopwatch sw, char[] pbChars)
        {
            // calculate percentage and update progress bar!
            double pecentageComplete = Math.Round((double)100 * (counter + 1) / totalLoops, 2);

            int lnCount = (int)pecentageComplete / (100 / pbChars.Length); //count how many lines to be changed!

            for (int i = 0; i < lnCount; i++) pbChars[i] = '|';

            string pbString = new string(pbChars);

            // format and display the TimeSpan value
            System.TimeSpan ts = sw.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
              ts.Hours, ts.Minutes, ts.Seconds,
              ts.Milliseconds / 10);

            // calculate average run time
            double avergeTime = Math.Round(ts.TotalSeconds / (counter + 1), 2); // average time for each iteration

            double expectedTime = Math.Round((ts.TotalMinutes / (counter + 1)) * totalLoops, 2); // estimation for total runs

            this.Message = elapsedTime + "\n" + pbString + "\n" + pecentageComplete.ToString() + "%\n"
              + "Average Time: " + avergeTime + " Sec.\n"
              + "Est. Total Time: " + expectedTime + " Min.\n";
        }

        private List<GH_NumberSlider> getConnectedSliders()
        {

            // Find the Guid for connected slides
            List<System.Guid> guids = new List<System.Guid>(); //empty list for guids
            GH.Kernel.IGH_Param selSlidersInput = this.Params.Input[0]; //ref for input where sliders are connected to this component
            IList<GH.Kernel.IGH_Param> sources = selSlidersInput.Sources; //list of things connected on this input
            bool isAnythingConnected = sources.Any(); //is there actually anything connected?

            // Find connected
            if (isAnythingConnected)
            { //if something's connected,
                foreach (var source in sources) //for each of these connected things:
                {
                    IGH_DocumentObject component = source.Attributes.GetTopLevel.DocObject; //for this connected thing, bring it into the code in a way where we can access its properties
                    GH.Kernel.Special.GH_NumberSlider mySlider = component as GH.Kernel.Special.GH_NumberSlider; //...then cast (?) it as a slider
                    if (mySlider == null) //of course, if the thing isn't a slider, the cast doesn't work, so we get null. let's filter out the nulls
                        continue;
                    guids.Add(mySlider.InstanceGuid); //things left over are sliders and are connected to our input. save this guid.
                                                      //we now have a list of guids of sliders connected to our input, saved in list var 'mySlider'
                }
            }

            // Find all sliders.
            List<GH.Kernel.Special.GH_NumberSlider> sliders = new List<GH.Kernel.Special.GH_NumberSlider>();
            foreach (IGH_DocumentObject docObject in doc.Objects)
            {
                GH.Kernel.Special.GH_NumberSlider slider = docObject as GH.Kernel.Special.GH_NumberSlider;
                if (slider != null)
                {
                    // check if the slider is in the selected list
                    if (isAnythingConnected)
                    {
                        if (guids.Contains(slider.InstanceGuid)) sliders.Add(slider);
                    }
                    else sliders.Add(slider);
                }
            }


            /*foreach (GH.Kernel.Special.GH_NumberSlider slider in sliders)
            {
                names.Add(slider.NickName);
            }*/

            return sliders;
        }


        private void Slider_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            ExpireSolution(true);
        }


        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.Colibri_logobase_1;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{6B3F7758-BFFC-49B4-89D5-D49A9DC2F574}"); }
        }
    }
}
