﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Types;

namespace Colibri.Grasshopper
{
    
    public class SetSelection : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the IteratorSelection class.
        /// </summary>
        public SetSelection()
          : base("Fly Selection", "Sel",
              "Generates iteration selections for Iterator.",
              "TT Toolbox", "Colibri")
        {
        }

        public override GH_Exposure Exposure { get { return GH_Exposure.secondary; } }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntervalParameter("Domains", "Domains", "Ranges of all iterations, can be one or a list of 1d domains (use Construct Domain).", GH_ParamAccess.list);
            pManager[0].Optional = true;

            pManager.AddIntegerParameter("Take", "Take", "Numbers to TAKE on each Slider, ValueList or Panel.  This should be a list of integers (each of which must be greater than one) of the same length as the list of sliders plugged into the Sliders input.\n\nIf no input data is provided, we'll use every tick on every slider as a step.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Selections", "Sel", "Selections for Iterator", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var positions = new List<int>();
            var ranges = new List<GH_Interval>();

            //get Data
            DA.GetDataList(1, positions);
            DA.GetDataList(0, ranges);

            foreach (var item in ranges)
            {
                if (item.Value.Min<0 ||item.Value.Max ==0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Domain should within or equal (min:0 TO max:total)");
                    return;
                }

            }
            var selections = new IteratorSelection(positions, ranges);

            //set Data
            DA.SetData(0, selections);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7dab01df-60be-477a-83d0-ff37a89d6a5a}"); }
        }
    }
}