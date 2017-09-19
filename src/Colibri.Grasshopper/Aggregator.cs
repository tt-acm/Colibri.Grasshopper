using System;
using System.Drawing;
using System.Dynamic;
using System.Collections.Generic;
using Grasshopper.Kernel.Special;

using Rhino.Geometry;
using Grasshopper.Kernel.Types;

using System.IO;
using GH = Grasshopper;
using Grasshopper.Kernel;
using System.Windows.Forms;
using System.Linq;
using GH_IO.Serialization;
using Grasshopper.Kernel.Parameters;


namespace Colibri.Grasshopper
{
    

    public class Aggregator : GH_Component
    {
        //private bool _writeFile = false;
        public string Folder = "";
        public OverrideMode OverrideTypes = OverrideMode.AskEverytime;
        //variable to keep track of what lines have been written during a colibri flight
        private List<string> _alreadyWrittenLines = new List<string>();
        private List<string> _printOutStrings = new List<string>();

        private bool _isFirstTimeOpen = true;
        //private bool _isAlwaysOverrideFolder = false;
        
        private bool _write = false;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Aggregator()
          : base("Colibri Aggregator", "Aggregator",
              "Aggregates design data, images & Spectacles models into a data.csv file (and corresponding data set that data.csv links to) that Design Explorer can open.",
              "TT Toolbox", "Colibri 2.0")
        {
        }

        public override GH_Exposure Exposure { get {return GH_Exposure.tertiary;} }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Folder", "Folder", "Path to a directory to write images, spectacles models, and the data.csv file into.\nPlease make sure you have authorized access.By default it is set to c:\\Colibri", GH_ParamAccess.item,"c:\\Colibri");
            pManager[0].Optional = true;
            pManager.AddTextParameter("Iteration Genome (ID)", "Genome", "Data from the Colibri Iterator compnent, which describes the ID of each iteration.\nCombination of Genome and Colibri Parameters is also acceptable.", GH_ParamAccess.list);
            pManager.AddTextParameter("Iteration Phenome (Results)", "Phenome", "Data from the Colibri Parameters component which collects all output results from each iteration.\nThis is optional if there is no computed data to export.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("ImgSetting", "ImgSetting", "Optional input from the Colibri ImageSetting component.", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddGenericParameter("3DObjects", "3DObjects", "Optional input for 3D Objects from the Spectacles SceneObjects component.\nCurrently this only exports straight lines and meshes.", GH_ParamAccess.list);
            pManager[4].Optional = true;
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[4].DataMapping = GH_DataMapping.Flatten;

            pManager.AddBooleanParameter("Write?", "Write?", "Set to true to write files to disk.", GH_ParamAccess.item,false);
            
            
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Records", "Records","Information that recorded in CSV file.", GH_ParamAccess.list);

        }

        
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            
            
            bool writeFile = false;

            //input variables
            List<string> inputs = new List<string>();
            List<string> outputs = new List<string>();

            List<object> inJSON = new List<object>();
            //object inJSON = null;
            
            var imgParams = new ImgParam();

            //get data
            DA.GetData(0, ref Folder);
            DA.GetDataList(1, inputs);
            DA.GetDataList(2, outputs);
            DA.GetData(3, ref imgParams);
            DA.GetDataList(4,  inJSON);
            DA.GetData(5, ref writeFile);

            //operations is ExpandoObject
            inJSON.RemoveAll(item => item == null);
            var JSON = new threeDParam();
            if (!inJSON.IsNullOrEmpty())
            {
                JSON = new threeDParam(inJSON);
            }


            Dictionary<string,string> inputCSVstrings = ColibriBase.FormatDataToCSVstring(inputs,"in:");
            Dictionary<string, string> outputCSVstrings = ColibriBase.FormatDataToCSVstring(outputs,"out:");
            //Dictionary<string, string> imgParamsClean = ColibriBase.ConvertBactToDictionary(imgParams);
            
            string csvPath = Folder + @"\data.csv";
            //var rawData = inputs;
            //int inDataLength = rawData.Count;
            //rawData.AddRange(outputs);
            //int allDataLength = rawData.Count;

            //Parsing data to csv format
            string flyID = inputCSVstrings["FlyID"]; 
            string keyReady = inputCSVstrings["DataTitle"];
            string valueReady = inputCSVstrings["DataValue"];

