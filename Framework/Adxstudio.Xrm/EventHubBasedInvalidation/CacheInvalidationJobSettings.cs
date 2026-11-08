/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using System;
	using Adxstudio.Xrm.AspNet.Cms;

	/// <summary>
	/// Settings for the <see cref="CacheInvalidationJob"/>.
	/// </summary>
	public class CacheInvalidationJobSettings
	{
		/// <summary>
		/// The interval between running the job in seconds.
		/// </summary>
		public int JobInterval { get; set; }

		/// <summary>
		/// The application startup timestamp.
		/// </summary>
		public DateTimeOffset StartedOn { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CacheInvalidationJobSettings" /> class.
		/// </summary>
		/// <param name="webAppSettings">The web app settings.</param>
		public CacheInvalidationJobSettings(WebAppSettings webAppSettings)
		{
			this.StartedOn = webAppSettings.StartedOn;
			this.JobInterval = 2;
		}
	}
}
