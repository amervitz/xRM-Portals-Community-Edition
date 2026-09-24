/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using System.Collections.Generic;
	using Azure.Messaging.ServiceBus;
	using Adxstudio.Xrm.Cms;
	using Newtonsoft.Json;

	/// <summary>
	/// Factory class to generate ICrmSubscriptionMessages
	/// </summary>
	internal sealed class CrmSubscriptionMessageFactory
	{
		/// <summary>
		/// Factory method that generates an ICrmSubscriptionMessage
		/// </summary>
		/// <param name="message">ServiceBusReceivedMessage</param>
		/// <returns>ICrmSubscriptionMessage</returns>
		public static ICrmSubscriptionMessage Create(ServiceBusReceivedMessage message)
		{
			if (message == null)
				return null;

			string messageBody = ReadMessageBody(message);

			if (messageBody == null)
				return null;
			
			ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Message Body for Subscription message is {0} ", messageBody.ToString()));

			ICrmSubscriptionMessage subscriptionMessage = CrmSubscriptionMessageFactory.Create(messageBody, message);

			if (subscriptionMessage != null)
			{
				CmsEventSource.Log.LatencyInfo(subscriptionMessage);
			}

			return subscriptionMessage;
		}

		private static string ReadMessageBody(ServiceBusReceivedMessage message)
		{
			// Existing CRM publishers use the legacy SDK's binary XML string serialization.
			var bytes = message.Body.ToArray();
			try
			{
				var quotas = new System.Xml.XmlDictionaryReaderQuotas
				{
					MaxDepth = 16,
					MaxStringContentLength = 4 * 1024 * 1024,
				};
				using (var reader = System.Xml.XmlDictionaryReader.CreateBinaryReader(bytes, quotas))
				{
					return (string)new System.Runtime.Serialization.DataContractSerializer(typeof(string)).ReadObject(reader);
				}
			}
			// Publishers that send the body as plain text are not binary XML and fail to deserialize;
			// their body is already the string, so decode it rather than discarding the message.
			catch (System.Xml.XmlException) { return message.Body.ToString(); }
			catch (System.Runtime.Serialization.SerializationException) { return message.Body.ToString(); }
		}

		private static ICrmSubscriptionMessage Create(string messageBody, ServiceBusReceivedMessage message)
		{
			Dictionary<string, string> jsonDictionary;

			// A body that is neither binary XML nor JSON reaches here as whatever text it decoded to.
			// Report it the way an unexpected format is reported below rather than letting the parse
			// failure escape to the subscription's job, which would abandon the rest of the batch.
			try
			{
				jsonDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(messageBody);
			}
			catch (JsonException e)
			{
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Unreadable message body. MessageId: {0}: {1} ", message.MessageId, e.Message));
				return null;
			}

			if (jsonDictionary == null || !jsonDictionary.ContainsKey("MessageName"))
			{
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Unexpected message format. MessageId: {0} ", message.MessageId));
				return null;
			}

			switch (jsonDictionary["MessageName"])
			{
				case "MetadataChange":
					NotificationUpdateManager.Instance.MetadataDirty = true;
					return MetadataMessage.DeserializeMessage(messageBody, message);
				case "Create":
				case "Update":
				case "Delete":
					return EntityRecordMessage.DeserializeMessage(messageBody, message);
				case "AssociateEntities":
				case "DisassociateEntities":
					return AssociateDisassociateMessage.DeserializeMessage(messageBody, message);
				default:
					ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Unexpected message type: {0} ", jsonDictionary["MessageName"]));
					return null;
			}
		}
	}
}
