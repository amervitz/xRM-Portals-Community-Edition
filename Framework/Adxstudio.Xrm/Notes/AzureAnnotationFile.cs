/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Web;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Adxstudio.Xrm.Notes
{
	internal class AzureAnnotationFile : AnnotationFile, IAzureAnnotationFile
	{
		private BlobClient _blockBlob;
		private BlobProperties _blobProperties;

		public BlobClient BlockBlob
		{
			get { return _blockBlob; }
			set
			{
				_blockBlob = value;
				_blobProperties = null;
			}
		}

		// The previous SDK retained fetched attributes on the blob reference itself, where
		// reading them never issued a request and never threw: until FetchAttributes had run,
		// the caller simply saw a zero length and a null content type. Mirror that here rather
		// than fetching on demand, so that reading the properties of a blob that does not exist
		// (or whose attributes were never fetched) stays inert instead of throwing a 404.
		// Whoever fetches the attributes assigns them; replacing the blob invalidates them.
		public BlobProperties BlobProperties
		{
			get { return _blobProperties ?? (_blobProperties = BlobsModelFactory.BlobProperties()); }
			set { _blobProperties = value; }
		}
		
		public AzureAnnotationFile()
		{
		}

		public AzureAnnotationFile(HttpPostedFileBase file) : base(file)
		{
			BlockBlob = null;
		}

		public AzureAnnotationFile(string fileName, string contentType, byte[] fileContent)
			: base(fileName, contentType, fileContent)
		{
			BlockBlob = null;
		}
	}
}
