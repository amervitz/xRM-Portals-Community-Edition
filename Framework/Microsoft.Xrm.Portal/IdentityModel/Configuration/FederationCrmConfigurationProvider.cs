/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Configuration;
using Microsoft.Xrm.Portal.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// Provides federated authentication configuration settings based on the <see cref="ConfigurationManager"/>.
	/// </summary>
	public class FederationCrmConfigurationProvider
	{
		private static IdentityModelSection CreateConfiguration()
		{
			var configuration = ConfigurationManager.GetSection(IdentityModelSection.SectionName) as IdentityModelSection ?? new IdentityModelSection();
			var args = new IdentityModelSectionCreatedEventArgs { Configuration = configuration };

			var handler = ConfigurationCreated;

			if (handler != null)
			{
				handler(null, args);
			}

			return args.Configuration;
		}

		/// <summary>
		/// Occurs after the <see cref="IdentityModelSection"/> configuration is created.
		/// </summary>
		public static event EventHandler<IdentityModelSectionCreatedEventArgs> ConfigurationCreated;

		private IdentityModelSection _identityModelSection;

		protected virtual IdentityModelSection GetIdentityModelSection()
		{
			return _identityModelSection ?? (_identityModelSection = CreateConfiguration());
		}

		/// <summary>
		/// Retrieves the configured user registration settings.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public virtual IUserRegistrationSettings GetUserRegistrationSettings(string portalName)
		{
			var section = GetIdentityModelSection();

			var settings = section.Registration;

			return settings;
		}

		/// <summary>
		/// Retrieves the metadata values needed to retrieve a user entity.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public virtual IUserResolutionSettings GetUserResolutionSettings(string portalName)
		{
			var portal = PortalCrmConfigurationManager.CreatePortalContext(portalName);
			var settings = portal as IUserResolutionSettings;

			return settings;
		}
	}
}
