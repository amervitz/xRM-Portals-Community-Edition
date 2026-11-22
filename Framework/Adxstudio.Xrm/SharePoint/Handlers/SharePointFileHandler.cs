/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Adxstudio.SharePoint;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Diagnostics;

namespace Adxstudio.Xrm.SharePoint.Handlers
{
	/// <summary>
	/// A handler for managing SharePoint files.
	/// </summary>
	public class SharePointFileHandler : IHttpHandler
	{
		private static readonly IEnumerable<string> _headersToRequest = new[] { "Accept-Encoding", "Accept-Language", "Cache-Control", "If-None-Match" };
		private static readonly IEnumerable<string> _headersToRespond = new[] { "Content-Length", "Cache-Control", "Content-Type", "Expires", "ETag", "Last-Modified", "Date" };

		public SharePointFileHandler(Uri sharePointFileUrl, string fileName)
		{
			SharePointFileUrl = sharePointFileUrl;
			FileName = fileName;
		}

		public bool IsReusable
		{
			get { return false; }
		}

		protected virtual Uri SharePointFileUrl { get; private set; }

		protected virtual string FileName { get; private set; }

		public void ProcessRequest(HttpContext context)
		{
			if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.InvariantCultureIgnoreCase)
				|| SharePointFileUrl == null
				|| string.IsNullOrWhiteSpace(FileName))
			{
				context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				return;
			}

			try
			{
				var spConnection = new SharePointConnection("SharePoint");
				var factory = new ClientFactory();
				var request = factory.CreateFileDownloadRequest(spConnection, SharePointFileUrl);

				// make sure SharePoint receives the cache control headers from the browser

				var requestHeaders = _headersToRequest
					.Select(name => new { Name = name, Value = context.Request.Headers[name] })
					.Where(header => !string.IsNullOrWhiteSpace(header.Value))
					.ToList();

				foreach (var header in requestHeaders)
				{
					request.Headers[header.Name] = header.Value;
				}

				request.Accept = context.Request.Headers["Accept"];
				request.UserAgent = context.Request.Headers["User-Agent"];

				DateTime ifModifiedSince;
				if (DateTime.TryParse(context.Request.Headers["If-Modified-Since"], out ifModifiedSince))
				{
					request.IfModifiedSince = ifModifiedSince;
				}

				WebResponse response;

				try
				{
					response = request.GetResponse();
				}
				catch (WebException we)
				{
					// handle non-200 response from SharePoint

					var hwr = we.Response as HttpWebResponse;

					if (hwr != null && hwr.StatusCode == HttpStatusCode.NotModified)
					{
						context.Response.StatusCode = (int)HttpStatusCode.NotModified;
						return;
					}

                    ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("Exception thrown trying to download {0}", SharePointFileUrl));
					ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("ProcessRequest", "Exception details: {0}", we.ToString()));

                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

					return;
				}

				using (var httpResponse = (HttpWebResponse)response)
				using (var stream = httpResponse.GetResponseStream())
				{
					if ((int)httpResponse.StatusCode >= 300)
					{
						context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
						return;
					}

					// forward SharePoint response headers back to the browser

					var responseHeaders = _headersToRespond
						.Select(name => new { Name = name, Value = response.Headers[name] })
						.Where(header => !string.IsNullOrWhiteSpace(header.Value))
						.ToList();

					foreach (var header in responseHeaders)
					{
						context.Response.AppendHeader(header.Name, header.Value);
					}

					context.Response.AppendHeader("Content-Disposition", @"attachment; filename=""{0}""".FormatWith(FileName));

					int contentLength;

					if (!int.TryParse(response.Headers["Content-Length"], out contentLength))
					{
						// indeterminant length
						contentLength = -1;
					}

					if (contentLength == 0)
					{
						context.Response.StatusCode = (int)HttpStatusCode.NoContent;
						return;
					}

					// start streaming file

					context.Response.StatusCode = (int)HttpStatusCode.OK;

					const int bufferSize = 65536;
					var buffer = new byte[bufferSize];
					int bytesRead;

					do
					{
						bytesRead = stream.Read(buffer, 0, bufferSize);
						context.Response.OutputStream.Write(buffer, 0, bytesRead);
					} while (bytesRead > 0);
				}
			}
			catch (Exception e)
			{
                ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("Exception thrown trying to download {0}", SharePointFileUrl));
				ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("Exception details: {0}", e.ToString()));

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
			}
		}
	}
}
