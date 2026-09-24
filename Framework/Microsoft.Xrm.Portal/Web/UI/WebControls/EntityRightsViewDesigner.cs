/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Text;
using System.Web.UI;
using System.Web.UI.Design;
using System.Web;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;
using Microsoft.Xrm.Client;

namespace Microsoft.Xrm.Portal.Web.UI.WebControls
{
	public sealed class EntityRightsViewDesigner : ControlDesigner
	{
		private const int NoRightsTemplateIndex = 0;
		private const int RightsTemplateIndex = 1;		
		private EntityRightsView _entityRightsView;
		private TemplateGroupCollection _templateGroups;
		
		private EditableDesignerRegion BuildRegion()
		{
			EditableDesignerRegion region = new EntityRightsViewDesignerRegion(CurrentObject, CurrentTemplate, CurrentTemplateDescriptor, TemplateDefinition)
			{
				Description = "ContainerControlDesigner_RegionWatermark"
			};
			return region;
		}
		
		public override string GetDesignTimeHtml()
		{
			var designTimeHtml = string.Empty;

			if (CurrentViewControlTemplate != null)
			{
				var viewControl = (EntityRightsView)ViewControl;

				IDictionary data = new HybridDictionary(1);

				data["TemplateIndex"] = CurrentView;

				((IControlDesignerAccessor)viewControl).SetDesignModeState(data);

				viewControl.DataBind();

				designTimeHtml = base.GetDesignTimeHtml();
			}

			return designTimeHtml;
		}

		public override string GetDesignTimeHtml(DesignerRegionCollection regions)
		{
			regions.Add(BuildRegion());
			
			var builder = new StringBuilder(0x400);

			builder.Append("<table cellspacing=0 cellpadding=0 border=0 style=\"display:inline-block\">\r\n                <tr>\r\n                    <td nowrap align=center valign=middle style=\"color:{0}; background-color:{1}; \">{2}</td>\r\n                </tr>\r\n                <tr>\r\n                    <td style=\"vertical-align:top;\" {3}='0'>{4}</td>\r\n                </tr>\r\n          </table>".FormatWith(
				new object[] 
				{ 
					ColorTranslator.ToHtml(SystemColors.ControlText), 
					ColorTranslator.ToHtml(SystemColors.Control), 
					System.Web.Security.AntiXss.AntiXssEncoder.HtmlEncode(_entityRightsView.ID ?? "EntityRightsView", false), 
					System.Web.Security.AntiXss.AntiXssEncoder.HtmlEncode(DesignerRegion.DesignerRegionAttributeName, false), 
					string.Empty 
				}));

			return builder.ToString();
		}

		public override string GetEditableDesignerRegionContent(EditableDesignerRegion region)
		{
			if (region is EntityRightsViewDesignerRegion)
			{
				var template = ((EntityRightsViewDesignerRegion)region).Template;

				if (template != null)
				{
					var service = (IDesignerHost)Component.Site.GetService(typeof(IDesignerHost));

					return ControlPersister.PersistTemplate(template, service);
				}
			}

			return base.GetEditableDesignerRegionContent(region);
		}

		protected override string GetEmptyDesignTimeHtml()
		{
			string str;

			switch (CurrentView)
			{
				case NoRightsTemplateIndex:

					str = "EntityRightsView_NoRightsTemplateEmpty";

					break;

				case RightsTemplateIndex:

					str = "EntityRightsView_RightsTemplateEmpty";

					break;

				default:

					str = "EntityRightsView_NoRightsTemplateEmpty";

					break;
			}
			return CreatePlaceHolderDesignTimeHtml(str + "<br>" + "EntityRightsView_NoTemplateInst");
		}

		protected override string GetErrorDesignTimeHtml(Exception e)
		{
			return CreatePlaceHolderDesignTimeHtml("EntityRightsView_ErrorRendering" + "<br />" + HttpContext.Current.Server.HtmlEncode(e.Message));
		}

		public override void Initialize(IComponent component)
		{
			_entityRightsView = (EntityRightsView)component;

			base.Initialize(component);
		}

		public override void SetEditableDesignerRegionContent(EditableDesignerRegion region, string content)
		{
			var region2 = region as EntityRightsViewDesignerRegion;

			if (region2 == null) return;

			var service = (IDesignerHost)Component.Site.GetService(typeof(IDesignerHost));

			var template = ControlParser.ParseTemplate(service, content);

			using (var transaction = service.CreateTransaction("SetEditableDesignerRegionContent"))
			{
				region2.PropertyDescriptor.SetValue(region2.Object, template);

				transaction.Commit();
			}

			region2.Template = template;
		}

