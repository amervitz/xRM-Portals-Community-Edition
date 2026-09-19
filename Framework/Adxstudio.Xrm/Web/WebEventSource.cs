/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Tracing;
	using System.Text;
	using Adxstudio.Xrm.Configuration;
	using Adxstudio.Xrm.Core.Telemetry.EventSources;
	using Adxstudio.Xrm.Diagnostics.Trace;

	[EventSource(Guid = "14318B53-CEB4-420D-A9F9-78CA594C751B", Name = InternalName)]
	public sealed class WebEventSource : EventSourceBase
	{
		/// <summary>
		/// The EventSource name.
		/// </summary>
		private const string InternalName = "PortalWeb";

		/// <summary>
		/// The internal TraceSource.
		/// </summary>
		private static readonly TraceSource InternalTrace = new TraceSource(InternalName);

		/// <summary>
		/// The variable for lazy initialization of <see cref="WebEventSource"/>.
		/// </summary>
		private static readonly Lazy<WebEventSource> _instance = new Lazy<WebEventSource>();

		public static WebEventSource Log
		{
			get { return _instance.Value; }
		}

		private enum EventName
		{
			/// <summary>
			/// An unhandled ASP.NET application exception.
			/// </summary>
			UnhandledException = 1,

			/// <summary>
			/// A generic error exception.
			/// </summary>
			GenericException = 2,

			/// <summary>
			/// A generic warning exception.
			/// </summary>
			GenericWarningException = 3,

			/// <summary>
			/// Application LifeCycle event.
			/// </summary>
			ApplicationLifecycle = 4,

		}

		/// <summary>
		/// Log a generic error exception.
		/// </summary>
		/// <param name="exception">exception.</param>
		/// <param name="eventData">Optional additional event data</param>
		/// <returns>Unique ID of event</returns>
		[NonEvent]
		public string GenericErrorException(Exception exception, string eventData = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
		{
			this.WriteEventGenericErrorException(
				Serialize(exception) ?? string.Empty,
				eventData ?? string.Empty,
				this.PortalVersion,
				memberName,
				sourceFilePath,
				sourceLineNumber,
				this.PortalUrl,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());

			return this.GetActivityId();
		}

		[Event((int)EventName.GenericException, Message = "Exception Message : {0} Event Data : {1} Portal Version : {2} Member Name : {3} Source File Path : {4} Source Line Number : {5} PortalURL : {6} PortalProductionOrTrial : {7} SessionId:{8} ElapsedTime:{9}", Level = EventLevel.Error, Version = 4)]
		private void WriteEventGenericErrorException(string exceptionMessage, string optionalEventData, string portalVersion, string memberName, string sourceFilePath, int sourceLineNumber, string portalUrl, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			InternalTrace.TraceEvent(
				PortalSettings.Instance, TraceEventType.Error, (int)EventName.GenericException,
				"Exception Message : {0} Event Data : {1} Portal Version : {2} Member Name : {3} Source File Path : {4} Source Line Number : {5}",
				exceptionMessage, optionalEventData, portalVersion, memberName, sourceFilePath, sourceLineNumber);

			this.WriteEvent(EventName.GenericException, exceptionMessage, optionalEventData, portalVersion, memberName, sourceFilePath, sourceLineNumber, portalUrl, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		/// <summary>
		/// Log a generic warning exception.
		/// </summary>
		/// <param name="exception">exception.</param>
		/// <param name="eventData">Optional additional event data</param>
		/// <returns>Unique ID of event</returns>
		[NonEvent]
		public string GenericWarningException(Exception exception, string eventData = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
		{
			this.WriteEventGenericWarningException(
				Serialize(exception) ?? string.Empty,
				eventData ?? string.Empty,
				this.PortalVersion,
				memberName,
				sourceFilePath,
				sourceLineNumber,
				this.PortalUrl,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());

			return this.GetActivityId();
		}

		/// <summary>
		/// Capture application lifecycle events
		/// </summary>
		/// <param name="category">Type of lifecycle event emitted</param>
		/// <param name="message">Message associated with the lifecycle event emitted</param>
		[NonEvent]
		public void WriteApplicationLifecycleEvent(ApplicationLifecycleEventCategory category, string message = "")
		{
			this.WriteEventApplicationLifecycle(
				category,
				message,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		/// <summary>
		/// Emit application lifecycle event 
		/// </summary>
		/// <param name="category">Type of lifecycle event emitted</param>
		/// <param name="message">Message associated with the lifecycle event emitted</param>
		/// <param name="portalUrl">Url for the portal home page as listed in Azure</param>
		/// <param name="portalVersion">Version of the portal</param>
		/// <param name="portalProductionOrTrialType">Production or trial portal</param>
		/// <param name="sessionId">Web Session ID</param>
		/// <param name="elapsedTime">Amount of time elapsed in the request</param>
		[Event((int)EventName.ApplicationLifecycle, Message = "Lifecycle = {0} Message = {1} PortalURL = {2} PortalVersion = {3} PortalProductionOrTrial = {4} sessionId = {5}", Level = EventLevel.Informational, Version = 2)]
		private void WriteEventApplicationLifecycle(ApplicationLifecycleEventCategory category, string message, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			InternalTrace.TraceEvent(
				PortalSettings.Instance, TraceEventType.Information, (int)EventName.ApplicationLifecycle,
				"Lifecycle = {0} Message = {1} PortalURL = {2} PortalVersion = {3} PortalProductionOrTrial = {4} sessionId = {5} elapsedTime= {6}",
				category, message, portalUrl, portalVersion, portalProductionOrTrialType, sessionId, elapsedTime);

			this.WriteEvent(EventName.ApplicationLifecycle, category, message, portalUrl, portalVersion, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		[Event((int)EventName.GenericWarningException, Message = "Exception Message : {0} Event Data : {1} Portal Version : {2} Member Name : {3} Source File Path : {4} Source Line Number : {5} PortalURL : {6} PortalProductionOrTrial : {7} SessionId:{8} ElapsedTime:{9}", Level = EventLevel.Warning, Version = 3)]
		private void WriteEventGenericWarningException(string exceptionMessage, string optionalEventData, string portalVersion, string memberName, string sourceFilePath, int sourceLineNumber, string portalUrl, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			InternalTrace.TraceEvent(
				PortalSettings.Instance, TraceEventType.Warning, (int)EventName.GenericWarningException,
				"Exception Message : {0} Event Data : {1} Portal Version : {2} Member Name : {3} Source File Path : {4} Source Line Number : {5}",
				exceptionMessage, optionalEventData, portalVersion, memberName, sourceFilePath, sourceLineNumber);

			this.WriteEvent(EventName.GenericWarningException, exceptionMessage, optionalEventData, portalVersion, memberName, sourceFilePath, sourceLineNumber, portalUrl, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		/// <summary>
		/// Log an unhandled ASP.NET application exception.
		/// </summary>
		/// <param name="exception">The unhandled exception.</param>
		/// <param name="eventData">Optional additional event data</param>
		/// <returns>Unique ID of event</returns>
		[NonEvent]
		public string UnhandledException(Exception exception, string eventData = null)
		{
			this.WriteEventUnhandledException(
				Serialize(exception) ?? string.Empty,
				eventData ?? string.Empty,
				this.PortalVersion,
				this.PortalUrl,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());

			return this.GetActivityId();
		}

		[Event((int)EventName.UnhandledException, Message = "Exception Message : {0} Event Data : {1} Portal Version : {2} PortalURL : {3} PortalProductionOrTrial : {4} SessionId:{5} ElapsedTime:{6}", Level = EventLevel.Error, Version = 4)]
		private void WriteEventUnhandledException(string exceptionMessage, string optionalEventData, string portalVersion, string portalUrl, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			InternalTrace.TraceEvent(
				PortalSettings.Instance, TraceEventType.Error, (int)EventName.UnhandledException,
				"Exception Message : {0} Event Data : {1} Portal Version : {2}",
				exceptionMessage, optionalEventData, portalVersion);

			this.WriteEvent(EventName.UnhandledException, exceptionMessage, optionalEventData, portalVersion, portalUrl, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		/// <summary>
		/// Serializes an Exception.
		/// </summary>
		/// <param name="error">The exception.</param>
		/// <returns>The exception text.</returns>
		private static string Serialize(Exception error)
		{
			if (error == null) return null;

			var sb = new StringBuilder();

			var current = error;

			while (current != null)
			{
				var help = !string.IsNullOrWhiteSpace(current.HelpLink) ? Environment.NewLine + current.HelpLink : null;

				sb.Append(current.Message + Environment.NewLine);
				sb.Append(current.GetType().FullName + Environment.NewLine);
				sb.Append(current.StackTrace + Environment.NewLine + Environment.NewLine);
				sb.Append(current.Source);
				sb.Append(help + Environment.NewLine + Environment.NewLine);

				current = current.InnerException;
			}

			return sb.ToString();
		}
	}
}
