/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using Adxstudio.Xrm.Search;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.Configuration;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.Cms;

namespace Adxstudio.Xrm.Web.UI.WebControls
{
	public class SearchDataSourceView : DataSourceView
	{
		public SearchDataSourceView(IDataSource owner, string viewName) : base(owner, viewName)
		{
			Owner = (SearchDataSource)owner;
		}

		public override bool CanPage
		{
			get { return true; }
		}

		public override bool CanRetrieveTotalRowCount
		{
			get { return true; }
		}

		protected SearchDataSource Owner { get; private set; }

		protected override IEnumerable ExecuteSelect(DataSourceSelectArguments args)
		{
			args.AddSupportedCapabilities(DataSourceCapabilities.Page);
			args.AddSupportedCapabilities(DataSourceCapabilities.RetrieveTotalRowCount);

			args.RaiseUnsupportedCapabilitiesError(this);

			var parameters = Owner.SelectParameters.GetValues(HttpContext.Current, Owner);

			var filter = GetFilter(args, parameters);
			var logicalNames = GetLogicalNames(args, parameters);
			var pageSize = GetPageSize(args, parameters);
			var pageNumber = GetPageNumber(args, parameters, pageSize);
			var queryText = GetQueryText(args, parameters);
			ContextLanguageInfo contextLanguage;
			var multiLanguageEnabled = TryGetLanguageCode(out contextLanguage);

			if (string.IsNullOrWhiteSpace(queryText) && string.IsNullOrWhiteSpace(filter))
			{
				args.TotalRowCount = 0;

				return new CrmEntitySearchResultPage(new List<ICrmEntitySearchResult>(), 0, pageNumber, pageSize);
			}

			var provider = SearchManager.GetProvider(Owner.SearchProvider);
			var query = new CrmEntityQuery(queryText, pageNumber, pageSize, logicalNames, contextLanguage.ContextLanguage, multiLanguageEnabled, filter);

			var selectingArgs = new SearchDataSourceSelectingEventArgs(provider, query);

			Owner.OnSelecting(selectingArgs);

			if (selectingArgs.Cancel)
			{
				args.TotalRowCount = 0;

				return new CrmEntitySearchResultPage(new List<ICrmEntitySearchResult>(), 0, pageNumber, pageSize);
			}

			try
			{
				using (var searcher = provider.GetIndexSearcher())
				{
					var results = searcher.Search(query);

					args.TotalRowCount = results.ApproximateTotalHits;

					Owner.OnSelected(new SearchDataSourceStatusEventArgs(provider, query, results));

					return results;
				}
			}
			catch (Exception e)
			{
                ADXTrace.Instance.TraceError(TraceCategory.Application, e.ToString());

                var selectedArgs = new SearchDataSourceStatusEventArgs(provider, query, new CrmEntitySearchResultPage(new List<ICrmEntitySearchResult>(), 0, pageNumber, pageSize))
				{
					Exception = e
				};

				Owner.OnSelected(selectedArgs);

				if (!selectedArgs.ExceptionHandled)
				{
					throw;
				}

				args.TotalRowCount = 0;

				return new CrmEntitySearchResultPage(new List<ICrmEntitySearchResult>(), 0, pageNumber, pageSize);
			}
		}

		protected string GetFilter(DataSourceSelectArguments args, IDictionary parameters)
		{
			if (string.IsNullOrEmpty(Owner.Filter))
			{
				return (parameters["Filter"] ?? string.Empty).ToString();
			}

			return Regex.Replace(Owner.Filter, @"@(?<parameter>\w+)", match =>
			{
				var value = parameters[match.Groups["parameter"].Value];

				return value == null ? match.Value : value.ToString();
			});
		}

		protected IEnumerable<string> GetLogicalNames(DataSourceSelectArguments args, IDictionary parameters)
		{
			if (!string.IsNullOrEmpty(Owner.LogicalNames))
			{
				var values = Owner.LogicalNames.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToArray();

				if (values.Any())
				{
					return values;
				}
			}

			var parameter = parameters["LogicalNames"];

			if (parameter != null)
			{
				if (parameter is IEnumerable<string>)
				{
					return (parameter as IEnumerable<string>).ToArray();
				}

				var values = parameter.ToString().Split(',').Where(s => !string.IsNullOrEmpty(s)).ToArray();

				if (values.Any())
				{
					return values;
				}
			}

			return Enumerable.Empty<string>();
		}

		protected string GetQueryText(DataSourceSelectArguments args, IDictionary parameters)
		{
			if (string.IsNullOrEmpty(Owner.Query))
			{
				return (parameters["Query"] ?? string.Empty).ToString();
			}

			return Regex.Replace(Owner.Query, @"@(?<parameter>\w+)", match =>
			{
				var value = parameters[match.Groups["parameter"].Value];
				var res = value == null ? match.Value : value.ToString();
			    return HttpUtility.HtmlDecode(res ?? string.Empty);
			});
		}

		protected bool TryGetLanguageCode(out ContextLanguageInfo websiteLanguage)
		{
			var contextLanguage = HttpContext.Current.GetContextLanguageInfo();
			websiteLanguage = contextLanguage;
			return contextLanguage.IsCrmMultiLanguageEnabled;
		}

		protected static int GetPageNumber(DataSourceSelectArguments args, IDictionary parameters, int pageSize)
		{
			if (args.StartRowIndex > 0)
			{
				return (args.StartRowIndex / pageSize) + 1;
			}

			var pageNumberParam = parameters["PageNumber"];

			int pageNumberParamValue;

			if ((pageNumberParam != null) && int.TryParse(pageNumberParam.ToString(), out pageNumberParamValue))
			{
				return pageNumberParamValue;
			}

			return 1;
		}

		protected static int GetPageSize(DataSourceSelectArguments args, IDictionary parameters)
		{
			if (args.MaximumRows > 0)
			{
				return args.MaximumRows;
			}

			var pageSizeParam = parameters["PageSize"];

			int pageSizeParamValue;

			if ((pageSizeParam != null) && int.TryParse(pageSizeParam.ToString(), out pageSizeParamValue))
			{
				return pageSizeParamValue;
			}

			return 10;
		}
	}
}
