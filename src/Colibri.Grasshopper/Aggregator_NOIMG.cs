using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

using Grasshopper.Kernel;
using Rhino.Display;
using Rhino.Geometry;

namespace Colibri.Grasshopper
{
    public class Aggregator_NOIMG : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Aggregator_NOIMG()
          : base("Aggregator_NOIMG", "Aggregator_NoIMG",
              "Aggregates design input and output data, image & Spectacles filemanes into a data.csv file that Design Explorer can open.",
              "TT Toolbox", "Colibri")
        {
        }

        public override GH_Exposure Exposure { get { return GH_Exposure.tertiary; } }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Folder", "Folder", "Path to a directory to write images, spectacles models, and the data.csv file into.", GH_ParamAccess.item);
            pManager.AddTextParameter("Inputs", "Inputs", "Inputs object from the Colibri Iterator compnent.", GH_ParamAccess.list);
            pManager.AddTextParameter("Outputs", "Outputs", "Outputs object from the Colibri Outputs component.", GH_ParamAccess.list);
            //pManager.AddTextParameter("ImgParams", "ImgParams", "ImgParams object from the Colibri ImageParameters component.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Write?", "Write?", "Set to true to write files to disk.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("SpectaclesFileName", "SpectaclesFileName",
                "Feed this into the Spectacles_SceneCompiler component downstream.", GH_ParamAccess.item);

        }

        //variable to keep track of what lines have been written during a colibri flight
        //private List<string> alreadyWrittenLines = new List<string>();

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //input variables
            string folder = "";
            List<string> inputs = new List<string>();
            List<string> outputs = new List<string>();
            bool writeFile = false;

            //get data
            if (!DA.GetData(0, ref folder))
                return;
            else if (!folder.EndsWith("\\"))
                folder += "\\";

            DA.GetDataList(1, inputs);
            DA.GetDataList(2, outputs);
            if (!DA.GetData(3, ref writeFile) || !writeFile)
                return;

            //operations
            string csvPath = folder + "data.csv";
            var rawData = inputs;
            int inDataLength = rawData.Count;
            rawData.AddRange(outputs);
            int allDataLength = rawData.Count;


            // keys
            string item = Convert.ToString(rawData[0]).Replace("[", "").Replace("]", "").Replace(" ", "");

            if (!File.Exists(csvPath))
            {
                //the first set

                string dataKey = item.Split(',')[0];
                string keyReady = "in:" + dataKey;

                for (int i = 1; i < inputs.Count; i++)
                {
                    item = Convert.ToString(rawData[i]).Replace("[", "").Replace("]", "").Replace(" ", "");
                    dataKey = item.Split(',')[0];
                    keyReady += ",in:" + dataKey;
                }

                for (int i = 0; i < inputs.Count; i++)
                {
                    item = Convert.ToString(rawData[i]).Replace("[", "").Replace("]", "").Replace(" ", "");
                    dataKey = item.Split(',')[0];
                    keyReady += ",out:" + dataKey;
                }

                File.WriteAllText(csvPath, keyReady + Environment.NewLine);
            }


            // values
            string dataValue = item.Split(',')[1];
            string valueReady = dataValue;

            for (int i = 1; i < inputs.Count; i++)
            {
                item = Convert.ToString(rawData[i]).Replace("[", "").Replace("]", "").Replace(" ", "");
                dataValue = item.Split(',')[1];
                valueReady += "," + dataValue;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                item = Convert.ToString(rawData[i]).Replace("[", "").Replace("]", "").Replace(" ", "");
                dataValue = item.Split(',')[1];
                valueReady += "," + dataValue;
            }

            File.AppendAllText(csvPath, valueReady + Environment.NewLine);
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
                return Colibri.Grasshopper.Properties.Resources.Colibri_logobase_4;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{B3C1C7A9-D07F-44F3-B6A1-00E7782F7713}"); }
        }
    }
}
