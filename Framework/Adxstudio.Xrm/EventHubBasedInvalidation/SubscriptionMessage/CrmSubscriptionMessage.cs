/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using System;
	using Azure.Messaging.ServiceBus;
	using Azure.Messaging.ServiceBus.Administration;
	using Newtonsoft.Json;

	/// <summary>
	/// Class used for deserializing and storing message information from an Azure Topic subscription
	/// </summary>
	public abstract class CrmSubscriptionMessage : ICrmSubscriptionMessage
	{
		private MessageType messageType;

		public CrmSubscriptionMessage()
		{
			this.Received = DateTime.UtcNow;
		}

		/// <summary>
		/// Deserializes the JSON message from a ServiceBusReceivedMessage
		/// </summary>
		/// <param name="messageBody">ServiceBusReceivedMessage message body to deserialize</param>
		/// <param name="t">Type to deserialize into</param>
		/// <returns>ICrmSubscriptionMessage initialized from the ServiceBusReceivedMessage message body</returns>
		protected static ICrmSubscriptionMessage DeserializeMessage(string messageBody, Type t)
		{
			try
			{
				var x = JsonConvert.DeserializeObject(messageBody, t) as ICrmSubscriptionMessage;
				return x;
			}
			catch (Exception e)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, e.ToString());
			}
			return null;
		}

		/// <summary>
		/// Populates this message from the properties of the brokered message
		/// </summary>
		/// <param name="message">ServiceBusReceivedMessage to pull properties from</param>
		protected void AppendProperties(ServiceBusReceivedMessage message)
		{
			this.EnqueuedTopicTimeUtc = this.TryGetDateTime(message, BrokeredMessageConstants.EnqueuedTopicTimeUtc);
			this.EnqueuedEventhubTimeUtc = this.TryGetDateTime(message, BrokeredMessageConstants.EnqueuedEventhubTimeUtc);
			this.DequeuedEventhubTimeUtc = this.TryGetDateTime(message, BrokeredMessageConstants.DequeuedEventhubTimeUtc);
			this.DequeuedTopicTimeUtc = this.Received;
		}

		/// <summary>
		/// Attempts to get the dateTime object out of the BrokeredMessageProperty
		/// </summary>
		/// <param name="message">message to pull the property from</param>
		/// <param name="property">Property to pull out of the message</param>
		/// <returns>DateTime object from the message property or default DateTime</returns>
		private DateTime TryGetDateTime(ServiceBusReceivedMessage message, string property)
		{
			if (message.ApplicationProperties.ContainsKey(property))
			{
				try
				{
					return (DateTime)message.ApplicationProperties[property];
				}
				catch
				{
				}
			}

			return default(DateTime);
		}

		public DateTime Received { get; }

		/// <summary>
		/// Time in UTC in which the message was enqueued in the Topic
		/// </summary>
		public DateTime EnqueuedTopicTimeUtc { get; private set; }

		/// <summary>
		/// Time in UTC in which the message was dequeued from the Topic
		/// </summary>
		public DateTime DequeuedTopicTimeUtc { get; private set; }

		/// <summary>
		/// Time in UTC in which the message was enqueued in the Eventhub
		/// </summary>
		public DateTime EnqueuedEventhubTimeUtc { get; private set; }

		/// <summary>
		/// Time in UTC in which the message was dequeued from the Eventhub
		/// </summary>
		public DateTime DequeuedEventhubTimeUtc { get; private set; }

		/// <summary>
		/// OrganizationId
		/// </summary>
		public Guid OrganizationId { get; set; }

		/// <summary>
		/// MessageId
		/// </summary>
		public Guid MessageId { get; set; }

		/// <summary>
		/// MessageName
		/// 	Create, Update, Delete, Associate, MetadataChange, Disassociate
		/// </summary>
		public string MessageName { get; set; }

		/// <summary>
		/// Returns true if the properties are all present, otherwise false
		/// </summary>
		protected virtual bool ValidMessage
		{
			get
			{
				return this.OrganizationId != default(Guid)
						&& !string.IsNullOrEmpty(this.MessageName);
			}
		}

		/// <summary>
		/// Returns the message type
		/// </summary>
		public MessageType MessageType
		{
			get
			{
				if (this.messageType == MessageType.Unknown)
					if (!Enum.TryParse(this.MessageName, true, out messageType))
						this.messageType = MessageType.Other;

				return messageType;
			}
		}
	}
}