            //add output data when it is not empty
            keyReady = string.IsNullOrWhiteSpace(outputCSVstrings["DataTitle"]) ? keyReady : keyReady + "," + outputCSVstrings["DataTitle"];
            valueReady = string.IsNullOrWhiteSpace(outputCSVstrings["DataValue"]) ? valueReady : valueReady + "," + outputCSVstrings["DataValue"];
            
            string systemSafeFileName = flyID.Replace(" ", "");
            systemSafeFileName = Path.GetInvalidFileNameChars()
                                    .Aggregate(systemSafeFileName, (current, c) => current.Replace(c.ToString(),""));

            //write only when toggle is connected
            if (this.Params.Input.Last().Sources.Any())
            {
                //first open check
                if (_isFirstTimeOpen)
                {
                    _isFirstTimeOpen = false;
                    setWriteFileToFalse();
                    return;
                }
                this._write = writeFile;
            }
            else
            {
                this._write = false;
            }
            
            //var ViewNames = new List<string>();
            
            
            //if we aren't told to write, clean out the list of already written items
            if (!_write)
            {
                DA.SetDataList(0, _printOutStrings);
                _alreadyWrittenLines = new List<string>();
                this.Message = "[OVERRIDE MODE]\n" + OverrideTypes.ToString() + "\n------------------------------\n[RECORDING DISABLED]\n";
                return;
                

            }
            
                //if we are told to run and we haven't written this line yet, do so

            if (_write)
            {
                
                //Check folder if existed
                checkStudyFolder(Folder);
                
                //check csv file
                if (!File.Exists(csvPath))
                {
                    //clean out the list of already written items
                    _printOutStrings = new List<string>();
                    //add key lines 
                    
                    //check if there is one or more imges
                    if (imgParams.IsDefined)
                    {
                        int imgCounts = imgParams.ViewNames.Count;
                        imgCounts = imgCounts > 0 ? imgCounts : 1;

                        if (imgCounts>1)
                        {
                            for (int i = 1; i <= imgCounts; i++)
                            {
                                keyReady += ",img_" + i;
                            }
                        }
                        else
                        {
                            keyReady += ",img";
                        }
                        
                    }
                    else
                    {
                        keyReady += ",img";
                    }

                    if (JSON.IsDefined)
                        keyReady += ",threeD";

                    keyReady += Environment.NewLine;
                    File.WriteAllText(csvPath, keyReady);
                    _alreadyWrittenLines.Add("[Title] "+keyReady);
                }
                else
                {
                    //add data lins
                    if (!_alreadyWrittenLines.Contains("[FlyID] " + flyID))
                    {
                        string writeInData = valueReady;

                        //save img
                        string imgFileName = captureViews(imgParams, systemSafeFileName);
                        writeInData += ","+imgFileName;

                        //save json
                        if (JSON.IsDefined)
                        {
                            string jsonFileName = systemSafeFileName + ".json";
                            string jsonFilePath = Folder + @"\" + jsonFileName;
                            File.WriteAllText(jsonFilePath, JSON.JsonSting);
                            writeInData += "," + jsonFileName;
                        }

                        //save csv // add data at the end 
                        //writeInData = string.Format("{0},{1},{2}\n", valueReady, imgFileName, jsonFileName);
                        writeInData += "\n";
                        File.AppendAllText(csvPath, writeInData);
                        
                        //add this line to our list of already written lines
                        _alreadyWrittenLines.Add("[FlyID] "+flyID);
                    }

                }
                
                _printOutStrings = _alreadyWrittenLines;
                
                //updateMsg();
                this.Message = "[OVERRIDE MODE]\n" + OverrideTypes.ToString()+ "\n------------------------------\n[RECORDING STARTED]\n";
                DA.SetDataList(0, _printOutStrings);

            }

            //set output
            //DA.SetData(0, writeInData);
            

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
                return Colibri.Grasshopper.Properties.Resources.Aggregator;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{c285fdce-3c5b-4701-a2ca-c4850c5aa2b7}"); }
        }
        
        public override bool Read(GH_IReader reader)
        {
            int readValue = -1;

            if (reader.ItemExists("recordingMode"))
            {
                reader.TryGetInt32("recordingMode", ref readValue);
                OverrideTypes = (OverrideMode)readValue;
            }

            if (reader.ItemExists("Write"))
            {
                this._isFirstTimeOpen =  reader.GetBoolean("Write");
            }
            
            
            return base.Read(reader);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("recordingMode", (int)OverrideTypes);
            writer.SetBoolean("Write", true);
            return base.Write(writer);  
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);


