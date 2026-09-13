/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// The configuration settings for a logical name filter attribute.
	/// </summary>
	/// <seealso cref="FederationCrmConfigurationManager"/>
	/// <seealso cref="UserRegistrationElement"/>
	public sealed class SignUpAttributeElement : ConfigurationElement
	{
		private static readonly ConfigurationPropertyCollection _properties;
		private static readonly ConfigurationProperty _propLogicalName;

		static SignUpAttributeElement()
		{
			_propLogicalName = new ConfigurationProperty("logicalName", typeof(string), null, ConfigurationPropertyOptions.IsRequired | ConfigurationPropertyOptions.IsKey);

			_properties = new ConfigurationPropertyCollection { _propLogicalName };
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get { return _properties; }
		}

		public override bool IsReadOnly()
		{
			return false;
		}

		/// <summary>
		/// The logical name of an entity attribute.
		/// </summary>
		public string LogicalName
		{
			get { return (string)base[_propLogicalName]; }
			set { base[_propLogicalName] = value; }
		}
	}
}
