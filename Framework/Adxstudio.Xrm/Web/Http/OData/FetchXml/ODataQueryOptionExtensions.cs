/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using Microsoft.AspNet.OData.Query;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Services.Query;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Web.Http.OData.FetchXml
{
	/// <summary>
	/// Operations that extend <see cref="ODataQueryOptions"/> to provide support for FetchXml.
	/// </summary>
	public static class ODataQueryOptionExtensions
	{
		/// <summary>
		/// Applys the <see cref="ODataQueryOptions"/> to <see cref="Fetch"/>
		/// </summary>
		/// <param name="queryOptions"><see cref="ODataQueryOptions"/></param>
		/// <param name="querySettings"><see cref="ODataQuerySettings"/></param>
		/// <param name="originalFetch">orginal <see cref="Fetch"/></param>
		/// <returns><see cref="Fetch"/></returns>
		public static Fetch ApplyTo(this ODataQueryOptions queryOptions, ODataQuerySettings querySettings, Fetch originalFetch)
		{
			var fetch = originalFetch;
			
			// convert $filter

			var filter = ToFetchFilter(queryOptions.Filter);

			if (filter != null)
			{
				fetch.Entity.Filters.Add(filter);
			}

			// convert $orderby

			var orders = ToFetchOrder(queryOptions.OrderBy);

			if (orders.Any())
			{
				fetch.Entity.Orders.Clear();

				fetch.Entity.Orders = orders;
			}

			// convert $select

			var attributes = ToFetchAttributes(queryOptions.SelectExpand);

			if (attributes.Any())
			{
				fetch.Entity.Attributes.Clear();

				fetch.Entity.Attributes = attributes;
			}

			// apply ODataQuerySettings

			fetch = fetch.Apply(querySettings);

			// apply $top

			fetch = fetch.Apply(queryOptions.Top);

			// apply $skip

			fetch = fetch.Apply(queryOptions.Skip);

			// apply $count

			fetch = fetch.Apply(queryOptions.Count);

			return fetch;
		}

		private static Fetch Apply(this Fetch fetch, ODataQuerySettings querySettings)
		{
			if (querySettings != null && querySettings.PageSize.HasValue)
			{
				fetch.PageSize = querySettings.PageSize.Value;
			}

			return fetch;
		}

		private static Fetch Apply(this Fetch fetch, TopQueryOption topQueryOption)
		{
			if (topQueryOption != null)
			{
				fetch.PageSize = topQueryOption.Value;
			}

			return fetch;
		}

		private static Fetch Apply(this Fetch fetch, SkipQueryOption skipQueryOption)
		{
			if (!fetch.PageSize.HasValue)
			{
				return fetch;
			}

			if (skipQueryOption != null)
			{
				fetch.PageNumber = skipQueryOption.Value > 0 ? (int)Math.Ceiling(skipQueryOption.Value / (double)fetch.PageSize.Value) + 1 : 1;
			}

			return fetch;
		}

		private static Fetch Apply(this Fetch fetch, CountQueryOption countQueryOption)
		{
			if (countQueryOption != null)
			{
				fetch.ReturnTotalRecordCount = countQueryOption.Value;
			}

			return fetch;
		}

		private static List<Order> ToFetchOrder(OrderByQueryOption orderByQueryOption)
		{
			var orders = new List<Order>();

			if (orderByQueryOption != null)
			{
				foreach (var orderByNode in orderByQueryOption.OrderByNodes)
				{
					var orderByPropertyNode = orderByNode as OrderByPropertyNode;

					if (orderByPropertyNode != null)
					{
						orders.Add(new Order(orderByPropertyNode.Property.Name, orderByPropertyNode.Direction == OrderByDirection.Descending ? OrderType.Descending : OrderType.Ascending));
					}
					else
					{
						throw new ODataException("Only ordering by properties is supported.");
					}
				}
			}

			return orders;
		}

		private static Filter ToFetchFilter(FilterQueryOption filterQueryOption)
		{
			Filter filter = null;

			if (filterQueryOption != null)
			{
				filter = FetchFilterBinder.BindFilterQueryOption(filterQueryOption);
			}

			return filter;
		}

		private static List<FetchAttribute> ToFetchAttributes(SelectExpandQueryOption selectExpandQueryOption)
		{
			var attributes = new List<FetchAttribute>();

			if (selectExpandQueryOption != null)
			{
				throw new NotImplementedException();
			}

			return attributes;
		}

		/// <summary>
		/// Get the link to the next page of records
		/// </summary>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <param name="pageSize">Size of the page</param>
		/// <returns></returns>
		public static Uri GetNextPageLink(HttpRequestMessage request, int pageSize)
		{
			return GetNextPageLink(request.RequestUri, request.GetQueryNameValuePairs(), pageSize);
		}

		internal static Uri GetNextPageLink(Uri requestUri, int pageSize)
		{
			return GetNextPageLink(requestUri, new FormDataCollection(requestUri), pageSize);
		}

		internal static Uri GetNextPageLink(Uri requestUri, IEnumerable<KeyValuePair<string, string>> queryParameters, int pageSize)
		{
			var stringBuilder = new StringBuilder();
			var num = pageSize;
			foreach (var keyValuePair in queryParameters)
			{
				var key = keyValuePair.Key;
				var str1 = keyValuePair.Value;
				switch (key)
				{
					case "$top":
						int result1;
						if (int.TryParse(str1, out result1))
						{
							str1 = (result1 - pageSize).ToString(CultureInfo.InvariantCulture);
						}
						break;
					case "$skip":
						int result2;
						if (int.TryParse(str1, out result2))
						{
							num += result2;
						}
						continue;
				}
				var str2 = key.Length <= 0 || (int)key[0] != 36 ? Uri.EscapeDataString(key) : string.Format("${0}", Uri.EscapeDataString(key.Substring(1)));
				var str3 = Uri.EscapeDataString(str1);
				stringBuilder.Append(str2);
				stringBuilder.Append('=');
				stringBuilder.Append(str3);
				stringBuilder.Append('&');
			}
			stringBuilder.AppendFormat("$skip={0}", num);
			return new UriBuilder(requestUri)
			{
				Query = stringBuilder.ToString()
			}.Uri;
		}
	}
}
