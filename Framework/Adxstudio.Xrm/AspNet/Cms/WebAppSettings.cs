/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.Cms
{
	using System;
	using System.Text.RegularExpressions;
	using System.Web.Hosting;
	using Adxstudio.Xrm.Configuration;

	/// <summary>
	/// Azure web app settings.
	/// </summary>
	public class WebAppSettings
	{
		/// <summary>
		/// Backing instance.
		/// </summary>
		private static readonly Lazy<WebAppSettings> LazyInstance = new Lazy<WebAppSettings>();

		/// <summary>
		/// The single instance.
		/// </summary>
		public static WebAppSettings Instance => LazyInstance.Value;

		/// <summary>
		/// The application startup timestamp.
		/// </summary>
		public DateTimeOffset StartedOn { get; private set; }

		/// <summary>
		/// The app instance Id.
		/// </summary>
		public string InstanceId { get; set; }

		/// <summary>
		/// Website name
		/// </summary>
		public string SiteName { get; set; }

		/// <summary>
		/// Tests that the application is running as an Azure Web App.
		/// </summary>
		public bool AzureWebAppEnabled { get; set; }

		/// <summary>
		/// Tests that the application is running as an Azure Web App and requires App_Data based remote cache invalidation.
		/// </summary>
		public bool AppDataCachingEnabled { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="WebAppSettings" /> class.
		/// </summary>
		public WebAppSettings()
		{
			this.StartedOn = DateTimeOffset.UtcNow;
			this.InstanceId = GetInstanceId();
			this.SiteName = GetWebsiteName();
			this.AzureWebAppEnabled = GetAzureWebAppEnabled();
			this.AppDataCachingEnabled = GetAppDataCachingEnabled();
		}

		/// <summary>
		/// Tests that the application is running as an Azure Web App and requires App_Data based remote cache invalidation.
		/// </summary>
		/// <returns>The flag.</returns>
		private static bool GetAppDataCachingEnabled()
		{
			// running in Azure Web Apps with a mode greater than "Limited"

			var websiteMode = GetAppSettingOrEnvironmentVariable("WEBSITE_SKU");
			return websiteMode != null && !string.Equals(websiteMode, "Free");
		}

		/// <summary>
		/// Tests that the application is running as an Azure Web App.
		/// </summary>
		/// <returns>The flag.</returns>
		private static bool GetAzureWebAppEnabled()
		{
			var websiteMode = GetAppSettingOrEnvironmentVariable("WEBSITE_SKU");
			return websiteMode != null;
		}

		/// <summary>
		/// Gets the web app instance Id.
		/// </summary>
		/// <returns>The id.</returns>
		private static string GetInstanceId()
		{
			var id = GetAppSettingOrEnvironmentVariable("WEBSITE_INSTANCE_ID")
				?? HostingEnvironment.ApplicationID;

			if (!string.IsNullOrWhiteSpace(id))
			{
				var permitted = new Regex("[^0-9a-zA-Z_.-]");
				return permitted.Replace(id, ".");
			}

			return null;
		}

		/// <summary>
		/// Gets the web app site name.
		/// </summary>
		/// <returns>The website name.</returns>
		private static string GetWebsiteName()
		{
			return GetAppSettingOrEnvironmentVariable("WEBSITE_SITE_NAME") ?? HostingEnvironment.SiteName;
		}

		/// <summary>
		/// Retrieves and environment variable value from the current process.
		/// </summary>
		/// <remarks>
		/// The environment variable can be overriden by an appSetting value of the same name.
		/// </remarks>
		/// <param name="name">The variable name.</param>
		/// <returns>The variable value.</returns>
		private static string GetAppSettingOrEnvironmentVariable(string name)
		{
			return name.ResolveAppSetting() ?? Environment.GetEnvironmentVariable(name);
		}
	}
}
