/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.IO;
using System.Threading;
using System.Web;
using Adxstudio.Xrm.Text;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Notes
{
	internal abstract class AnnotationFile : IAnnotationFile
	{
		private Lazy<Entity> _annotation;

		private delegate Stream GetStream();
		private readonly GetStream _getStream;
		

		protected AnnotationFile()
		{
			_annotation = new Lazy<Entity>(() => null, LazyThreadSafetyMode.None);
			_getStream = null;
		}

		protected AnnotationFile(HttpPostedFileBase file)
		{
			FileName = AnnotationHelper.EnsureValidFileName(file.FileName);
			MimeType = file.ContentType;
			FileSize = new FileSize(file.ContentLength > 0 ? Convert.ToUInt64(file.ContentLength) : 0);
			_annotation = new Lazy<Entity>(() => null, LazyThreadSafetyMode.None);
			_getStream = () => file.InputStream;
		}

		protected AnnotationFile(string fileName, string contentType, byte[] fileContent)
		{
			FileName = AnnotationHelper.EnsureValidFileName(fileName);
			MimeType = contentType;
			FileSize = new FileSize(fileContent.Length > 0 ? Convert.ToUInt64(fileContent.Length) : 0);
			_annotation = new Lazy<Entity>(() => null, LazyThreadSafetyMode.None);
			_getStream = () => new MemoryStream(fileContent);
		}

		public string FileName { get; set; }
		public string MimeType { get; set; }
		public FileSize FileSize { get; set; }

		public Entity Annotation 
		{
			get { return _annotation.Value; }
			set { _annotation = new Lazy<Entity>(() => value, LazyThreadSafetyMode.None); }
		}

		public string Url
		{
			get { return Annotation == null ? "#" : Annotation.GetFileAttachmentUrl(); }
		}

		public Stream GetFileStream()
		{
			return _getStream == null ? null : _getStream();
		}

		public void SetAnnotation(Func<Entity> func)
		{
			_annotation = new Lazy<Entity>(func, LazyThreadSafetyMode.None);
		}
	}
}
