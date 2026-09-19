/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Adxstudio.Xrm.AspNet.Cms
{
	/// <summary>
	/// Settings for <see cref="AppInfoMiddleware"/>."
	/// </summary>
	public class AppInfoOptions
	{
		public bool Enabled { get; set; }

		public PathString CallbackPath { get; set; }

		public string AssemblyName { get; set; }

		public AppInfoOptions()
		{
			Enabled = true;
			CallbackPath = new PathString("/_services/about/app");
			AssemblyName = "Adxstudio.Xrm";
		}
	}

	/// <summary>
	/// Returns basic public portal application properties.
	/// </summary>
	public class AppInfoMiddleware : OwinMiddleware
	{
		private static string _info;

		public AppInfoOptions Options { get; private set; }

		public AppInfoMiddleware(OwinMiddleware next, AppInfoOptions options)
			: base(next)
		{
			if (options == null) throw new ArgumentNullException("options");

			Options = options;
		}

		public override async Task Invoke(IOwinContext context)
		{
			if (Options.Enabled && Equals(context.Request.Path, Options.CallbackPath))
			{
				LazyInitializer.EnsureInitialized(ref _info, GetInfo);
                context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(_info);
			}
			else
			{
				await Next.Invoke(context);
			}
		}

		private string GetInfo()
		{
			var assembly = GetAssembly();
			var version = assembly.Version.ToString();
			var info = new JObject(new JProperty("version", version));

			return info.ToString(Formatting.Indented);
		}

		private AssemblyName GetAssembly()
		{
			var assemblies =
				from assembly in AppDomain.CurrentDomain.GetAssemblies()
				let name = assembly.GetName()
				where !assembly.GlobalAssemblyCache
				select name;

			var result = assemblies.FirstOrDefault(a => a.Name == Options.AssemblyName);

			return result;
		}
	}
}
