/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// The configuration settings for federated authentication.
	/// </summary>
	public sealed class IdentityModelSection : ConfigurationSection
	{
		private const string _defaultConfigurationProviderType = "Microsoft.Xrm.Portal.IdentityModel.Configuration.FederationCrmConfigurationProvider, Microsoft.Xrm.Portal";

		/// <summary>
		/// The element name of the section.
		/// </summary>
		public const string SectionName = "microsoft.xrm.portal.identityModel";

		private static readonly ConfigurationPropertyCollection _properties;
		private static readonly ConfigurationProperty _propConfigurationProviderType;
		private static readonly ConfigurationProperty _propRegistration;

		static IdentityModelSection()
		{
			_propConfigurationProviderType = new ConfigurationProperty("sectionProviderType", typeof(string), _defaultConfigurationProviderType, ConfigurationPropertyOptions.None);
			_propRegistration = new ConfigurationProperty("registration", typeof(UserRegistrationElement), new UserRegistrationElement(), ConfigurationPropertyOptions.None);

			_properties = new ConfigurationPropertyCollection { _propConfigurationProviderType, _propRegistration };
		}

		/// <summary>
		/// The configuration provider type name.
		/// </summary>
		[ConfigurationProperty("sectionProviderType", DefaultValue = _defaultConfigurationProviderType)]
		public string ConfigurationProviderType
		{
			get { return (string)base[_propConfigurationProviderType]; }
			set { base[_propConfigurationProviderType] = value; }
		}

		[ConfigurationProperty("registration")]
		public UserRegistrationElement Registration
		{
			get { return (UserRegistrationElement)base[_propRegistration]; }
			set { base[_propRegistration] = value; }
		}

		public override bool IsReadOnly()
		{
			return false;
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get { return _properties; }
		}

		protected override void Reset(ConfigurationElement parentElement)
		{
			base.Reset(parentElement);
			FederationCrmConfigurationManager.Reset();
		}
	}
}
