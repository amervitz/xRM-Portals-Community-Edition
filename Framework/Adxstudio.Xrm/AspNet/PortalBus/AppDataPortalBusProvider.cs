/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.PortalBus
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Threading.Tasks.Dataflow;
	using Adxstudio.Xrm.AspNet.Cms;
	using Adxstudio.Xrm.IO;
	using Adxstudio.Xrm.Web;
	using global::Owin;
	using Microsoft.Owin;
	using Microsoft.Owin.BuilderProperties;
	using Polly;
	using Adxstudio.Xrm.Threading;
	using Newtonsoft.Json;

	/// <summary>
	/// Settings related to the <see cref="AppDataPortalBusProvider{TMessage}"/>.
	/// </summary>
	/// <typeparam name="TMessage"></typeparam>
	public class AppDataPortalBusOptions<TMessage>
	{
		public string AppDataPath { get; set; }
		public string InstanceId { get; set; }
		public TimeSpan Timeout { get; set; }
		public ResiliencePipeline ResiliencePipeline { get; set; }

		public AppDataPortalBusOptions(WebAppSettings webAppSettings)
		{
			var retryStrategy = RetryPolicies.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

			this.AppDataPath = "~/App_Data/Adxstudio.Xrm.AspNet.PortalBus/" + typeof(TMessage).Name;
			this.InstanceId = webAppSettings.InstanceId;
			this.Timeout = TimeSpan.FromMinutes(5);
			this.ResiliencePipeline = retryStrategy.CreateRetryPolicy();
		}
	}

	/// <summary>
	/// A portal bus that uses a shared filesystem (App_Data) folder for sending messages to remote instances.
	/// </summary>
	/// <typeparam name="TMessage"></typeparam>
	public class AppDataPortalBusProvider<TMessage> : PortalBusProvider<TMessage>
	{
		private static readonly JsonSerializer _serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };

		protected string AppDataFullPath { get; private set; }
		protected AppDataPortalBusOptions<TMessage> Options { get; private set; }

		public AppDataPortalBusProvider(IAppBuilder app, AppDataPortalBusOptions<TMessage> options)
			: base(app)
		{
			this.AppDataFullPath = options.AppDataPath.StartsWith("~/")
				? System.Web.Hosting.HostingEnvironment.MapPath(options.AppDataPath)
				: options.AppDataPath;
			this.Options = options;

			this.RegisterAppDisposing(app);
			this.Subscribe();
		}

		protected void RegisterAppDisposing(IAppBuilder app)
		{
			var properties = new AppProperties(app.Properties);
			var token = properties.OnAppDisposing;

			if (token != CancellationToken.None)
			{
				token.Register(this.OnAppDisposing);
			}
		}

		protected virtual void OnAppDisposing()
		{
			var subscriptionPath = Path.Combine(this.AppDataFullPath, this.Options.InstanceId);

			if (this.Options.ResiliencePipeline.DirectoryExists(subscriptionPath))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Delete: subscriptionPath={0}", subscriptionPath));
				this.Options.ResiliencePipeline.DirectoryDelete(subscriptionPath, true);
			}
		}

		#region Subscribe Members

		protected void Subscribe()
		{
			// watch for renamed files which indicates messages ready for handling

			var buffer = new BufferBlock<string>();
			var subscriptionPath = this.GetSubscriptionPath();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Subscribe: subscriptionPath={0}", subscriptionPath));

			try
			{
				var watcher = this.Options.ResiliencePipeline.CreateFileSystemWatcher(subscriptionPath);
				watcher.Renamed += (sender, args) => buffer.Post(args.FullPath);
				watcher.Error += async (sender, args) => await this.OnSubscriptionErrorAsync(args.GetException()).WithCurrentCulture();
			}
			catch (Exception e)
			{
				WebEventSource.Log.GenericErrorException(e);

				if (this.Options.ResiliencePipeline.DirectoryExists(subscriptionPath))
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Delete: subscriptionPath={0}", subscriptionPath));
					this.Options.ResiliencePipeline.DirectoryDelete(subscriptionPath, true);
				}

				throw;
			}

			var action = new ActionBlock<string>(this.OnSubscriptionChangedAsync).AsObserver();

			buffer.AsObservable().Subscribe(action);
		}

		protected virtual string GetSubscriptionPath()
		{
			var subscriptionPath = Path.Combine(this.AppDataFullPath, this.Options.InstanceId);

			if (this.Options.ResiliencePipeline.DirectoryExists(subscriptionPath))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Exists: subscriptionPath={0}", subscriptionPath));
			}
			else
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Create: subscriptionPath={0}", subscriptionPath));
				this.Options.ResiliencePipeline.DirectoryCreate(subscriptionPath);
			}

			return subscriptionPath;
		}

		protected virtual IOwinContext GetOwinContext()
		{
			return new OwinContext();
		}

		protected virtual Task OnSubscriptionErrorAsync(Exception error)
		{
			WebEventSource.Log.GenericErrorException(new Exception("Subscription error: InstanceId: " + this.Options.InstanceId, error));

			return Task.FromResult(0);
		}

		protected virtual async Task OnSubscriptionChangedAsync(string messagePath)
		{
			try
			{
				if (!this.Options.ResiliencePipeline.FileExists(messagePath)) return;

				var context = this.GetOwinContext();
				var message = this.Deserialize(messagePath) as IPortalBusMessage;

				if (message != null && message.Validate(context, this.Protector))
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("messagePath={0}", messagePath));

					await message.InvokeAsync(context).WithCurrentCulture();
				}

				// clean up the file

				this.Options.ResiliencePipeline.FileDelete(messagePath);
			}
			catch (Exception e)
			{
				WebEventSource.Log.GenericErrorException(new Exception("Subscription error: InstanceId: " + this.Options.InstanceId, e));
			}
		}

		protected virtual TMessage Deserialize(string messagePath)
		{
			using (var reader = this.Options.ResiliencePipeline.OpenText(messagePath))
			using (var jr = new JsonTextReader(reader))
			{
				return _serializer.Deserialize<TMessage>(jr);
			}
		}

		#endregion

		#region Send Members

		protected override async Task SendRemoteAsync(IOwinContext context, TMessage message)
		{
			if (!this.Options.ResiliencePipeline.DirectoryExists(this.AppDataFullPath))
			{
				return;
			}

			var directory = this.Options.ResiliencePipeline.GetDirectory(this.AppDataFullPath);
			var subscriptions = this.Options.ResiliencePipeline.GetDirectories(directory, "*")
				.Where(subscription => !string.Equals(subscription.Name, this.Options.InstanceId));

			foreach (var subscription in subscriptions)
			{
				await this.WriteMessage(this.Options.Timeout, subscription.FullName, message).WithCurrentCulture();
			}
		}

		protected virtual Task WriteMessage(TimeSpan timeout, string subscriptionPath, TMessage message)
		{
			// clean up expired files

			var directory = this.Options.ResiliencePipeline.GetDirectory(subscriptionPath);

			var expired =
				this.Options.ResiliencePipeline.GetFiles(directory, "*.msg")
				.Where(file => file.LastAccessTimeUtc + timeout < DateTime.UtcNow)
				.ToArray();

			foreach (var file in expired)
			{
				try
				{
					this.Options.ResiliencePipeline.FileDelete(file);
				}
				catch (Exception e)
				{
					ADXTrace.Instance.TraceError(TraceCategory.Application, e.ToString());
				}
			}

			var name = GetFilename(message);
			var tempPath = Path.Combine(subscriptionPath, name + ".lock.msg");
			var messagePath = Path.Combine(subscriptionPath, name + ".msg");

			// write to a temporary file

			using (var fs = this.Options.ResiliencePipeline.Open(tempPath, FileMode.CreateNew))
			using (var sw = new StreamWriter(fs))
			using (var jw = new JsonTextWriter(sw))
			{
				jw.Formatting = Formatting.Indented;

				_serializer.Serialize(jw, message);
			}

			// rename to the destination file

			this.Options.ResiliencePipeline.FileMove(tempPath, messagePath);

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("messagePath={0}", messagePath));
			
			return Task.FromResult(0);
		}

		private static string GetFilename(TMessage message)
		{
			var pbm = message as PortalBusMessage;

			return pbm != null && !string.IsNullOrWhiteSpace(pbm.Id)
				? pbm.Id
				: Guid.NewGuid().ToString();
		}

		#endregion
	}
}
