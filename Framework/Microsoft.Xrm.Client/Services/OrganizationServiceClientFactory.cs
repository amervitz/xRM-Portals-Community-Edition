using System;
using System.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;

namespace Microsoft.Xrm.Client.Services
{
	/// <summary>Creates the explicitly configured SDK client for all organization-service wrappers.</summary>
	internal static class OrganizationServiceClientFactory
	{
		public static IOrganizationService Create(string connectionString, string organizationServiceType)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new ConfigurationErrorsException("The CRM connection string must be specified.");
			}

			if (organizationServiceType == "ServiceClient")
			{
				var client = new ServiceClient(connectionString);
				if (!client.IsReady)
				{
					throw new InvalidOperationException("ServiceClient could not establish the configured CRM connection.", client.LastException);
				}

				return client;
			}
			else if (organizationServiceType == "CrmServiceClient")
			{
				var client = new CrmServiceClient(connectionString);
				if (!client.IsReady)
				{
					throw new InvalidOperationException("CrmServiceClient could not establish the configured CRM connection.", client.LastCrmException);
				}

				return client;
			}

			throw new ConfigurationErrorsException("Set the OrganizationServiceType app setting to OrganizationServiceProxy, ServiceClient, or CrmServiceClient.");
		}
	}
}
