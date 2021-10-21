using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace SafeLayout
{
	public class SafeLayoutPlugIn : Rhino.PlugIns.PlugIn
	{
		public SafeLayoutPlugIn()
		{
			Instance = this;

			if (!this.Settings.Keys.Contains("enabled"))
				this.Settings.SetBool("enabled", true);
			if (!this.Settings.Keys.Contains("new_layer_layout_visible"))
				this.Settings.SetBool("new_layer_visible_in_layout", false);

			Rhino.Display.RhinoView.SetActive += RhinoView_SetActive;
			Rhino.RhinoDoc.LayerTableEvent += RhinoDoc_LayerTableEvent;
            Rhino.RhinoDoc.AddRhinoObject += RhinoDoc_AddRhinoObject;
			Rhino.RhinoDoc.ReplaceRhinoObject += RhinoDoc_ReplaceRhinoObject;
            //Rhino.RhinoDoc.InstanceDefinitionTableEvent += RhinoDoc_InstanceDefinitionTableEvent;
	}

        private void RhinoDoc_InstanceDefinitionTableEvent(object sender, Rhino.DocObjects.Tables.InstanceDefinitionTableEventArgs e)
        {
			//There doesn't appear to be an event for exploding a block, so for now exploded blocks will appear in all detail views even if previously hidden.
			//The only option I can think of is to write our own SafeLayout_ExplodeBlock command
			//RhinoApp.WriteLine("SL : RhinoDoc_InstanceDefinitionTableEvent");
		}

        private void RhinoDoc_LayerTableEvent(object sender, Rhino.DocObjects.Tables.LayerTableEventArgs e)
		{

			// enabled ?
			if (!this.Settings.GetBool("enabled")) return;

			// Not add event
			if (e.EventType != Rhino.DocObjects.Tables.LayerTableEventType.Added) return;

			//  In detail view
			if (Rhino.RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.ViewportType == Rhino.Display.ViewportType.DetailViewport) return;

			// Holding shift inverse SafeLayout new layer behavior.
			if (this.Settings.GetBool("new_layer_visible_in_layout") != ((Eto.Forms.Keyboard.Modifiers & Eto.Forms.Keys.Shift) != 0)) return; 

			// Hide layer in every layouts and details
			Rhino.DocObjects.Layer layer = Rhino.RhinoDoc.ActiveDoc.Layers.FindIndex(e.LayerIndex);
			foreach (Rhino.Display.RhinoPageView pageView in Rhino.RhinoDoc.ActiveDoc.Views.GetPageViews())
			{
				//layer.SetPerViewportVisible(pageView.MainViewport.Id, false);
				foreach (Rhino.DocObjects.DetailViewObject detail in pageView.GetDetailViews())
					layer.SetPerViewportVisible(detail.Id, false);
			}

			Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
			//RhinoApp.WriteLine("SL : RhinoDoc_LayerTableEvent");	
		}

		private void RhinoDoc_AddRhinoObject(object sender, Rhino.DocObjects.RhinoObjectEventArgs e)
        {
			// enabled ?
			//RhinoApp.WriteLine("SL : RhinoDoc_AddObjectEvent");
			if (!this.Settings.GetBool("enabled")) return;
			if (this.Settings.GetBool("new_object_visible_in_layout")) return;
			if (one_shot_disable) { one_shot_disable = false; return; }
			
			var theObject = e.TheObject;
			var doc = RhinoDoc.ActiveDoc;

			// Hide object in every layouts and details
			foreach (Rhino.Display.RhinoPageView pageView in Rhino.RhinoDoc.ActiveDoc.Views.GetPageViews())
			{
				foreach (Rhino.DocObjects.DetailViewObject detail in pageView.GetDetailViews())
				{ 
					var attributes = theObject.Attributes.Duplicate();
					attributes.AddHideInDetailOverride(detail.Id);
					doc.Objects.ModifyAttributes(theObject, attributes, true);
				}
			}


		}

		private void RhinoDoc_ReplaceRhinoObject(object sender, Rhino.DocObjects.RhinoReplaceObjectEventArgs e)
		{
			//RhinoApp.WriteLine("SL : RhinoDoc_ReplaceObjectEvent");
			//Rhino creates this event then an AddObject event when you modify an object
			//Need to one-shot disable our AddObject event
			
			one_shot_disable = true;

		}

		//TODO: When you explode a BLOCK, the newly added components should be turned on

		private void RhinoView_SetActive(object sender, Rhino.Display.ViewEventArgs e)
		{
			// enabled ?
			if (!this.Settings.GetBool("enabled")) return;

			if (last_view_type != e.View.MainViewport.ViewportType || last_view_type == (Rhino.Display.ViewportType)(-1))
			{
				last_view_type = e.View.MainViewport.ViewportType;

				if (e.View.MainViewport.ViewportType == Rhino.Display.ViewportType.StandardModelingViewport)
				{
					//switched from paper space to model space
					Rhino.RhinoDoc.ActiveDoc.NamedLayerStates.Restore(layer_states_name, Rhino.DocObjects.Tables.RestoreLayerProperties.Visible);

					//Hide all previously hidden objects
					foreach (var rhino_object_id in object_visibility_state)
					{
						Rhino.RhinoDoc.ActiveDoc.Objects.Hide(rhino_object_id, true);
					}
				}
				else
				{
					//switched from model space to paper space
					//Show all layers
					Rhino.RhinoDoc.ActiveDoc.NamedLayerStates.Save(layer_states_name);
					foreach (Rhino.DocObjects.Layer layer in Rhino.RhinoDoc.ActiveDoc.Layers)
						if (!layer.IsDeleted)
							Rhino.RhinoDoc.ActiveDoc.Layers.ForceLayerVisible(layer.Id);
					//Show all hidden objects
					var object_enumerator_settings = new Rhino.DocObjects.ObjectEnumeratorSettings();
					//TODO: might need to fine tune these settings 
					object_enumerator_settings.IncludeLights = true;
					object_enumerator_settings.IncludeGrips = false;
					object_enumerator_settings.HiddenObjects = true;
					var rhino_objects = Rhino.RhinoDoc.ActiveDoc.Objects.GetObjectList(object_enumerator_settings);
					object_visibility_state.Clear();
					foreach (var rhino_object in rhino_objects)
					{
						if (rhino_object.IsHidden)
						{
							Rhino.RhinoDoc.ActiveDoc.Objects.Show(rhino_object.Id, true);
							object_visibility_state.Add(rhino_object.Id);
						}
					}
				}
				Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
			}
			//RhinoApp.WriteLine("SL : RhinoView_SetActive");
		}

		///<summary>Gets the only instance of the SafeLayoutPlugIn plug-in.</summary>
		public static SafeLayoutPlugIn Instance
		{
			get; private set;
		}

		public override Rhino.PlugIns.PlugInLoadTime LoadTime { get => Rhino.PlugIns.PlugInLoadTime.AtStartup; }
		private Rhino.Display.ViewportType last_view_type = (Rhino.Display.ViewportType)(-1);
		private const String layer_states_name = "SafeLayout:ModelSpace";
		public List<Guid> object_visibility_state = new List<Guid>(); //list of all hidden object in model space, used when switching from paper space back to model space
		private bool one_shot_disable = false;
	}
}