            Menu_AppendItem(menu, "Clean the foler and run all", Menu_DoClick_Override, true, OverrideTypes == OverrideMode.OverrideAll)
                .ToolTipText = "This will clean the folder first, and then start from beginning.";

            Menu_AppendItem(menu, "Run all and append to the end", Menu_DoClick_AppendAll, true, OverrideTypes == OverrideMode.AppendAllToTheEnd)
                .ToolTipText ="Keep all data, and append all new data to the end of CSV.";

            Menu_AppendItem(menu, "Run the rest and append to the end", Menu_DoClick_FinishRest, true, OverrideTypes == OverrideMode.FinishTheRest)
                .ToolTipText = "Only run what is left compared to the existing CSV. (All settings must be same with the previous)";

            Menu_AppendItem(menu, "Ask me everytime if the folder is not empty", Menu_DoClick_Default, true, OverrideTypes == OverrideMode.AskEverytime);
            
            Menu_AppendSeparator(menu);
        }

        private void Menu_DoClick_FinishRest(object sender, EventArgs e)
        {
            this.setWriteFileToFalse();
            this.OverrideTypes = OverrideMode.FinishTheRest;
            updateMsg();
            
        }

        private void Menu_DoClick_AppendAll(object sender, EventArgs e)
        {
            this.setWriteFileToFalse();
            this.OverrideTypes = OverrideMode.AppendAllToTheEnd;
            updateMsg();
        }

        private void Menu_DoClick_Override(object sender, EventArgs e)
        {
            this.setWriteFileToFalse();
            this.OverrideTypes = OverrideMode.OverrideAll;
            updateMsg();
        }

        private void Menu_DoClick_Default(object sender, EventArgs e)
        {
            this.setWriteFileToFalse();
            this.OverrideTypes = OverrideMode.AskEverytime;
            updateMsg();
        }
        
        private void updateMsg()
        {
            this.Message = "[OVERRIDE MODE]\n" + this.OverrideTypes.ToString();
            int recordedCount = _printOutStrings.Select(_ => _.StartsWith("FlyID")).Count();

            if (_write)
            {
                this.Message += "\n------------------------------\n[RECORDING STARTED]\n";
                //this.Message += (_printOutStrings.Count - 1).ToString() + " new data added";
            }
            else if(this.Params.Input.Last().Sources.Any())
            {
                this.Message += "\n------------------------------\n[RECORDING DISABLED]\n";
                //this.Message += recordedCount + " new data added";

            }
            else
            {
                this.Message += "\n------------------------------\n[RECORDING DISABLED]";
            }
            
            this.ExpireSolution(true);
            
        }

        private string captureViews(ImgParam imgParams,string flyID)
        {
            if (imgParams.IsActive)
            {
                //string imgID = flyID;
                var ViewNames = new List<string>();
                int width = 600;
                int height = 600;


                string imgName = flyID;
                string imgPath = string.Empty;

                var imgCSV = new List<string>();


                // overwrite the image parameter setting if user has inputed the values
                if (imgParams.IsDefined)
                {
                    bool isThereNoImgName = imgParams.SaveName == "defaultName";
                    imgName = isThereNoImgName ? imgName : imgParams.SaveName;
                    ViewNames = imgParams.ViewNames;
                    width = imgParams.Width;
                    height = imgParams.Height;

                }

                Size imageSize = new Size(width, height);
                //If ViewNames is empty, which means to capture current active view
                var activeView = Rhino.RhinoDoc.ActiveDoc.Views.ActiveView;
                if (!ViewNames.Any())
                {
                    imgName += ".png";
                    imgPath = Folder + @"\" + imgName;

                    activeView.Redraw();

                    var pic = activeView.CaptureToBitmap(imageSize);
                    pic.Save(imgPath);

                    //return here, and skip the following views' check
                    return imgName;

                }

                //If user set View Names
                var views = Rhino.RhinoDoc.ActiveDoc.Views.ToDictionary(v => v.ActiveViewport.Name, v => v);
                var namedViews = Rhino.RhinoDoc.ActiveDoc.NamedViews.ToDictionary(v => v.Name, v => v);

                //string newImgPathWithViewName = ImagePath;
                string currentImgName = imgName;
                for (int i = 0; i < ViewNames.Count; i++)
                {

                    string viewName = ViewNames[i];
                    string existViewName = string.Empty;

                    if (views.ContainsKey(viewName))
                    {
                        activeView = views[viewName];
                        existViewName = viewName;
                    }
                    else if (namedViews.ContainsKey(viewName))
                    {
                        existViewName = viewName;
                        var namedViewIndex = Rhino.RhinoDoc.ActiveDoc.NamedViews.FindByName(viewName);
                        Rhino.RhinoDoc.ActiveDoc.NamedViews.Restore(namedViewIndex, Rhino.RhinoDoc.ActiveDoc.Views.ActiveView, true);
                    }

                    //capture
                    if (!string.IsNullOrEmpty(existViewName))
                    {
                        currentImgName = imgName + "_" + existViewName + ".png";
                        imgCSV.Add(currentImgName);
                        imgPath = Folder + @"\" + currentImgName;
                        //save imgs
                        activeView.Redraw();
                        var pic = activeView.CaptureToBitmap(imageSize);
                        pic.Save(imgPath);

                    }

                }

                return string.Join(",", imgCSV);

            }
            else
            {
                return "";
            }
            
        }

        public void setWriteFileToFalse()
        {
            if (this.Params.Input.Last().Sources.Any())
            {
                var writeFileToggle = this.Params.Input.Last().Sources.First() as GH_BooleanToggle;
                if (writeFileToggle != null)
                {
                    writeFileToggle.Value = false;
                    writeFileToggle.ExpireSolution(true);
                }
                
            }
        }

        
        //Check if Aggregator exist, and if it is at the last
        public List<string> CheckAggregatorIfReady()
        {
            setToLast();
            var checkingMsg = new List<string>();
            checkingMsg = checkIfRecording(checkingMsg);
            //checkingMsg = checkIfLast(checkingMsg);
            return checkingMsg;

        }
        
        private void setToLast()
        {

            var doc = GH.Instances.ActiveCanvas.Document;
            bool isAggregatorLast = doc.Objects.Last().InstanceGuid.Equals(this.InstanceGuid);

            if (!isAggregatorLast)
            {
                this.OnPingDocument().DeselectAll();
                this.Attributes.Selected = true;
                this.OnPingDocument().BringSelectionToTop();
                this.Attributes.Selected = false;
            }
            
        }

        private List<string> checkIfRecording(List<string> msg)
        {
            string warningMsg = "  Aggregator is not recording the data.\n\t[SOLUTION]: set Aggregator's \"write?\" to true.";
            var isRecording = this.Params.Input.Last().VolatileData.AllData(true).First() as GH.Kernel.Types.GH_Boolean;

            if (!isRecording.Value)
            {
                msg.Add(warningMsg);
            }
            
            return msg;

        }
        

        private void checkStudyFolder(string StudyFolderPath)
        {
            string warningMsg = "Study folder is not empty, do you want to override everything inside!";
            string csvFilePath = StudyFolderPath + "\\data.csv";
            
            if (!Directory.Exists(StudyFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(StudyFolderPath);
                    
                }
                catch (Exception ex)
                {

                    throw ex;
                }

                return;
            }

            
            if (!_alreadyWrittenLines.IsNullOrEmpty()) return;
            
            //Check Mode
            if (this.OverrideTypes == OverrideMode.OverrideAll)
            {
                cleanTheFolder(StudyFolderPath);
            }
            else if (this.OverrideTypes == OverrideMode.AppendAllToTheEnd || this.OverrideTypes == OverrideMode.FinishTheRest)
            {
                //do nothing, append the end
            }
            else if(Directory.GetFiles(StudyFolderPath).Any() && this.OverrideTypes == OverrideMode.AskEverytime)
            {
                //popup msg box and ask user
                var userClick = MessageBox.Show(warningMsg, "Attention", MessageBoxButtons.YesNo);
                if (userClick == DialogResult.Yes)
                {
                    cleanTheFolder(StudyFolderPath);
                }
                

            }
            
        }
        
        private void cleanTheFolder(string FolderPath)
        {
            if (!Directory.Exists(FolderPath)) return;
            

            DirectoryInfo folderInfo = new DirectoryInfo(FolderPath);
            try
            {
                foreach (var item in folderInfo.GetFiles())
                {
                    item.Delete();
                }

            }
            catch (Exception)
            {
                //MessageBox.Show("Override the folder failed, please clean up the folder manually./n/n"+ex.ToString());
                //throw ex;
            }
        }


    }
}
