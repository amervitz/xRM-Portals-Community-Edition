/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.Cms
{
	using System;
	using System.Configuration;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Threading;
	using Adxstudio.Xrm.AspNet.Identity;
	using Adxstudio.Xrm.AspNet.PortalBus;
	using Adxstudio.Xrm.Owin.Security.Saml2;
	using ITfoxtec.Saml2.Cryptography;
	using Microsoft.Owin.Security.Cookies;
	using global::Owin;
	using global::Owin.Security.Providers.LinkedIn;
	using global::Owin.Security.Providers.Yahoo;
	using System.Web.Optimization;
	using System.Globalization;
	using System.IO;
	using System.Web;
	using System.Web.Hosting;
	using System.Web.WebPages;
	using Adxstudio.Xrm.Configuration;
	using Adxstudio.Xrm.Core.Telemetry;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.Cms.SolutionVersions;
	using Adxstudio.Xrm.EventHubBasedInvalidation;
	using Adxstudio.Xrm.Owin;
	using Adxstudio.Xrm.Search.Configuration;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Web;
	using Microsoft.AspNet.Identity.Owin;
	using Microsoft.Owin;
	using Microsoft.Owin.BuilderProperties;
	using Microsoft.Xrm.Sdk;

	public static class Extensions
	{
		public static CrmEntityStoreSettings GetCrmUserStoreSettings(this CrmWebsite website, IOwinContext context)
		{
			if (website == null) throw new ArgumentNullException("website");

			return new CrmEntityStoreSettings
			{
				DeleteByStatusCode = website.Settings.Get<bool?>("Authentication/UserStore/DeleteByStatusCode").GetValueOrDefault(true),
				PortalSolutions = context.Get<PortalSolutions>()
			};
		}

		public static CrmIdentityErrorDescriber GetCrmIdentityErrorDescriber(this CrmWebsite website, IOwinContext context)
		{
			if (website == null) throw new ArgumentNullException("website");

			return new CrmIdentityErrorDescriber(context);
		}

		public static void UseSiteMapAuthentication(this IAppBuilder app, CookieAuthenticationOptions options)
		{
			app.Use<SiteMapAuthenticationMiddleware>(app, options);
		}

		public static void UpdatePrimaryDomainName<TWebsite, TKey>(this IAppBuilder app, WebsiteManager<TWebsite, TKey> websiteManager, TWebsite website, PortalSolutions portalSolutions)
			where TWebsite : CrmWebsite<TKey>
			where TKey : IEquatable<TKey>
		{
			if (portalSolutions.BaseSolutionCrmVersion < BaseSolutionVersions.PotassiumVersion)
			{
				return;
			}

			var domainName = PortalSettings.Instance.DomainName;
			var websiteBindingsCount = website.Bindings.Count();

			// ensure that there is only one website binding
			if (!string.Equals(website.PrimaryDomainName, domainName, StringComparison.OrdinalIgnoreCase) && websiteBindingsCount == 1)
			{
				website.PrimaryDomainName = domainName;
				websiteManager.UpdateAsync(website).GetAwaiter().GetResult();
				return;
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Multiple website bindings found. Not updating Primary Domain Name");
		}

		public static void UseApplicationRestartPluginMessage(this IAppBuilder app, PluginMessageOptions options)
		{
			app.Use<ApplicationRestartPluginMessageMiddleware>(app, options);
		}

		public static void UsePortalBus<TMessage>(this IAppBuilder app, WebAppSettings webAppSettings, EventHubJobSettings eventHubJobSettings)
		{
			if (webAppSettings.AppDataCachingEnabled && !eventHubJobSettings.IsEnabled)
			{
				UseAppDataPortalBus(app, new AppDataPortalBusOptions<TMessage>(webAppSettings));
			}
			else
			{
				UseLocalOnlyPortalBus<TMessage>(app);
			}
		}

		public static void UseLocalOnlyPortalBus<TMessage>(this IAppBuilder app)
		{
			PortalBusManager<TMessage>.Subscribe(new LocalOnlyPortalBusProvider<TMessage>(app));
		}

		public static void UseAppDataPortalBus<TMessage>(this IAppBuilder app, AppDataPortalBusOptions<TMessage> options)
		{
			try
			{
				PortalBusManager<TMessage>.Subscribe(new AppDataPortalBusProvider<TMessage>(app, options));
			}
			catch (Exception e)
			{
				WebEventSource.Log.GenericErrorException(new Exception($"PortalBus error: InstanceId: {options.InstanceId}", e));
				throw;
			}
		}

		public static void UsePortalsAuthentication<TUser>(this IAppBuilder app, StartupSettingsManager<TUser> manager)
			where TUser : CrmUser
		{
			CryptoConfig.AddAlgorithm(typeof(RSAPKCS1SHA256SignatureDescription), "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");

			if (manager.MicrosoftAccount != null) app.UseMicrosoftAccountAuthentication(manager.MicrosoftAccount);
			if (manager.Twitter != null) app.UseTwitterAuthentication(manager.Twitter);
			if (manager.Facebook != null) app.UseFacebookAuthentication(manager.Facebook);
			if (manager.Google != null) app.UseGoogleAuthentication(manager.Google);

			if (manager.LinkedIn != null) app.UseLinkedInAuthentication(manager.LinkedIn);
			if (manager.Yahoo != null) app.UseYahooAuthentication(manager.Yahoo);

			if (manager.WsFederationOptions != null)
			{
				foreach (var options in manager.WsFederationOptions)
				{
					app.UseWsFederationAuthentication(options);
				}
			}

			if (manager.Saml2Options != null)
			{
				foreach (var options in manager.Saml2Options)
				{
					app.UseSaml2Authentication(options);
				}
			}

			if (manager.OpenIdConnectOptions != null)
			{
				foreach (var options in manager.OpenIdConnectOptions)
				{
					app.UseOpenIdConnectAuthentication(options);
				}
			}

			if (manager.AzureAdOptions != null)
			{
				app.UseOpenIdConnectAuthentication(manager.AzureAdOptions);
			}
		}

		public static void UseWebsiteHeaderSettings(this IAppBuilder app, CrmWebsite website)
		{
			app.Use<WebsiteHeaderSettingsMiddleware>(website);
		}

		public static void UseStrictTransportSecuritySettings(this IAppBuilder app, StrictTransportSecurityOptions options)
		{
			app.Use<StrictTransportSecurityMiddleware>(options);
		}

		public static void UseETWMiddleware(this IAppBuilder app)
		{
			app.Use<ETWMiddleware>();
		}

		public static void ConfigureApplicationLifecycleEvents(this IAppBuilder app)
		{
			// Log an event on App Start
			WebEventSource.Log.WriteApplicationLifecycleEvent(ApplicationLifecycleEventCategory.Start);

			var token = new AppProperties(app.Properties).OnAppDisposing;
			if (token != CancellationToken.None)
			{
				token.Register(() =>
				{
					// Log an event on App End
					WebEventSource.Log.WriteApplicationLifecycleEvent(ApplicationLifecycleEventCategory.End, TelemetryState.ApplicationEndInfo.ToString());
				});
			}
		}

		public static void UseRequireSsl(this IAppBuilder app, RequireSslOptions options)
		{
			app.Use<RequireSslMiddleware>(options);
		}

		public static void UseAppInfo(this IAppBuilder app, AppInfoOptions options = null)
		{
			app.Use<AppInfoMiddleware>(options ?? new AppInfoOptions());
		}

		/// <summary>
		/// Adds RequestTelemetry Middleware that will be used to adding request logging
		/// </summary>
		/// <param name="app">type: IAppBuilder</param>
		/// <param name="isConfigured">function used to check if the app is configured or not</param>
		public static void UseRequestTelemetry(this IAppBuilder app, Func<bool> isConfigured)
		{
			app.Use<RequestTelemetryMiddleware>(isConfigured);
		}

		/// <summary>
		/// Adds health middleware that will be used to externally monitor health like availablity, CRM Connectivity etc.,
		/// </summary>
		/// <param name="app"></param>
		public static void UseHealth(this IAppBuilder app)
		{
			app.Use<HealthMiddleware>();
		}

		/// <summary>
		/// Adds middleware to log scaleout notification data into jarvis.
		/// </summary>
		/// <param name="app"></param>
		public static void UseScaleOutTelemetry(this IAppBuilder app)
		{
			app.Use<ScaleOutMiddleware>();
		}

		/// <summary>
		/// Setup Culture for localization and globalization of the application.
		/// </summary>
		/// <param name="app">The app.</param>
		/// <param name="website">The website record.</param>
		public static void ConfigureCulture(this IAppBuilder app, CrmWebsite website, IContentMapProvider contentMapProvider, BundleCollection bundles, Action<BundleCollection, CultureInfo> registerAction)
		{
			UseLocalizedBundles(website.Entity, website.Language, contentMapProvider, bundles, registerAction);
		}

		/// <summary>
		/// Setup Culture for localization and globalization of the application.
		/// </summary>
		/// <param name="website"></param>
		/// <param name="legacyWebsiteLcid">CrmWebsite.Language value, for backwards compatibility with legacy pre-Multi-Language CRM environments.</param>
		/// <param name="bundles"></param>
		/// <param name="registerAction"></param>
		private static void UseLocalizedBundles(Entity website, int legacyWebsiteLcid, IContentMapProvider contentMapProvider, BundleCollection bundles, Action<BundleCollection, CultureInfo> registerAction)
		{
			// For backward compatibility with pre-Multi-Language CRM environments.
			// At this point in code execution, PortalContext is not available yet, so check for existance of adx_defaultlanguage field to determine if this is legacy environment or not.
			if (!ContextLanguageInfo.IsCrmMultiLanguageEnabledInWebsite(website))
			{
				if (legacyWebsiteLcid != 0)
				{
					CultureInfo culture = new CultureInfo(legacyWebsiteLcid);
					CultureInfo.DefaultThreadCurrentCulture = culture;
					CultureInfo.DefaultThreadCurrentUICulture = culture;
					registerAction(bundles, culture);
				}
				return;
			}
			
			// Note: for environments WITH multi-language, the CurrentCulture will be set by ContextLanguageInfo.BuildContextLanguageInfo(...)

			// Not able to use portal language context so getting language directly
			// (portal context depends on owin context which is accesible only per request, not on startup)

			WebsiteNode websiteNode = null;
			contentMapProvider.Using(contentMap => contentMap.TryGetValue(website, out websiteNode));

			if (websiteNode == null)
			{
				registerAction(bundles, CultureInfo.CurrentCulture);
			}

			foreach (var websiteLanguage in websiteNode.WebsiteLanguages)
			{
				var portalLanguage = websiteLanguage.PortalLanguage;
				var culture = ContextLanguageInfo.GetCulture(portalLanguage.CrmLanguage ?? 0);

				registerAction(bundles, culture);
			}
		}

		public static ContentMapProvider ConfigureContentMap(
			this IAppBuilder app,
			Func<CrmDbContext> createContext,
			CrmWebsite website,
			EventHubJobSettings eventHubJobSettings,
			PortalSolutions portalSolutions)
		{
			var solutionDefinitionProvider = new CmsSolutionDefinitionProvider(portalSolutions, website);
			var contentMapProvider = new ContentMapProvider(createContext, solutionDefinitionProvider, eventHubJobSettings, portalSolutions);

			app.CreatePerOwinContext(() => contentMapProvider);

			// support legacy accessors
			AdxstudioCrmConfigurationProvider.Set(solutionDefinitionProvider);
			AdxstudioCrmConfigurationProvider.Set(contentMapProvider);

			return contentMapProvider;
		}

		public static void ConfigureDisplayModes(this IAppBuilder app, CrmWebsite website)
		{
			if (website != null)
			{
				DisplayModeProvider.Instance.Modes.Insert(
					0, new FacebookDisplayMode(website.Settings.Get<string>("DisplayModes/Facebook/HostName")));
				DisplayModeProvider.Instance.Modes.Insert(
					1, new IframeDisplayMode(website.Settings.Get<string>("DisplayModes/Iframe/HostName")));
			}
		}

		#region Search

		public static void ConfigureSearchProvider(this IAppBuilder app, CrmWebsite website)
		{
			var searchElement = AdxstudioCrmConfigurationManager.GetCrmSection().Search;

			searchElement.Enabled = website.Settings.Get<bool>("Search/Enabled");

			if (searchElement.Enabled)
			{
				if (searchElement.Providers.Count == 0)
				{
					ConfigureProviderSettings(searchElement, website);
				}
			}
		}

		private static void ConfigureProviderSettings(SearchElement searchElement, CrmWebsite website)
		{
			var searchIndexPath = GetSearchIndexPath("Adxstudio.Xrm.Search", true);
			var indexQueryName = website.Settings.Get<string>("Search/IndexQueryName") ?? "Portal Search";
			var languageSiteSetting = website.Settings.Get<string>("KnowledgeManagement/Article/Language") ?? string.Empty;
			var displayNotesSetting = website.Settings.Get<string>("KnowledgeManagement/DisplayNotes") ?? string.Empty;
			var notesFilterSetting = website.Settings.Get<string>("KnowledgeManagement/NotesFilter") ?? string.Empty;
			var useEncryptedDirectory = GetUseEncryptedDirectory();
			var isOnlinePortal = GetIsOnlinePortal();

			var defaultProvider = searchElement.DefaultProvider ?? "Portal";
			searchElement.DefaultProvider = defaultProvider;

			var settings = new ProviderSettings
			{
				Name = defaultProvider,
				Type = "Adxstudio.Xrm.Search.PortalSearchProvider, Adxstudio.Xrm"
			};
			settings.Parameters.Add("portalName", "Xrm");
			settings.Parameters.Add("dataContextName", "Xrm");
			settings.Parameters.Add("indexPath", searchIndexPath);
			settings.Parameters.Add("indexQueryName", indexQueryName);
			settings.Parameters.Add("useEncryptedDirectory", useEncryptedDirectory.ToString());
			settings.Parameters.Add("isOnlinePortal", isOnlinePortal.ToString());
			settings.Parameters.Add("websiteId", website.Id.ToString());
			settings.Parameters.Add("articlesLanguageCode", languageSiteSetting);
			settings.Parameters.Add("displayNotes", displayNotesSetting);
			settings.Parameters.Add("notesFilter", notesFilterSetting);

			searchElement.Providers.Add(settings);

			if (useEncryptedDirectory)
			{
				// In case of encryption key changes we have requirment to drop old search indexes on application startup.
				Search.Store.Encryption.EncryptedDirectoryUtils.CleanupLegacy(searchIndexPath, isOnlinePortal);
			}
		}

		private static bool GetIsOnlinePortal()
		{
			return ConfigurationManager.AppSettings["PortalConfigType"] == "online";
		}

		private static bool GetUseEncryptedDirectory()
		{
			var useEncryptedDirectory = ConfigurationManager.AppSettings["EncryptIndex"] == "true";

			return useEncryptedDirectory;
		}

		private static string GetSearchIndexPath(string subfolder, bool allowCreate = false)
		{
			var appDataPath = GetAppDataPath();

			if (allowCreate && !Directory.Exists(appDataPath))
			{
				Directory.CreateDirectory(appDataPath);
			}

			var indexPath = Path.Combine(GetAppDataPath(), subfolder);

			return indexPath;
		}

		private static string GetAppDataPath()
		{
			return HostingEnvironment.MapPath("~/App_Data");
		}

		#endregion

		/// <summary>
		/// Set the culture for the request.
		/// </summary>
		/// <param name="app">The app.</param>
		public static void UseCurrentThreadCulture(this IAppBuilder app)
		{
			app.Use((context, next) =>
			{
				var websiteLanguage = ContextLanguageInfo.ResolveContextLanguage(context.Get<HttpContextBase>(typeof(HttpContextBase).FullName));

				if (websiteLanguage != null)
				{
					ContextLanguageInfo.SetCultureInfo(websiteLanguage.Lcid);
				}

				return next.Invoke();
			});
		}

		public static PortalSolutions ConfigurePortalSolutionsDetails(this IAppBuilder app, CrmDbContext context)
		{
			var portalSolutions = new PortalSolutions(context);
			app.CreatePerOwinContext(() => portalSolutions);

			return portalSolutions;
		}

		public static void StartupComplete(this IAppBuilder app)
		{
			CacheItemTelemetry.StartupFlag = false;
		}
	}
}
