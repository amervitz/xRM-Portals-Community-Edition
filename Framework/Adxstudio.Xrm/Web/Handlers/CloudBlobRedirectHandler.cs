/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Configuration;
using System.Web;
using Adxstudio.Xrm.Resources;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
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

			BlobServiceClient storageAccount;

			if (!TryGetBlobServiceClient(context, out storageAccount))
			{
				context.Response.StatusCode = 404;
				context.Response.ContentType = "text/plain";
				context.Response.Write(ResourceManager.GetString("Failed_To_Configure_Cloud_Storage_Account"));

				return;
			}

			// Power Pages uses a fully qualified blob address ("https://account.blob.core.windows.net/container/file.txt"),
			// while xRM Portals uses one relative to the endpoint ("container/file.txt"). The endpoint appears twice to accept both:
			// the inner constructor resolves either form, ignoring the endpoint when the address is already absolute,
			// and AbsolutePath then re-anchors just the container and file name to the configured account.
			var endpoint = new Uri(storageAccount.Uri.AbsoluteUri.TrimEnd('/') + "/");
			var blobAddress = new BlobUriBuilder(new Uri(endpoint, new Uri(endpoint, _blobAddress).AbsolutePath));
			var blob = storageAccount.GetBlobContainerClient(blobAddress.BlobContainerName).GetBlobClient(blobAddress.BlobName);

			var downloadUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(55));
			
			context.Response.Redirect(downloadUri.AbsoluteUri);
		}

		public bool IsReusable
		{
			get { return false; }
		}

		protected virtual bool TryGetBlobServiceClient(HttpContext context, out BlobServiceClient storageAccount)
		{
			var website = context.GetWebsite();
			var settingValue = website.Settings.Get<string>("WebFiles/CloudStorageAccount");
			return Notes.AnnotationDataAdapter.TryCreateStorageClient(settingValue, out storageAccount)
				|| Notes.AnnotationDataAdapter.TryCreateStorageClient(ConfigurationManager.AppSettings["Adxstudio.Xrm.Cms.WebFiles.CloudStorageAccount"], out storageAccount);
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
