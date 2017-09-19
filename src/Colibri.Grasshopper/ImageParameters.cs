using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

namespace Colibri.Grasshopper
{
   
    public class ImageParameters : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ImageParameters()
          : base("Image Setting", "Img Setting",
              "Defines how Colibri generates images during flights.  You can specify which viewport[s] to capture, and the resolution of the image.",
              "TT Toolbox", "Colibri 2.0")
        {
        }

        public override GH_Exposure Exposure { get { return GH_Exposure.secondary; } }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ImageSaveAsName", "SaveAs", "Optional input for overwriting the default image name.  Don't do anything here if you don't know how to generate a dynamic name!", GH_ParamAccess.item,"defaultName");
            pManager.AddTextParameter("RhinoViewNames", "Views", "Optional input for the Rhino viewport name which you would like to take a snapshot of.  Acceptable inputs include \"Perspective\", \"Top\", \"Bottom\", etc or any view name that you have already saved within the Rhino file (note that you do not need to input quotations).  If no text is input here, the default will be an image of the active viewport - the last viewport in which you navigated.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Width", "Width", "Image width in pixels.", GH_ParamAccess.item, 600);
            pManager.AddIntegerParameter("Height", "Height", "Image height in pixels.", GH_ParamAccess.item, 600);
            pManager.AddBooleanParameter("Active", "Active", "True if recording images.", GH_ParamAccess.item, true);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ImgSetting", "ImgSetting", "Colibri's image setting object.  Feed this into the Colibri aggregator downstream.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //input variables
            string imgName = "";
            var viewNames = new List<string>();
            int width = 600;
            int height = 600;
            bool active = true;

            //get data
            DA.GetData(0, ref imgName);
            DA.GetDataList(1, viewNames);
            DA.GetData(2, ref width);
            DA.GetData(3, ref height);
            DA.GetData(4, ref active);
            //defense
            if (width < 50 || height < 50)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Width and Height must be greater than 50 pixels.");
                return;
            }

            //set output
            var imageP = new ImgParam(imgName, viewNames, width, height, active);
            //var imageP = new Dictionary<string, object>();
            //imageP.Add("imgName", imgName);
            //imageP.Add("viewNames", viewNames);
            //imageP.Add("Width", width);
            //imageP.Add("Height", height);
            //DA.SetDataList(0, imageP);
            DA.SetData(0, imageP);
            
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
                return Colibri.Grasshopper.Properties.Resources.imgParam;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{2291c6d3-6867-4adf-a8b6-0bc27ce00a27}"); }
        }
    }
}
