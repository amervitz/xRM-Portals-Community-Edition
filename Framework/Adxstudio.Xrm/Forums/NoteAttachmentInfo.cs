/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Net;
using Adxstudio.Xrm.Notes;
using Adxstudio.Xrm.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Web;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Forums
{
	public class NoteAttachmentInfo : IForumPostAttachmentInfo
	{
		public NoteAttachmentInfo(EntityReference annotation, string name, string contentType, int size, Guid? websiteId = null, BlobContainerClient cloudStorageContainer = null)
		{
			if (annotation == null) throw new ArgumentNullException("annotation");

			Name = name;
			ContentType = contentType;
			Path = (new Entity(annotation.LogicalName) { Id = annotation.Id }).GetFileAttachmentPath(websiteId);
			Size = new FileSize(Convert.ToUInt64(size < 0 ? 0 : size));
			if (cloudStorageContainer != null)
			{
				var file =
					cloudStorageContainer.GetBlobClient("{0:N}/{1}".FormatWith(annotation.Id, name));

				// Fetching the attributes reports a missing blob as a 404, so the size is established
				// in a single request and the CRM-reported size is kept when the blob is absent.
				try
				{
					Size = new FileSize(Convert.ToUInt64(file.GetProperties().Value.ContentLength));
				}
				catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
				{
				}
			}
		}

		public string Name { get; private set; }

		public string ContentType { get; private set; }

		public ApplicationPath Path { get; private set; }

		public FileSize Size { get; private set; }
	}
}
