/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	using System;
	using System.Threading;
	using Microsoft.Crm.Sdk.Messages;
	
	using Azure.Messaging.ServiceBus;
	using Azure.Messaging.ServiceBus.Administration;
	using Adxstudio.Xrm.AspNet;
	using Adxstudio.Xrm.Web;

	/// <summary>
	/// The Event Hub context.
	/// </summary>
	public class EventHubJobManager : IDisposable
	{
		/// <summary>
		/// The settings.
		/// </summary>
		public EventHubJobSettings Settings { get; private set; }

		/// <summary>
		/// The organization Id field.
		/// </summary>
		private readonly Lazy<Guid> organizationId;

		/// <summary>
		/// The organization Id.
		/// </summary>
		public Guid OrganizationId
		{
			get
			{
				try
				{
					return this.organizationId.Value;
				}
				catch (Exception e)
				{
					WebEventSource.Log.GenericErrorException(e);

					return Guid.Empty;
				}
			}
		}

		/// <summary>
		/// The namespace manager field.
		/// </summary>
		private Lazy<ServiceBusAdministrationClient> namespaceManager;

		/// <summary>
		/// The namespace manager.
		/// </summary>
		public ServiceBusAdministrationClient ServiceBusAdministrationClient
		{
			get
			{
				try
				{
					return this.namespaceManager.Value;
				}
				catch (Exception e)
				{
					WebEventSource.Log.GenericErrorException(e);

					return null;
				}
			}
		}

		/// <summary>
		/// The topic exists field.
		/// </summary>
		private Lazy<bool> topicExists;

		/// <summary>
		/// The topic exists flag.
		/// </summary>
		public bool TopicExists
		{
			get
			{
				try
				{
					return this.topicExists.Value;
				}
				catch (Exception e)
				{
					WebEventSource.Log.GenericErrorException(e);

					return false;
				}
			}
		}

		/// <summary>
		/// The subscription field.
		/// </summary>
		private Lazy<SubscriptionProperties> subscription;

		/// <summary>
		/// The subscription.
		/// </summary>
		public SubscriptionProperties Subscription
		{
			get
			{
				try
				{
					return this.subscription.Value;
				}
				catch (Exception e)
				{
					WebEventSource.Log.GenericErrorException(e);

					return null;
				}
			}
		}

		/// <summary>
		/// The subscription client field.
		/// </summary>
		private Lazy<ServiceBusReceiver> subscriptionClient;
		private Lazy<ServiceBusClient> client;

		/// <summary>
		/// The subscription client.
		/// </summary>
		public ServiceBusReceiver ServiceBusReceiver
		{
			get
			{
				try
				{
					return this.subscriptionClient.Value;
				}
				catch (Exception e)
				{
					WebEventSource.Log.GenericErrorException(e);

					return null;
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EventHubJobManager" /> class.
		/// </summary>
		/// <param name="context">The organization service context.</param>
		/// <param name="settings">The settings.</param>
		public EventHubJobManager(CrmDbContext context, EventHubJobSettings settings)
		{
			this.Settings = settings;

			this.organizationId = new Lazy<Guid>(() => GetOrganizationId(context), LazyThreadSafetyMode.PublicationOnly);
			this.Reset();
		}

		/// <summary>
		/// Resets the properties.
		/// </summary>
		public void Reset()
		{
			DisposeClients();
			// PublicationOnly does not cache a failed initialization, so a transient Service Bus error
			// is retried on the next access instead of disabling invalidation for the process lifetime.
			this.client = new Lazy<ServiceBusClient>(() => new ServiceBusClient(this.Settings.ConnectionString), LazyThreadSafetyMode.PublicationOnly);
			this.namespaceManager = new Lazy<ServiceBusAdministrationClient>(CreateServiceBusAdministrationClient(this.Settings), LazyThreadSafetyMode.PublicationOnly);
			this.topicExists = new Lazy<bool>(() => this.GetTopicExists(this.Settings), LazyThreadSafetyMode.PublicationOnly);
			this.subscription = new Lazy<SubscriptionProperties>(() => this.GetSubscription(this.Settings), LazyThreadSafetyMode.PublicationOnly);
			this.subscriptionClient = new Lazy<ServiceBusReceiver>(() => this.GetServiceBusReceiver(this.Settings), LazyThreadSafetyMode.PublicationOnly);
		}

		/// <summary>
		/// Retrieves the organization Id.
		/// </summary>
		/// <param name="context">The organization service context.</param>
		/// <returns>The organization Id.</returns>
		private static Guid GetOrganizationId(CrmDbContext context)
		{
			var response = context.Service.Execute(new WhoAmIRequest()) as WhoAmIResponse;
			return response.OrganizationId;
		}

		/// <summary>
		/// Initializes the namespace manager field.
		/// </summary>
		/// <param name="settings">The settings.</param>
		/// <returns>The namespace manager.</returns>
		private static Func<ServiceBusAdministrationClient> CreateServiceBusAdministrationClient(EventHubJobSettings settings)
		{
			return () => new ServiceBusAdministrationClient(settings.ConnectionString);
		}

		/// <summary>
		/// Initializes the topic exists field.
		/// </summary>
		/// <param name="settings">The settings.</param>
		/// <returns>The topic exists flag.</returns>
		private bool GetTopicExists(EventHubJobSettings settings)
		{
			var topicPath = settings.Subscription.TopicName;
			var exists = this.ServiceBusAdministrationClient.TopicExistsAsync(topicPath).ConfigureAwait(false).GetAwaiter().GetResult().Value;

			if (!exists)
			{
				throw new InvalidOperationException(string.Format("The topic '{0}' does not exist.", topicPath));
			}

			return true;
		}

		/// <summary>
		/// Initializes the subscription field.
		/// </summary>
		/// <param name="settings">The settings.</param>
		/// <returns>The subscription.</returns>
		private SubscriptionProperties GetSubscription(EventHubJobSettings settings)
		{
			var topicPath = settings.Subscription.TopicName;
			var subscriptionName = settings.Subscription.SubscriptionName;

			if (!this.TopicExists)
			{
				throw new InvalidOperationException(string.Format("The topic '{0}' does not exist.", topicPath));
			}

			var subscriptionExists = this.ServiceBusAdministrationClient.SubscriptionExistsAsync(topicPath, subscriptionName).ConfigureAwait(false).GetAwaiter().GetResult().Value;

			if (!subscriptionExists)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Creating Subscription '{0}' for topic '{1}'.", subscriptionName, topicPath));

				return this.CreateSubscription(settings.Subscription);
			}

			if (settings.RecreateSubscription)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Deleting Subscription '{0}' for topic '{1}'.", subscriptionName, topicPath));

				this.ServiceBusAdministrationClient.DeleteSubscriptionAsync(topicPath, subscriptionName).ConfigureAwait(false).GetAwaiter().GetResult();

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Creating Subscription '{0}' for topic '{1}'.", subscriptionName, topicPath));

				return this.CreateSubscription(settings.Subscription);
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Using Subscription '{0}' for topic '{1}'.", subscriptionName, topicPath));

			return this.ServiceBusAdministrationClient.GetSubscriptionAsync(topicPath, subscriptionName).ConfigureAwait(false).GetAwaiter().GetResult().Value;
		}

		/// <summary>
		/// Creates the subscription.
		/// </summary>
		/// <param name="description">The subscription description.</param>
		/// <returns>The subscription.</returns>
		private SubscriptionProperties CreateSubscription(CreateSubscriptionOptions description)
		{
            try
            {
                return this.ServiceBusAdministrationClient.CreateSubscriptionAsync(description, new CreateRuleOptions("$Default", this.CreateFilter())).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            }
            catch (Azure.RequestFailedException e) when (e.Status == 409)
            {
                WebEventSource.Log.GenericWarningException(e, string.Format("MessagingEntityAlreadyExistsException: Using Subscription '{0}' for topic '{1}'.", description.SubscriptionName, description.TopicName));
                return this.ServiceBusAdministrationClient.GetSubscriptionAsync(description.TopicName, description.SubscriptionName).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            }
        }

        /// <summary>
        /// Creates the filter.
        /// </summary>
        /// <returns>The filter.</returns>
        private RuleFilter CreateFilter()
		{
			return new SqlRuleFilter(string.Format("OrganizationId = '{0}'", this.OrganizationId));
		}

		/// <summary>
		/// Creates the subscription client.
		/// </summary>
		/// <param name="settings">The settings.</param>
		/// <returns>The subscription client.</returns>
		private ServiceBusReceiver GetServiceBusReceiver(EventHubJobSettings settings)
		{
			if (this.Subscription != null)
			{
				var topicPath = this.Subscription.TopicName;
				var subscriptionName = this.Subscription.SubscriptionName;
				return this.client.Value.CreateReceiver(topicPath, subscriptionName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
			}

			throw new InvalidOperationException(string.Format("The subscription '{0}' for topic '{1}' is not ready.", settings.Subscription.SubscriptionName, settings.Subscription.TopicName));
		}

		private void DisposeClients()
		{
			try
			{
				if (this.subscriptionClient != null && this.subscriptionClient.IsValueCreated)
					this.subscriptionClient.Value.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
			}
			finally
			{
				if (this.client != null && this.client.IsValueCreated)
					this.client.Value.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
			}
		}

		/// <summary>
		/// Internal use only.
		/// </summary>
		void IDisposable.Dispose()
		{
			DisposeClients();
		}
	}
}
