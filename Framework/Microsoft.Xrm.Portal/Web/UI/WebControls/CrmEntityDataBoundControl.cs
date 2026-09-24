/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Portal.Runtime;
using Microsoft.Xrm.Sdk;

namespace Microsoft.Xrm.Portal.Web.UI.WebControls
{
	/// <summary>
	/// Abstract base class for databound controls that bind to a single entity (or to a property of
	/// a single entity).
	/// </summary>
	[PersistChildren(false)]
	[ParseChildren(true)]
	[DefaultProperty("DataSource")]
	public abstract class CrmEntityDataBoundControl : DataBoundControl, INamingContainer
	{
		private static readonly object EventItemCreated = new object();
		private static readonly object EventItemDataBound = new object();

		[Category("Behavior"), Description("Occurs when an item is created.")]
		public event RepeaterItemEventHandler ItemCreated
		{
			add { Events.AddHandler(EventItemCreated, value); }
			remove { Events.RemoveHandler(EventItemCreated, value); }
		}

		[Category("Behavior"), Description("Occurs after an item is data-bound but before it is rendered on the page.")]
		public event RepeaterItemEventHandler ItemDataBound
		{
			add { Events.AddHandler(EventItemDataBound, value); }
			remove { Events.RemoveHandler(EventItemDataBound, value); }
		}

		/// <summary>
		/// Gets or sets the entity data item to which this control will bind.
		/// </summary>
		/// <remarks>
		/// If this property is set, the control will databind to this object automatically.
		/// </remarks>
		public virtual object DataItem { get; set; }

		/// <summary>
		/// Gets or sets a custom format string through which the rendered value will be formatted.
		/// </summary>
		/// <remarks>
		/// The object value to which this control is bound will be passed through <see cref="string.Format(string,object)"/>.
		/// </remarks>
		public virtual string Format { get; set; }

		/// <summary>
		/// Gets or sets the flag to perform an HtmlEncode on the output.
		/// </summary>
		[Bindable(false)]
		[Category("Appearance")]
		[DefaultValue(false)]
		[Description("Enables HtmlEncoded output.")]
		public virtual bool HtmlEncode { get; set; }

		/// <summary>
		/// Gets or sets the template that defines how the contained controls are displayed.
		/// </summary>
		[DefaultValue((string)null)]
		[PersistenceMode(PersistenceMode.InnerProperty)]
		[TemplateContainer(typeof(RepeaterItem))]
		[Description("")]
		[Browsable(false)]
		public virtual ITemplate ItemTemplate { get; set; }

		/// <summary>
		/// Gets or sets the property name to retrieve a value from.
		/// This attribute can be a comma-delimited set of property names of which the first not null value will be retrieved.
		/// </summary>
		/// <example>
		/// "Title" will attempt to retrieve the Title property of the bound Entity.
		/// "Summary, Description, Copy" will attempt to retrieve the Summary property followed by the Description and lastly the Copy property, if both Summary and Description were null.
		/// </example>
		[Bindable(false)]
		[Category("Data")]
		[DefaultValue((string)null)]
		public virtual string PropertyName { get; set; }

		public string PortalName { get; set; }

		protected RepeaterItem CreateItem(Control container, int itemIndex, ListItemType itemType, bool dataBind, object dataItem)
		{
			var item = new RepeaterItem(itemIndex, itemType);
			
			var e = new RepeaterItemEventArgs(item);

			ItemTemplate.InstantiateIn(item);

			if (dataBind)
			{
				item.DataItem = dataItem;
			}

			OnItemCreated(e);

			container.Controls.Add(item);

			if (dataBind)
			{
				item.DataBind();
				
				OnItemDataBound(e);
				
				item.DataItem = null;
			}

			return item;
		}

		protected virtual void OnItemCreated(RepeaterItemEventArgs e)
		{
			var handler = (RepeaterItemEventHandler)Events[EventItemCreated];

			if (handler != null)
			{
				handler(this, e);
			}
		}

		protected virtual void OnItemDataBound(RepeaterItemEventArgs e)
		{
			var handler = (RepeaterItemEventHandler)Events[EventItemDataBound];

			if (handler != null)
			{
				handler(this, e);
			}
		}

