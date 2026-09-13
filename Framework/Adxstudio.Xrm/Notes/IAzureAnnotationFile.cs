/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Adxstudio.Xrm.Notes
{
	public interface IAzureAnnotationFile : IAnnotationFile
	{
		BlobClient BlockBlob { get; set; }
	}
}
