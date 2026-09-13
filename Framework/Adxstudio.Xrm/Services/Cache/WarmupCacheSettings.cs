/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services.Cache
{
	using System;
	using Adxstudio.Xrm.Configuration;
	using Polly;
	using Adxstudio.Xrm.Threading;

	/// <summary>
	/// Settings for the <see cref="PersistCachedRequestsJob"/>.
	/// </summary>
	public class WarmupCacheSettings
	{
		/// <summary>
		/// The default job interval value.
		/// </summary>
		private static readonly TimeSpan DefaultJobInterval = new TimeSpan(1, 0, 0);

		/// <summary>
		/// The default expiration window value.
		/// </summary>
		private static readonly TimeSpan DefaultExpirationWindow = new TimeSpan(0, 30, 0);

		/// <summary>
		/// The interval between running the job in seconds.
		/// </summary>
		public int JobInterval { get; set; }

		/// <summary>
		/// Allows overlapping jobs to run in parallel.
		/// </summary>
		public bool Reentrant { get; set; }

		/// <summary>
		/// A flag for enabling the persist cache feature.
		/// </summary>
		public bool IsEnabled { get; set; }

		/// <summary>
		/// Determines if warmup should run asynchronously.
		/// </summary>
		public bool AsyncWarmupEnabled { get; set; }

		/// <summary>
		/// The number of seconds to wait after startup to invoke the warmup job.
		/// </summary>
		public int AsyncWarmupDelay { get; set; }

		/// <summary>
		/// The duration for indicating expired disk cache items.
		/// </summary>
		public TimeSpan ExpirationWindow { get; set; }

		/// <summary>
		/// Determines if cache is persisted on application shutdown.
		/// </summary>
		public bool PersistOnAppDisposing { get; set; }

		/// <summary>
		/// Determines if cache is persisted on a repeating schedule.
		/// </summary>
		public bool PersistOnSchedule { get; set; }

		/// <summary>
		/// The folder for storing persisted items.
		/// </summary>
		public string AppDataPath { get; set; }

		/// <summary>
		/// The format string for the filename.
		/// </summary>
		public string FilenameFormat { get; set; }

		/// <summary>
		/// Whether to throw any exceptions generated during the job.
		/// </summary>
		public bool PropagateExceptions { get; set; }

		/// <summary>
		/// Retry policy for App_Data IO operations.
		/// </summary>
		public ResiliencePipeline AppDataRetryPolicy { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="WarmupCacheSettings" /> class.
		/// </summary>
		public WarmupCacheSettings()
		{
			var isEnabled = "PortalWarmupCacheEnabled".ResolveAppSetting().ToBoolean().GetValueOrDefault(true);
			var asyncWarmupEnabled = "PortalWarmupCacheAsyncWarmupEnabled".ResolveAppSetting().ToBoolean().GetValueOrDefault(true);
			var expirationWindow = "PortalWarmupCacheExpirationWindow".ResolveAppSetting().ToTimeSpan().GetValueOrDefault(DefaultExpirationWindow);
			var persistOnAppDisposing = "PortalPersistCachedRequestsOnAppDisposing".ResolveAppSetting().ToBoolean().GetValueOrDefault(true);
			var persistOnSchedule = "PortalPersistCachedRequestsOnSchedule".ResolveAppSetting().ToBoolean().GetValueOrDefault();
			var jobInterval = "PortalPersistCachedRequestsJobInterval".ResolveAppSetting().ToTimeSpan().GetValueOrDefault(DefaultJobInterval);
			var retryStrategy = RetryPolicies.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

			this.AppDataPath = "~/App_Data/Adxstudio.Xrm.Services.Cache.Warmup/";
			this.FilenameFormat = "cache_{0}.request";
			this.IsEnabled = isEnabled;
			this.AsyncWarmupEnabled = asyncWarmupEnabled;
			this.AsyncWarmupDelay = 0;
			this.JobInterval = Convert.ToInt32(jobInterval.TotalSeconds);
			this.ExpirationWindow = expirationWindow;
			this.PersistOnAppDisposing = persistOnAppDisposing;
			this.PersistOnSchedule = persistOnSchedule;
			this.PropagateExceptions = false;
			this.AppDataRetryPolicy = Adxstudio.Xrm.IO.Extensions.CreateRetryPolicy(retryStrategy);
		}
	}
}
