/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Collections.Specialized;
using System.Web;
using System.Text;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;

namespace Microsoft.Xrm.Portal.Web
{
	/// <summary>
	/// Loads the current querystring when instantiated and can output a flat formatted string.
	/// </summary>
	public sealed class QueryStringCollection : NameValueCollection
	{
		public QueryStringCollection(System.Web.HttpContext context)
			: base(context.Request.QueryString)
		{
		}

		public QueryStringCollection(System.Web.HttpRequest request)
			: base(request.QueryString)
		{
		}

		public QueryStringCollection(NameValueCollection col)
			: base(col)
		{
		}

		/// <summary>
		/// Parses a string querystring representation.
		/// </summary>
		/// <param name="url"></param>
		public QueryStringCollection(string url)
		{
			if (!string.IsNullOrEmpty(url))
			{
				int i = url.IndexOf('?');

				if (i != -1)
				{
					url = url.Substring(i + 1);
					//Trace.Warn("url = " + url);
					string[] pairs = url.Split('&');

					foreach (string pair in pairs)
					{
						if ((!string.IsNullOrEmpty(pair)) && pair.Contains("="))
						{
							string delimiter = "=";
							string[] attribute = pair.Split(delimiter.ToCharArray(), 2);


							// decode querystring to store in the collection as decoded values
							Add(HttpUtility.UrlDecode(attribute[0]), HttpUtility.UrlDecode(attribute[1]));
						}
						else
						{
							// decode querystring to store in the collection as decoded values
							Add(HttpUtility.UrlDecode(pair), null);
						}
					}
				}
			}
		}

		public static implicit operator QueryStringCollection(string url)
		{
			return new QueryStringCollection(url);
		}

		public static implicit operator string(QueryStringCollection url)
		{
			return url.ToString();
		}

		public void EnableReadOnly()
		{
			base.IsReadOnly = true;
		}

		/// <summary>
		/// Returns a formatted querystring.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			StringBuilder queryString = new StringBuilder();

			// build out each attribute name/value pair in the collection
			foreach (string key in this.Keys)
			{
				// split up multi-valued attributes into separate pairs
				string[] values = this.GetValues(key);

				if (values != null)
				{
					foreach (string value in values)
					{
						if (!string.IsNullOrEmpty(key))
						{
							queryString.Append("&").Append(System.Web.Security.AntiXss.AntiXssEncoder.UrlEncode(key)).Append("=").Append(System.Web.Security.AntiXss.AntiXssEncoder.UrlEncode(value));
						}
						else
						{
							queryString.Append("&").Append(System.Web.Security.AntiXss.AntiXssEncoder.UrlEncode(value));
						}
					}
				}
				else
				{
					// there is a key with no values - just output the key
					queryString.Append("&").Append(System.Web.Security.AntiXss.AntiXssEncoder.UrlEncode(key));
				}
			}

			if (queryString.Length > 0 && queryString[0] == '&')
			{
				queryString[0] = '?';
			}
			return queryString.ToString();
		}
	}
}