		public override DesignerActionListCollection ActionLists
		{
			get
			{
				var lists = new DesignerActionListCollection();

				lists.AddRange(base.ActionLists);

				lists.Add(new EntityRightsViewDesignerActionList(this));

				return lists;
			}
		}

		private object CurrentObject
		{
			get
			{
				return Component;
			}
		}

		private ITemplate CurrentTemplate
		{
			get
			{
				return CurrentView == RightsTemplateIndex ? _entityRightsView.RightsTemplate : _entityRightsView.NoRightsTemplate;
			}
		}

		private PropertyDescriptor CurrentTemplateDescriptor
		{
			get
			{
				return CurrentView == RightsTemplateIndex ? TypeDescriptor.GetProperties(Component)["RightsTemplate"] : TypeDescriptor.GetProperties(Component)["NoRightsTemplate"];
			}
		}

		private int CurrentView
		{
			get
			{
				return DesignerState["CurrentView"] == null ? 0 : (int)DesignerState["CurrentView"];
			}
			set
			{
				DesignerState["CurrentView"] = value;
			}
		}

		private ITemplate CurrentViewControlTemplate
		{
			get
			{
				return CurrentView == RightsTemplateIndex ? ((EntityRightsView)ViewControl).RightsTemplate : ((EntityRightsView)ViewControl).NoRightsTemplate;
			}
		}

		private TemplateDefinition TemplateDefinition
		{
			get
			{
				return CurrentView == RightsTemplateIndex ? new TemplateDefinition(this, "RightsTemplate", _entityRightsView, "RightsTemplate") : new TemplateDefinition(this, "NoRightsTemplate", _entityRightsView, "NoRightsTemplate");
			}
		}

		public override TemplateGroupCollection TemplateGroups
		{
			get
			{
				var templateGroups = base.TemplateGroups;

				if (_templateGroups == null)
				{
					_templateGroups = new TemplateGroupCollection();

					var group = new TemplateGroup("NoRightsTemplate");

					group.AddTemplateDefinition(new TemplateDefinition(this, "NoRightsTemplate", _entityRightsView, "NoRightsTemplate"));

					_templateGroups.Add(group);

					group = new TemplateGroup("RightsTemplate");

					group.AddTemplateDefinition(new TemplateDefinition(this, "RightsTemplate", _entityRightsView, "RightsTemplate"));

					_templateGroups.Add(group);
				}

				templateGroups.AddRange(_templateGroups);

				return templateGroups;
			}
		}

		protected override bool UsePreviewControl
		{
			get
			{
				return true;
			}
		}

		private class EntityRightsViewDesignerActionList : DesignerActionList
		{
			private readonly EntityRightsViewDesigner _designer;

			public EntityRightsViewDesignerActionList(EntityRightsViewDesigner designer) : base(designer.Component)
			{
				_designer = designer;
			}

			public override DesignerActionItemCollection GetSortedActionItems()
			{
				var items = new DesignerActionItemCollection
				{		            		
					new DesignerActionPropertyItem("View", "Views:", string.Empty, "Change the current view template")
				};
				return items;
			}

			public override bool AutoShow
			{
				get
				{
					return true;
				}
				set
				{
				}
			}

			[TypeConverter(typeof(EntityRightsViewViewTypeConverter))]
			public string View
			{
				get
				{
					var currentView = _designer.CurrentView;

					return currentView == RightsTemplateIndex ? "RightsTemplate" : "NoRightsTemplate";
				}
				set
				{
					if (string.Compare(value, "NoRightsTemplate", StringComparison.Ordinal) == 0)
					{
						_designer.CurrentView = NoRightsTemplateIndex;
					}
					else if (string.Compare(value, "RightsTemplate", StringComparison.Ordinal) == 0)
					{
						_designer.CurrentView = RightsTemplateIndex;
					}

					_designer.UpdateDesignTimeHtml();
				}
			}

			private class EntityRightsViewViewTypeConverter : TypeConverter
			{
				public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
				{
					var values = new string[2];

					values[0] = "NoRightsTemplate";

					values[1] = "RightsTemplate";

					return new StandardValuesCollection(values);
				}

				public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
				{
					return true;
				}

				public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
				{
					return true;
				}
			}
		}

		private class EntityRightsViewDesignerRegion : TemplatedEditableDesignerRegion
		{
			private readonly object _object;
			private readonly PropertyDescriptor _prop;

			public EntityRightsViewDesignerRegion(object obj, ITemplate template, PropertyDescriptor descriptor, TemplateDefinition definition) : base(definition)
			{
				Template = template;

				_object = obj;

				_prop = descriptor;

				EnsureSize = true;
			}

			public object Object
			{
				get
				{
					return _object;
				}
			}

			public PropertyDescriptor PropertyDescriptor
			{
				get
				{
					return _prop;
				}
			}

			public ITemplate Template { get; set; }
		}
	}
}
