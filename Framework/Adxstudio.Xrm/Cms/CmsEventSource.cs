/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Cms
{
	using System;
	using System.Diagnostics.Tracing;
	using System.Globalization;
	using System.Threading;
	using Adxstudio.Xrm.AspNet.Cms;
	using Adxstudio.Xrm.Core.Telemetry.EventSources;
	using Adxstudio.Xrm.EventHubBasedInvalidation;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Sdk;

	[EventSource(Guid = "88E12386-900A-401D-92BA-CAFAF3FC6AEF", Name = InternalName)]
	internal sealed class CmsEventSource : EventSourceBase
	{
		/// <summary>
		/// The EventSource name.
		/// </summary>
		private const string InternalName = "PortalCms";

		private static readonly Lazy<CmsEventSource> _instance = new Lazy<CmsEventSource>();

		public static CmsEventSource Log
		{
			get { return _instance.Value; }
		}

		private enum EventName
		{
			/// <summary>
			/// A matching portal website binding has not been found.
			/// </summary>
			WebsiteBindingNotFound = 1,

			/// <summary>
			/// The required "Home" site marker for the current portal website has not been found.
			/// </summary>
			HomeSiteMarkerNotFound = 2,

			/// <summary>
			/// Content map read or write lock acquisition has failed.
			/// </summary>
			ContentMapLockTimeout = 3,

			/// <summary>
			/// Attempt to access attribute values from a content map reference node.
			/// </summary>
			/// <remarks>
			/// This is usually evidence of a content map corruption, which is usually evidence of
			/// a content map load/update issue.
			/// </remarks>
			ContentMapReferenceNodeAccess = 4,

			/// <summary>
			/// The status of the content map read or write lock.
			/// </summary>
			ContentMapLockStatus = 5,

			/// <summary>
			/// Event to log webhook notification received for scale out event.
			/// </summary>
			ScaleOutNotification = 6,

			/// <summary>
			/// Logs the latency information associated with the messages
			/// </summary>
			LatencyInfo = 7,
		}

		/// <summary>
		/// Log that content map read or write lock acquisition has failed.
		/// </summary>
		[NonEvent]
		public void ContentMapLockTimeout(ContentMapLockType lockType, ReaderWriterLockSlim contentMapLock)
		{
			ContentMapLockTimeout(
				lockType.ToString(),
				contentMapLock.IsReadLockHeld,
				contentMapLock.IsUpgradeableReadLockHeld,
				contentMapLock.IsWriteLockHeld,
				contentMapLock.CurrentReadCount,
				contentMapLock.RecursiveReadCount,
				contentMapLock.RecursiveUpgradeCount,
				contentMapLock.RecursiveWriteCount,
				contentMapLock.WaitingReadCount,
				contentMapLock.WaitingUpgradeCount,
				contentMapLock.WaitingWriteCount,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		/// <summary>
		/// Log an attempt to access attribute values from a content map reference node.
		/// </summary>
		/// <remarks>
		/// This is usually evidence of a content map corruption, which is usually evidence of
		/// a content map load/update issue.
		/// </remarks>
		[Event((int)EventName.ContentMapLockTimeout, Message = "Lock Type : {0} Is Read Lock Held : {1} Is Upgradeble Lock Held : {2} Is Write Lock Held : {3} Current Read Count : {4} Recursive Read Count : {5} Recursive Upgrade Count : {6} Recursive Write Count : {7} Waiting Read Count : {8} Waiting Upgrade Count : {9} Waiting Write Count : {10} PortalUrl : {11} PortalVersion : {12} PortalProductionOrTrial : {13}  SessionId : {14} ElapsedTime : {15}", Level = EventLevel.Error, Version = 3)]
		private void ContentMapLockTimeout(string lockType, bool isReadLockHeld, bool isUpgradeableReadLockHeld, bool isWriteLockHeld, int currentReadCount, int recursiveReadCount, int recursiveUpgradeCount, int recursiveWriteCount, int waitingReadCount, int waitingUpgradeCount, int waitingWriteCount, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(
				EventName.ContentMapLockTimeout,
				lockType ?? string.Empty,
				isReadLockHeld,
				isUpgradeableReadLockHeld,
				isWriteLockHeld,
				currentReadCount,
				recursiveReadCount,
				recursiveUpgradeCount,
				recursiveWriteCount,
				waitingReadCount,
				waitingUpgradeCount,
				waitingWriteCount,
				portalUrl,
				portalVersion,
				portalProductionOrTrialType,
				sessionId,
				elapsedTime);
		}

		/// <summary>
		/// Log the status of the content map read or write lock.
		/// </summary>
		[NonEvent]
		public void ContentMapLockStatus(ContentMapLockType lockType, string status, long duration)
		{
			ContentMapLockStatus(
				lockType.ToString(),
				status,
				duration,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		/// <summary>
		/// Log the status of the content map read or write lock.
		/// </summary>
		[Event((int)EventName.ContentMapLockStatus, Message = "Lock Type : {0} Status : {1} Duration : {2} PortalUrl : {3} PortalVersion : {4} PortalProductionOrTrial : {5}  SessionId : {6} ElapsedTime : {7}", Level = EventLevel.Informational, Version = 3)]
		private void ContentMapLockStatus(string lockType, string status, long duration, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(
				EventName.ContentMapLockStatus,
				lockType ?? string.Empty,
				status ?? string.Empty,
				duration,
				portalUrl,
				portalVersion,
				portalProductionOrTrialType,
				sessionId,
				elapsedTime);
		}

		[NonEvent]
		public void ContentMapReferenceNodeAccess(EntityReference reference, string attributeLogicalName)
		{
			if (reference == null)
			{
				return;
			}

			ContentMapReferenceNodeAccess(
				reference.LogicalName,
				reference.Id,
				attributeLogicalName,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		[Event((int)EventName.ContentMapReferenceNodeAccess, Message = "Reference Entity Name : {0} Entity Id : {1} Attribute Name : {2} PortalUrl : {3} PortalVersion : {4} PortalProductionOrTrial : {5}  SessionId : {6} ElapsedTime : {7}", Level = EventLevel.Error, Version = 3)]
		private void ContentMapReferenceNodeAccess(string logicalName, Guid id, string attributeLogicalName, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(EventName.ContentMapReferenceNodeAccess, logicalName ?? string.Empty, id, attributeLogicalName ?? string.Empty, portalUrl, portalVersion, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		/// <summary>
		/// Log that the required "Home" site marker for the current portal website has not been found.
		/// </summary>
		[NonEvent]
		public void HomeSiteMarkerNotFound(WebsiteNode website)
		{
			if (website == null)
			{
				return;
			}

			HomeSiteMarkerNotFound(
				website.Name,
				website.Id,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		[Event((int)EventName.HomeSiteMarkerNotFound, Message = "Website Name : {0} Website Id : {1} PortalUrl : {2} PortalVersion : {3} PortalProductionOrTrial : {4} SessionId : {5} ElapsedTime : {6}", Level = EventLevel.Critical, Version = 3)]
		private void HomeSiteMarkerNotFound(string websiteName, Guid websiteId, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(EventName.HomeSiteMarkerNotFound, websiteName ?? string.Empty, websiteId, portalUrl, portalVersion, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		/// <summary>
		/// Log that a matching portal website binding has not been found.
		/// </summary>
		[NonEvent]
		public void WebsiteBindingNotFoundByHostingEnvironment(PortalHostingEnvironment environment)
		{
			if (environment == null)
			{
				return;
			}

			WebsiteBindingNotFound(
				@"HostingEnvironment(SiteName=""{0}"" ApplicationVirtualPath=""{1}""). Current website binding is not present in database.".FormatWith(environment.SiteName, environment.ApplicationVirtualPath),
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		/// <summary>
		/// Log that a matching portal website binding has not been found.
		/// </summary>
		[NonEvent]
		public void WebsiteBindingNotFoundByWebsiteName(string websiteName)
		{
			WebsiteBindingNotFound(
				@"WebsiteName=""{0}""".FormatWith(websiteName),
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		[Event((int)EventName.WebsiteBindingNotFound, Message = "Website Name : {0} PortalUrl : {1} PortalVersion : {2} PortalProductionOrTrial : {3} SessionId : {4} ElapsedTime :5}", Level = EventLevel.Critical, Version = 3)]
		private void WebsiteBindingNotFound(string message, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(EventName.WebsiteBindingNotFound, message ?? string.Empty, portalUrl, portalVersion, portalProductionOrTrialType, sessionId, elapsedTime);
		}

		[NonEvent]
		public void ScaleOutNotification(string status, string operation, string notificationTimeStamp, string details, string oldCapacity, string newCapacity, string resourceId)
		{
			ScaleOutNotification(
				status ?? string.Empty,
				operation ?? string.Empty,
				notificationTimeStamp ?? string.Empty,
				details ?? string.Empty,
				oldCapacity ?? string.Empty,
				newCapacity ?? string.Empty,
				resourceId ?? string.Empty,
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial,
				this.SessionId,
				this.ElapsedTime());
		}

		[Event((int)EventName.ScaleOutNotification, Message = "Status : {0} Operation : {1} NotificationTimeStamp : {2} Details : {3} OldCapacity : {4} NewCapacity : {5} ResourceId : {6} PortalUrl : {7} PortalVersion : {8} PortalProductionOrTrial : {9} SessionId : {10} ElapsedTime : {11}", Level = EventLevel.Critical, Version = 3)]
		private void ScaleOutNotification(string status, string operation, string notificationTimeStamp, string details, string oldCapacity, string newCapacity, string resourceId, string portalUrl, string portalVersion, string portalProductionOrTrialType, string sessionId, string elapsedTime)
		{
			WriteEvent(
				EventName.ScaleOutNotification,
				status ?? string.Empty,
				operation ?? string.Empty,
				notificationTimeStamp ?? string.Empty,
				details ?? string.Empty,
				oldCapacity ?? string.Empty,
				newCapacity ?? string.Empty,
				resourceId ?? string.Empty,
				portalUrl,
				portalVersion,
				portalProductionOrTrialType,
				sessionId,
				elapsedTime);
		}

		/// <summary>
		/// Logs Latency information given the subscriptionMessage
		/// </summary>
		/// <param name="subscriptionMessage">The <see cref="Exception"/> thrown.</param>
		/// <returns>Unique ID of event</returns>
		[NonEvent]
		public string LatencyInfo(ICrmSubscriptionMessage subscriptionMessage)
		{
			DateTime enqueueEventHubUtc = subscriptionMessage.EnqueuedEventhubTimeUtc;
			DateTime dequeueEventHubUtc = subscriptionMessage.DequeuedEventhubTimeUtc;
			TimeSpan eventHubLatency = dequeueEventHubUtc - enqueueEventHubUtc;

			DateTime enqueueTopicUtc = subscriptionMessage.EnqueuedTopicTimeUtc;
			DateTime dequeueTopicUtc = subscriptionMessage.DequeuedTopicTimeUtc;
			TimeSpan topicLatency = dequeueTopicUtc - enqueueTopicUtc;

			WriteEventLatencyInfo(
				subscriptionMessage.MessageId.ToString("D"),
				enqueueEventHubUtc.ToString(CultureInfo.InvariantCulture),
				dequeueEventHubUtc.ToString(CultureInfo.InvariantCulture),
				enqueueTopicUtc.ToString(CultureInfo.InvariantCulture),
				dequeueTopicUtc.ToString(CultureInfo.InvariantCulture),
				eventHubLatency.TotalSeconds.ToString(CultureInfo.InvariantCulture),
				topicLatency.TotalSeconds.ToString(CultureInfo.InvariantCulture),
				(topicLatency.TotalSeconds + eventHubLatency.TotalSeconds).ToString(CultureInfo.InvariantCulture),
				this.PortalUrl,
				this.PortalVersion,
				this.ProductionOrTrial);

			return GetActivityId();
		}

		[Event((int)EventName.LatencyInfo, Message = "MessageId={0} enqueueEventHubUtc={1} dequeueEventHubUtc={2} enqueueTopicUtc={3} dequeueTopicUtc={4} eventhubLatency={5} topicLatency={6} totalLatency={7} portalUrl={8} portalVersion={9} portalProductionOrTrialType={10}", Level = EventLevel.Informational, Version = 1)]
		private void WriteEventLatencyInfo(string messageId, string enqueueEventHubUtc, string dequeueEventHubUtc, string enqueueTopicUtc, string dequeueTopicUtc, string eventhubLatency, string topicLatency, string totalLatency, string portalUrl, string portalVersion, string portalProductionOrTrialType)
		{
			WriteEvent(EventName.LatencyInfo, messageId, enqueueEventHubUtc, dequeueEventHubUtc, enqueueTopicUtc, dequeueTopicUtc, eventhubLatency, topicLatency, totalLatency, portalUrl, portalVersion, portalProductionOrTrialType);
		}
	}
}
