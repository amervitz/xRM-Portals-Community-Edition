/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Owin
{
	using System;
	using System.Web;
	using System.Threading.Tasks;
	using Microsoft.Owin;
	using Adxstudio.Xrm.Core.Telemetry;
	using Adxstudio.Xrm.Decorators;
	using Adxstudio.Xrm.Performance;
	using Adxstudio.Xrm.Performance.AggregateEvent;

	/// <summary>
	/// Encapsulates the Telemetry for requests
	/// </summary>
	public sealed class RequestTelemetryMiddleware : OwinMiddleware
	{
		/// <summary>
		/// Function to call that determines whether or not the Portal is configured
		/// </summary>
		private Func<bool> IsPortalConfigured { get; } 

		/// <summary>
		/// Initializes a new instance of the <see cref="RequestTelemetryMiddleware"/> class.
		/// </summary>
		/// <param name="next">type: OwinMiddleware</param>
		/// <param name="isPortalConfigured">Test function to determine whether or not the portal is configured</param>
		public RequestTelemetryMiddleware(OwinMiddleware next, Func<bool> isPortalConfigured)
			: base(next)
		{
			this.IsPortalConfigured = isPortalConfigured;
		}

		/// <summary>
		/// Process an individual request.
		/// </summary>
		/// <param name="context">the IOwinContext</param>
		/// <returns>type: IOwinContext</returns>
		public override async Task Invoke(IOwinContext context)
		{
			this.BeginRequest(context);

			await this.Next.Invoke(context);

			this.EndRequest(context);
		}

		/// <summary>
		/// Pre-Request processing
		/// </summary>
		/// <param name="context">type: IOwinContext</param>
		private void BeginRequest(IOwinContext context)
		{
			HeaderDecorator.GetInstance().Decorate();
			ItemDecorator.GetInstance().Decorate();
		}

		/// <summary>
		/// Post-Request processing
		/// </summary>
		/// <param name="context">type: IOwinContext</param>
		private void EndRequest(IOwinContext context)
		{
			if (HttpContext.Current == null)
			{
				return;
			}

			var startTime = ItemDecorator.GetInspectorInstance()[ItemDecorator.RequestStartTime];
			if (startTime != null
				//// ignore non-user pings
				&& TelemetryState.IsTelemetryEnabledUserAgent()
				//// ignore requests to specific paths
				&& TelemetryState.IsTelemetryEnabledRequestPath()
				//// ignore requests to specific extensions
				&& TelemetryState.IsTelemetryEnabledRequestExtension()
				//// make sure the portal is configured
				&& this.IsPortalConfigured())
			{
				var requestStartTime = (DateTime)startTime;
				var elapsedTime = DateTime.UtcNow - requestStartTime;

				var ag = PerformanceAggregateLogger.GetPerformanceAggregate();
				if (ag != null)
				{
					PerformanceEventSource.Log.PerformanceAggregate(ag, elapsedTime);
				}
			}
		}
	}
}
