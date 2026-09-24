/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using System;
	using System.Linq;
	using Adxstudio.Xrm.Threading;

	/// <summary>
	/// A continuous job for consuming the Service Bus topic.
	/// </summary>
	public class EventHubJob : FluentSchedulerJob
	{
		/// <summary>
		/// The mutex lock.
		/// </summary>
		private static readonly object JobLock = new object();

		/// <summary>
		/// The Event Hub manager.
		/// </summary>
		public EventHubJobManager Manager { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="EventHubJob" /> class.
		/// </summary>
		/// <param name="manager">The Event Hub manager.</param>
		public EventHubJob(EventHubJobManager manager)
		{
			this.Manager = manager;
		}

		/// <summary>
		/// The body.
		/// </summary>
		/// <param name="id">The activity id.</param>
		protected override void ExecuteInternal(Guid id)
		{
			bool isSearchSubscription = this.Manager.Settings.SubscriptionType == EventHubSubscriptionType.SearchSubscription;
			if (this.Manager.ServiceBusReceiver != null)
			{
				lock (JobLock)
				{
					try
					{
						ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Subscription = '{0}' Topic = '{1}'", this.Manager.Subscription.SubscriptionName, this.Manager.Subscription.TopicName));

						// take N at a time
						var messages = this.Manager.ServiceBusReceiver
							.ReceiveMessagesAsync(this.Manager.Settings.ReceiveBatchMessageCount,
								this.Manager.Settings.ReceiveBatchServerWaitTime > TimeSpan.Zero ? this.Manager.Settings.ReceiveBatchServerWaitTime : TimeSpan.FromMilliseconds(100))
							.ConfigureAwait(false).GetAwaiter().GetResult()
							.Where(message => message != null);

						foreach (var message in messages)
						{
							// Settle the message before processing it. Cache invalidation messages are not
							// worth redelivering: a message that fails to process will fail again, and with
							// the subscription's MaxDeliveryCount it would block the queue behind it.
							this.Manager.ServiceBusReceiver.CompleteMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();

							var crmSubscriptionMessage = CrmSubscriptionMessageFactory.Create(message);

							if (crmSubscriptionMessage != null)
							{
								NotificationUpdateManager.Instance.UpdateNotificationMessageTable(crmSubscriptionMessage, isSearchSubscription);
							}
						}
					}
					catch (Exception e)
					{
						this.Manager.Reset();
						ADXTrace.Instance.TraceInfo(TraceCategory.Application, e.ToString());
					}
				}
			}
		}
	}
}
