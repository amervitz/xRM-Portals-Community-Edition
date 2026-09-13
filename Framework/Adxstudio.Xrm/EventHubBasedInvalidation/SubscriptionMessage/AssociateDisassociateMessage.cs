/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using Azure.Messaging.ServiceBus;
	using Azure.Messaging.ServiceBus.Administration;

	/// <summary>
	/// CrmSubscriptionMessage wrapping a ServiceBusReceivedMessage indicating a CRM Associate Disassociate record change
	/// </summary>
	public sealed class AssociateDisassociateMessage : EntityRecordMessage
	{
		/// <summary>
		/// Relationship Name for the relationship
		/// </summary>
		public string RelationshipName { get; set; }

		/// <summary>
		/// Related Entity Name
		/// 	Entity logical name for the associated entity
		/// </summary>
		public string RelatedEntity1Name { get; set; }

		/// <summary>
		/// Related Entity Name
		/// 	Entity logical name for the associated entity
		/// </summary>
		public string RelatedEntity2Name { get; set; }

		/// <summary>
		/// Deserialize the ServiceBusReceivedMessage message body into a EntityRecordMessage
		/// </summary>
		/// <param name="message">ServiceBusReceivedMessage message body</param>
		/// <param name="brokeredMessage">ServiceBusReceivedMessage message</param>
		/// <returns>ICrmSubscriptionMessage</returns>
		internal new static ICrmSubscriptionMessage DeserializeMessage(string message, ServiceBusReceivedMessage brokeredMessage)
		{
			AssociateDisassociateMessage entityRecordMessage = (AssociateDisassociateMessage)CrmSubscriptionMessage.DeserializeMessage(message, typeof(AssociateDisassociateMessage));

			if (entityRecordMessage != null && entityRecordMessage.ValidMessage)
			{
				entityRecordMessage.AppendProperties(brokeredMessage);
				return entityRecordMessage;
			}

			return null;
		}

		/// <summary>
		/// Returns true if the properties are all present, otherwise false
		/// </summary>
		protected override bool ValidMessage
		{
			get
			{
				return !string.IsNullOrEmpty(this.RelationshipName)
					&& !string.IsNullOrEmpty(this.RelatedEntity1Name)
					&& !string.IsNullOrEmpty(this.RelatedEntity2Name)
					&& base.ValidMessage;
			}
		}
	}
}
