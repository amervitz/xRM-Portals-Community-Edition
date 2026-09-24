/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using Azure.Messaging.ServiceBus;

	/// <summary>
	/// CrmSubscriptionMessage wrapping a ServiceBusReceivedMessage indicating a CRM Metadata change
	/// </summary>
	public sealed class MetadataMessage : CrmSubscriptionMessage
	{
		/// <summary>
		/// Deserialize the ServiceBusReceivedMessage message body into a MetadataMessage
		/// </summary>
		/// <param name="message">ServiceBusReceivedMessage message body</param>
		/// <param name="brokeredMessage">ServiceBusReceivedMessage message</param>
		/// <returns>ICrmSubscriptionMessage</returns>
		internal static ICrmSubscriptionMessage DeserializeMessage(string message, ServiceBusReceivedMessage brokeredMessage)
		{
			MetadataMessage metadataMessage = (MetadataMessage)CrmSubscriptionMessage.DeserializeMessage(message, typeof(MetadataMessage));

			if (metadataMessage != null && metadataMessage.ValidMessage)
			{
				metadataMessage.AppendProperties(brokeredMessage);
				return metadataMessage;
			}

			return null;
		}
	}
}
