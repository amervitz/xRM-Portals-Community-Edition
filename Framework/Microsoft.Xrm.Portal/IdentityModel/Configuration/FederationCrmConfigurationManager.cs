/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Configuration;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// Methods for retrieving dependencies from the configuration.
	/// </summary>
	/// <remarks>
	/// Configure federation with the .NET Framework system.identityModel and
	/// system.identityModel.services sections. Portal registration settings remain
	/// in microsoft.xrm.portal.identityModel. See docs/internal/dependencies.md.
	/// </remarks>
	/// <seealso cref="IUserRegistrationSettings"/>
	public static class FederationCrmConfigurationManager
	{
		private static Lazy<FederationCrmConfigurationProvider> _provider = new Lazy<FederationCrmConfigurationProvider>(CreateProvider);

		private static FederationCrmConfigurationProvider CreateProvider()
		{
			var section = ConfigurationManager.GetSection(IdentityModelSection.SectionName) as IdentityModelSection ?? new IdentityModelSection();

			if (!string.IsNullOrWhiteSpace(section.ConfigurationProviderType))
			{
				var typeName = section.ConfigurationProviderType;
				var type = TypeExtensions.GetType(typeName);

				if (type == null || !type.IsA<FederationCrmConfigurationProvider>())
				{
					throw new ConfigurationErrorsException("The value '{0}' is not recognized as a valid type or is not of the type '{1}'.".FormatWith(typeName, typeof(FederationCrmConfigurationProvider)));
				}

				return Activator.CreateInstance(type) as FederationCrmConfigurationProvider;
			}

			return new FederationCrmConfigurationProvider();
		}

		/// <summary>
		/// Resets the cached dependencies.
		/// </summary>
		public static void Reset()
		{
			_provider = new Lazy<FederationCrmConfigurationProvider>(CreateProvider);
		}

		/// <summary>
		/// Retrieves the configured user registration settings.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public static IUserRegistrationSettings GetUserRegistrationSettings(string portalName = null)
		{
			return _provider.Value.GetUserRegistrationSettings(portalName);
		}

		/// <summary>
		/// Retrieves the metadata values needed to retrieve a user entity.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public static IUserResolutionSettings GetUserResolutionSettings(string portalName = null)
		{
			return _provider.Value.GetUserResolutionSettings(portalName);
		}
	}
}
