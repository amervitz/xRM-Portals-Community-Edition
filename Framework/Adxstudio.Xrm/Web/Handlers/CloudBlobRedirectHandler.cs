/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Configuration;
using System.Web;
using Adxstudio.Xrm.Resources;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Web.Handlers
{
	internal class CloudBlobRedirectHandler : IHttpHandler
	{
		private readonly string _blobAddress;

		public CloudBlobRedirectHandler(Entity entity)
		{
			if (entity == null) throw new ArgumentNullException("entity");

			_blobAddress = entity.GetAttributeValue<string>("adx_cloudblobaddress");
		}

		public void ProcessRequest(HttpContext context)
		{
			if (context == null) throw new ArgumentNullException("context");

			if (_blobAddress == null)
			{
				context.Response.StatusCode = 404;
				context.Response.ContentType = "text/plain";
				context.Response.Write(ResourceManager.GetString("Not_Found_Exception"));

				return;
			}

			CloudStorageAccount storageAccount;

			if (!TryGetCloudStorageAccount(context, out storageAccount))
			{
				context.Response.StatusCode = 404;
				context.Response.ContentType = "text/plain";
				context.Response.Write(ResourceManager.GetString("Failed_To_Configure_Cloud_Storage_Account"));

				return;
			}

			var blobClient = storageAccount.CreateCloudBlobClient();
			// Power Pages uses a fully qualified blob address ("https://account.blob.core.windows.net/container/file.txt"),
			// while xRM Portals uses one relative to the endpoint ("container/file.txt"). BaseUri appears twice to accept both:
			// the inner constructor resolves either form, ignoring the base when the address is already absolute,
			// and AbsolutePath then re-anchors just the container and file name to the configured account.
			var blob = blobClient.GetBlobReferenceFromServer(new Uri(blobClient.BaseUri, new Uri(blobClient.BaseUri, _blobAddress).AbsolutePath));

			var accessSignature = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy
			{
				Permissions = SharedAccessBlobPermissions.Read,
				SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(55)
			});
			
			context.Response.Redirect(blob.Uri + accessSignature);
		}

		public bool IsReusable
		{
			get { return false; }
		}

		protected virtual bool TryGetCloudStorageAccount(HttpContext context, out CloudStorageAccount storageAccount)
		{
			storageAccount = null;
			var website = context.GetWebsite();
			var settingValue = website.Settings.Get<string>("WebFiles/CloudStorageAccount");

			if (!string.IsNullOrEmpty(settingValue) && CloudStorageAccount.TryParse(settingValue, out storageAccount))
			{
				return true;
			}

			const string configurationKey = "Adxstudio.Xrm.Cms.WebFiles.CloudStorageAccount";

			try
			{
				storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings.Get(configurationKey));

				return storageAccount != null;
			}
			catch (InvalidOperationException)
			{
				var appSetting = ConfigurationManager.AppSettings[configurationKey];

				return !string.IsNullOrEmpty(appSetting) && CloudStorageAccount.TryParse(appSetting, out storageAccount);
			}
		}

		public static bool IsCloudBlob(Entity entity)
		{
			return entity != null && !string.IsNullOrEmpty(entity.GetAttributeValue<string>("adx_cloudblobaddress"));
		}

		public static bool TryGetCloudBlobHandler(Entity entity, out IHttpHandler handler)
		{
			if (IsCloudBlob(entity))
			{
				handler = new CloudBlobRedirectHandler(entity);

				return true;
			}

			handler = null;

			return false;
		}
	}
}
