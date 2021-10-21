using System;
using Rhino;
using Rhino.Commands;

namespace SafeLayout
{
    public class SafeLayoutSyncCommand : Command
    {
        public SafeLayoutSyncCommand()
        {
            Instance = this;
        }

        ///<summary>Sets object visibility in detail view to match model space.</summary>
        public static SafeLayoutSyncCommand Instance { get; private set; }

        public override string EnglishName => "SafeLayoutSync";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

			var detail = doc.Views.ActiveView.ActiveViewportID;

			if (doc.Views.ActiveView.ActiveViewport.ViewportType != Rhino.Display.ViewportType.DetailViewport)
			{
				Rhino.RhinoApp.WriteLine("Must have a detail viewport active to run this command");
				return Result.Nothing;
			}
			
			//set viewport visibility to match modelspace visibility.
			var object_enumerator_settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
				//TODO: might need to fine tune these settings 
				object_enumerator_settings.IncludeLights = true;
				object_enumerator_settings.IncludeGrips = false;
				object_enumerator_settings.HiddenObjects = true;
				var rhino_objects = doc.Objects.GetObjectList(object_enumerator_settings);
			foreach (var rhino_object in rhino_objects)
            {
				var attributes = rhino_object.Attributes.Duplicate();
				attributes.RemoveHideInDetailOverride(detail);
				doc.Objects.ModifyAttributes(rhino_object, attributes, true);
			}
			foreach (var rhino_object_id in SafeLayoutPlugIn.Instance.object_visibility_state)
            {
				var rhino_object = doc.Objects.Find(rhino_object_id);
				var attributes = rhino_object.Attributes.Duplicate();
				attributes.AddHideInDetailOverride(detail);
				doc.Objects.ModifyAttributes(rhino_object, attributes, true);
			}

			//TODO: sync detail layer visibility with model layer visibility
			doc.Views.ActiveView.Redraw();

			return Result.Success;
        }
    }
}