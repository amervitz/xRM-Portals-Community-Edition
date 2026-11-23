/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Polly;
using Microsoft.Xrm.Client.Diagnostics;

namespace Adxstudio.Xrm.ServiceModel
{
	/// <summary>
	/// Provides singleton access to a <see cref="ServiceHost"/>.
	/// </summary>
	public abstract class ServiceHostContext
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		protected ServiceHostContext()
		{
		}

		/// <summary>
		/// Initializes a <see cref="ResiliencePipeline"/> for transient fault handling.
		/// </summary>
		protected ServiceHostContext(ResiliencePipeline retryPolicy)
		{
			_retryPolicy = retryPolicy;
		}

		private readonly ResiliencePipeline _retryPolicy;
		private Lazy<ServiceHost> _host;

		private ServiceHost OpenServiceHost(bool useSynchronizationContext)
		{
			// in order to open the host during HttpApplication.Application_BeginRequest, the service needs to set UseSynchronizationContext = false

			var host = CreateServiceHost();
			var behavior = host.Description.Behaviors.Find<ServiceBehaviorAttribute>();
			behavior.UseSynchronizationContext = useSynchronizationContext;

			host.Opened += (s, a) => ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Opened");
			host.Closed += (s, a) => ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Closed");
			host.Faulted += (s, a) => ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Faulted");

			if (_retryPolicy != null)
			{
				_retryPolicy.Execute(host.Open);
			}
			else
			{
				host.Open();
			}

			return host;
		}

		/// <summary>
		/// Generates the <see cref="ServiceHost"/> based on a <see cref="ServiceHostFactory"/>.
		/// </summary>
		/// <returns></returns>
		protected abstract ServiceHost CreateServiceHost();

		/// <summary>
		/// Opens the global persistent <see cref="ServiceHost"/> connection for the application.
		/// </summary>
		public void Open(bool useSynchronizationContext = false)
		{
			if (_host != null && _host.Value != null && _host.Value.State == CommunicationState.Faulted)
			{
                ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}={1}", _host.Value, _host.Value.State));

				_host.Value.Abort();
				_host = null;
			}

			if (_host == null)
			{
				_host = new Lazy<ServiceHost>(() => OpenServiceHost(useSynchronizationContext));
			}

			var host = _host.Value;

			if (host != null)
			{
                ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}={1}", host, host.State));
			}
		}

		/// <summary>
		/// Closes the global persistent <see cref="ServiceHost"/> connection for the application.
		/// </summary>
		public void Close()
		{
			if (_host != null && _host.Value != null)
			{
				if (_host.Value.State == CommunicationState.Faulted)
					_host.Value.Abort();
				else
					_host.Value.Close();
			}
		}
	}
}