		protected override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			if (!Page.IsPostBack)
			{
				DataBindToDataItemIfProvided();
			}
		}

		protected override void OnPreRender(EventArgs args)
		{
			base.OnPreRender(args);

			if (Page.IsPostBack)
			{
				DataBindToDataItemIfProvided();
			}
		}

		protected virtual void PerformDataBindingOfCrmEntity(Entity entity)
		{
			if (ItemTemplate != null)
			{
				CreateItem(this, 0, ListItemType.Item, true, entity);
			}
		}

		protected abstract void PerformDataBindingOfCrmEntityProperty(Entity entity, string propertyName, string value);

		protected virtual void PerformDataBindingOfNonCrmEntity(object value)
		{
			// Do nothing by default, this can be overridden in subclasses if desired.
		}

		protected override void PerformDataBinding(IEnumerable data)
		{
			base.PerformDataBinding(data);

			var dataObject = GetDataObject(data);

			var entity = dataObject as Entity;

			// The extracted data item is not an Entity, go down non-entity path.
			if (entity == null)
			{
				PerformDataBindingOfNonCrmEntity(dataObject);

				return;
			}
			
			// No property name provided, bind directly against the entity itself.
			if (string.IsNullOrEmpty(PropertyName))
			{
				PerformDataBindingOfCrmEntity(entity);

				return;
			}

			var property = GetAttributeValue(entity, PropertyName.Split(','));

			// No property was found.
			if (property == null)
			{
				PerformDataBindingOfCrmEntityProperty(entity, null, null);

				return;
			}

			// Property was found, but none of the property fallbacks had a non-null value.
			if (property.Value == null)
			{
				PerformDataBindingOfCrmEntityProperty(entity, property.Name, null);

				return;
			}

			var textValue = (Format ?? "{0}").FormatWith(property.Value);

			var contentFormatter = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<ICrmEntityContentFormatter>(GetType().FullName);

			if (contentFormatter != null)
			{
				textValue = contentFormatter.Format(textValue, entity, this);
			}

			if (HtmlEncode)
			{
				textValue = Encoder.HtmlEncode(textValue, false);
			}

			PerformDataBindingOfCrmEntityProperty(entity, property.Name, textValue);
		}

		protected object GetDataObject(IEnumerable data)
		{
			if (data == null)
			{
				return null;
			}
			
			var enumerator = data.GetEnumerator();

			if (!enumerator.MoveNext())
			{
				return null;
			}

			if (enumerator.Current is CrmSiteMapNode)
			{
				return (enumerator.Current as CrmSiteMapNode).Entity;
			}

			return enumerator.Current;
		}

		private void DataBindToDataItemIfProvided()
		{
			if (DataItem != null)
			{
				DataSource = new List<object> { DataItem };

				DataBind();
			}
		}

		private PropertyNameAndValue GetAttributeValue(Entity entity, IEnumerable<string> propertyNames)
		{
			propertyNames = propertyNames.Where(name => !string.IsNullOrEmpty(name));

			if ((entity == null) || !propertyNames.Any())
			{
				return null;
			}

			var validPropertyNames = new List<string>();

			foreach (var propertyName in propertyNames)
			{
				try
				{
					var value = DataBinder.Eval(entity, propertyName);

					validPropertyNames.Add(propertyName);

					if (value != null)
					{
						return new PropertyNameAndValue(validPropertyNames.First(), value);
					}
				}
				catch (HttpException)
				{
					continue;
				}
			}

			if (validPropertyNames.Any())
			{
				return new PropertyNameAndValue(validPropertyNames.First(), null);
			}

			throw new InvalidOperationException(
				"DataBinding: '{0}' does not contain a property with the name {1}.".FormatWith(
					entity.GetType(),
					string.Join(" or ", propertyNames.Select(name => "'{0}'".FormatWith(name)).ToArray())));
		}

		private class PropertyNameAndValue
		{
			public PropertyNameAndValue(string name, object value)
			{
				name.ThrowOnNullOrWhitespace("name");

				Name = name;
				Value = value;
			}

			public string Name { get; private set; }

			public object Value { get; private set; }
		}
	}
}
