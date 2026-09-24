/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services.Cache
{
	using System;
	using System.ComponentModel;
	using System.Linq;
	using System.Runtime.Caching;
	using System.Threading;
	using Adxstudio.Xrm.AspNet;
	using Adxstudio.Xrm.EventHubBasedInvalidation;
	using Adxstudio.Xrm.Json;
	using Adxstudio.Xrm.Web;
	using FluentScheduler;
	using global::Owin;
	using Microsoft.Owin.BuilderProperties;
	using Microsoft.Xrm.Client.Configuration;
	using Microsoft.Xrm.Sdk;

	/// <summary>
	/// Helper functions for the current namespace.
	/// </summary>
	public static class Extensions
	{
		#region EventHub

		/// <summary>
		/// Configure Event Hub based cache invalidation.
		/// </summary>
		/// <param name="app">The app.</param>
		/// <param name="websiteId">Current website id.</param>
		/// <param name="createContext">The context generator.</param>
		/// <param name="cacheEventHubJobSettings">The cache Event Hub job settings.</param>
		/// <param name="searchEventHubJobSettings">The search Event Hub job settings.</param>
		/// <param name="cacheInvalidationJobSettings">The cache invalidation job settings.</param>
		public static void ConfigureEventHubCacheInvalidation(this IAppBuilder app, Guid websiteId, Func<CrmDbContext> createContext, EventHubJobSettings cacheEventHubJobSettings, EventHubJobSettings searchEventHubJobSettings, CacheInvalidationJobSettings cacheInvalidationJobSettings)
		{
			WebAppConfigurationProvider.GetPortalEntityList();

			if (!cacheEventHubJobSettings.IsEnabled || !searchEventHubJobSettings.IsEnabled)
			{
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, "Service Bus is not configured.");

				return;
			}

			var cacheManager = new EventHubJobManager(createContext(), cacheEventHubJobSettings);
			var searchManager = new EventHubJobManager(createContext(), searchEventHubJobSettings);

			if (cacheManager.ServiceBusReceiver != null)
			{
				// warm up the client
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Subscription = '{0}' Topic = '{1}'", cacheManager.Subscription.SubscriptionName, cacheManager.Subscription.TopicName));
			}

			app.StartSchedule(
				() =>
				{
					new EventHubJob(cacheManager).Execute();
					new EventHubJob(searchManager).Execute();
					new CacheInvalidationJob(cacheInvalidationJobSettings, createContext(), websiteId).Execute();
				}, run => run.Now().AndEvery(cacheEventHubJobSettings.JobInterval).Seconds(), cacheEventHubJobSettings.Reentrant);

			new AppProperties(app.Properties).OnAppDisposing.Register(() =>
			{
				((IDisposable)cacheManager).Dispose();
				((IDisposable)searchManager).Dispose();
			});

			WebAppConfigurationProvider.AppStartTime = cacheInvalidationJobSettings.StartedOn.ToString("MM/dd/yyyy HH:mm:ss");
		}

		#endregion

		/// <summary>
		/// Configures cache warmup.
		/// </summary>
		/// <param name="app">The app.</param>
		/// <param name="createContext">The context generator.</param>
		/// <param name="settings">The settings.</param>
		public static void WarmupCache(this IAppBuilder app, Func<CrmDbContext> createContext, WarmupCacheSettings settings)
		{
			var json = CrmJsonConvert.SerializeObject(settings);
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, json);

			if (settings.IsEnabled)
			{
				if (settings.AsyncWarmupEnabled)
				{
					// run warmup job as a scheduled job
					app.StartSchedule(() => new WarmupCacheJob(createContext(), settings).Execute(),
						run => run.OnceIn(settings.AsyncWarmupDelay).Seconds());
				}
				else
				{
					try
					{
						// run warmup job synchronously
						new WarmupCacheJob(createContext(), settings).Execute();
					}
					catch (Exception e)
					{
						WebEventSource.Log.GenericErrorException(e);
					}
				}

				// register application shutdown job

				if (settings.PersistOnAppDisposing)
				{
					var properties = new AppProperties(app.Properties);
					var token = properties.OnAppDisposing;

					if (token != CancellationToken.None)
					{
						token.Register(() => ExecutePersistCachedRequestsJob(settings));
					}
				}

				// register scheduled job

				if (settings.PersistOnSchedule)
				{
					app.StartSchedule(() => new PersistCachedRequestsJob(GetCache("Xrm"), settings).Execute(),
						run => run.Now().AndEvery(settings.JobInterval).Seconds(), settings.Reentrant);
				}
			}
		}

		/// <summary>
		/// Executes a <see cref="PersistCachedRequestsJob"/>.
		/// </summary>
		/// <param name="settings">The settings.</param>
		private static void ExecutePersistCachedRequestsJob(WarmupCacheSettings settings)
		{
			try
			{
				new PersistCachedRequestsJob(GetCache("Xrm"), settings).Execute();
			}
			catch (Exception e)
			{
				WebEventSource.Log.GenericErrorException(e);
			}
		}

		private static void StartSchedule(this IAppBuilder app, Action job, Action<RunSpecifier> specifier, bool reentrant = false)
		{
			Action run = () =>
			{
				try
				{
					job();
				}
				catch (Exception exception)
				{
					// The scheduler has no error handling of its own, so a job that throws would
					// otherwise fault on a background thread with nothing recording why.
					WebEventSource.Log.GenericErrorException(exception);
				}
			};

			// A Schedule never runs concurrently with itself, so a reentrant job is handed to a
			// background work item and the schedule is free to start the next run immediately.
			// Queuing is itself refused once the host is shutting down, so it runs inline in that
			// case rather than dropping the job silently.
			var schedule = new Schedule(() =>
			{
				if (!reentrant)
				{
					run();
					return;
				}

				try
				{
					System.Web.Hosting.HostingEnvironment.QueueBackgroundWorkItem(_ => run());
				}
				catch (InvalidOperationException)
				{
					run();
				}
			}, specifier);

			var disposing = new AppProperties(app.Properties).OnAppDisposing;
			if (disposing.CanBeCanceled) disposing.Register(schedule.Stop);
			schedule.Start();
		}

		/// <summary>
		/// Retrieves the <see cref="ObjectCache"/>.
		/// </summary>
		/// <param name="objectCacheName">The configuration name of the cache.</param>
		/// <returns>The cache.</returns>
		private static ObjectCache GetCache(string objectCacheName)
		{
			var cache = CrmConfigurationManager.GetObjectCaches(objectCacheName).FirstOrDefault()
					?? CrmConfigurationManager.CreateObjectCache(objectCacheName);

			return cache;
		}
	}
}
