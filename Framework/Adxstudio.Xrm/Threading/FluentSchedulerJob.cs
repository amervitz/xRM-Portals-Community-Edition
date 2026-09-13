/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Threading
{
	using System;
	using System.Diagnostics.Tracing;
	using System.Web.Hosting;
	
	using Adxstudio.Xrm.Web;

	/// <summary>
	/// A scheduler job.
	/// </summary>
	public abstract class FluentSchedulerJob : IRegisteredObject
	{
		/// <summary>
		/// Safe shutdown lock.
		/// </summary>
		private readonly object shutDownLock = new object();

		/// <summary>
		/// Shutdown flag.
		/// </summary>
		private bool shuttingDown;

		/// <summary>
		/// Executes the job.
		/// </summary>
		public void Execute()
		{
			var id = Guid.NewGuid();
			var name = this.GetType().ToString();
			EventSource.SetCurrentThreadActivityId(id);

			// Register this job with the hosting environment.
			// Allows for a more graceful stop of the job, in the case of IIS shutting down.
			HostingEnvironment.RegisterObject(this);

			try
			{
				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Job registered: {0}", name));

				lock (this.shutDownLock)
				{
					if (this.shuttingDown)
					{
						return;
					}

					ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Job begin: {0}", name));

					this.ExecuteInternal(id);

					ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Job end: {0}", name));
				}
			}
			catch (Exception e)
			{
				this.OnError(e);

				throw;
			}
			finally
			{
				HostingEnvironment.UnregisterObject(this);

				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Job unregistered: {0}", name));
			}
		}

		/// <summary>
		/// Error event.
		/// </summary>
		/// <param name="e">The error.</param>
		protected virtual void OnError(Exception e)
		{
			WebEventSource.Log.GenericErrorException(e);
		}

		/// <summary>
		/// Requests a registered object to unregister.
		/// </summary>
		/// <param name="immediate">true to indicate the registered object should unregister from the hosting environment before returning; otherwise, false.</param>
		public void Stop(bool immediate)
		{
			// Locking here will wait for the lock in Execute to be released until this code can continue.
			lock (this.shutDownLock)
			{
				this.shuttingDown = true;
			}
		}

		/// <summary>
		/// The body.
		/// </summary>
		/// <param name="id">The activity id.</param>
		protected abstract void ExecuteInternal(Guid id);
	}
}
