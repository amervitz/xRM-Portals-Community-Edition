/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site
{
	using System.Globalization;
	using System.Web.Optimization;
	using Adxstudio.Xrm.Web.Mvc.Html;
	using Adxstudio.Xrm.AspNet.Cms;

	public static class BundleConfig
	{
		private static string GetTimeAgoLanguageCode(CultureInfo culture)
		{
			string languageLetterCode = culture.TwoLetterISOLanguageName;
			int lcid = ContextLanguageInfo.ResolveCultureLcid(culture.LCID);

			if (lcid == 1044) //norwegian (bokmål)
			{
				languageLetterCode = "no";
			}
			else if (lcid == 1046) //portuguese (brazil)
			{
				languageLetterCode = "pt-br";
			}
			else if (lcid == 3098) //serbian (cyrillic)
			{
				languageLetterCode = "sr";
			}
			else if (lcid == 2074) //serbian (latin)
			{
				languageLetterCode = "rs";
			}
			else if (lcid == 1028) //chinese (traditional)
			{
				languageLetterCode = "zh-TW";
			}
			else if (lcid == 2052) //chinese (simplified)
			{
				languageLetterCode = "zh-CN";
			}
			else if (lcid == 3076)
			{
				languageLetterCode = "zh-HK";
			}

			return languageLetterCode;
		}

		public static void RegisterBundles(BundleCollection bundles)
		{
			bundles.Add(new StyleBundle("~/css/default.bundle.css").Include(
				"~/js/google-code-prettify/prettify.css",
				"~/css/bootstrap-datetimepicker.min.css",
				"~/css/rateit.css",
				"~/css/portal.css",
				"~/css/timeline.css",
				"~/css/comments.css",
				"~/css/map.css",
				"~/css/webforms.css",
				"~/css/ckmenu.css",
				"~/Areas/Search/css/facet.css",
				"~/Areas/Search/css/search-content.css"));

			var preformBundle = new ScriptBundle("~/js/default.preform.bundle.js");
			preformBundle.Include("~/js/jquery-1.12.4.min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/jquery-migrate-{version}.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/respond.min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/underscore-min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/moment-with-locales.min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/moment-with-locales_zh-hk.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/DateFormat.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/URI.min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/bootstrap-datetimepicker.min.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/ckeditor-basepath.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/ckeditor/ckeditor.js");		// Skip minification. ckeditor.js is already minified and performing another minification will actually throw error.
			preformBundle.Include("~/js/antiforgerytoken.js", new JsMinifySingleItem());
			preformBundle.Include("~/js/ckoverwriteclass.js", new JsMinifySingleItem());
			preformBundle.Transforms.Clear();
			bundles.Add(preformBundle);
		}

		/// <summary>
		/// ItemTransform implementation that performs JsMinify on a single bundle item.
		/// </summary>
		private class JsMinifySingleItem : IItemTransform
		{
			/// <summary>
			/// ItemTransform to perform JsMinify transform on a single input item.
			/// </summary>
			/// <param name="includedVirtualPath">Included virtual path.</param>
			/// <param name="input">Input content.</param>
			/// <returns>Minified result.</returns>
			public string Process(string includedVirtualPath, string input)
			{
				var minifier = new Microsoft.Ajax.Utilities.Minifier();
				var result = minifier.MinifyJavaScript(input);
				if (minifier.ErrorList.Count > 0)
				{
					System.Text.StringBuilder errorMessage = new System.Text.StringBuilder();
					errorMessage.AppendLine("/* Minification failed. Returning unminified contents.");
					foreach (var error in minifier.ErrorList)
					{
						errorMessage.AppendLine(error.ToString());
					}
					errorMessage.AppendLine(" */");
					errorMessage.Append(input);
					return errorMessage.ToString();
				}
				return result;
			}
		}

		public static void RegisterLanguageSpecificBundles(BundleCollection bundles, CultureInfo culture)
		{
			var bundleName = LocalizedScripts.GetLocalizedBundleName("~/js/default.bundle.js", ContextLanguageInfo.ResolveCultureLcid(culture.LCID));

			var defaultBundle = new ScriptBundle(bundleName);

			defaultBundle.Include("~/js/bootstrap.min.js",
				"~/js/eventListener.polyfill.js",
				"~/js/handlebars.js",
				"~/js/date.js",
				"~/js/timeago/jquery.timeago.js",
				"~/js/google-code-prettify/prettify.js",
				"~/js/jquery.cookie.js",
				"~/js/jquery.bootstrap-pagination.js",
				"~/js/jquery.blockUI.js",
				"~/js/jquery.form.min.js",
				"~/js/entity-notes.js",
				"~/js/entity-form.js",
				"~/js/entity-grid.js",
				"~/js/entity-associate.js",
				"~/js/entity-lookup.js",
				"~/js/quickform.js",
				"~/js/serialized-query.js",
				"~/Areas/Cms/js/ads.js",
				"~/Areas/Cms/js/polls.js",
				"~/Areas/Chat/js/auth.js",
				"~/Areas/CaseManagement/js/case-deflection.js",
				"~/Areas/CaseManagement/js/entitlements.js",
				"~/Areas/Search/js/faceted-search.js",
				"~/Areas/KnowledgeManagement/js/article.js",
				"~/js/badges.js",
				"~/js/sharepoint-grid.js");

#if HIGHCHARTS
			defaultBundle.Include("~/js/charts/highcharts/highcharts.js");
#endif
#if HIGHCHARTSFUNNEL
			defaultBundle.Include("~/js/charts/highcharts/funnel.js");
#endif

			defaultBundle.Include("~/js/charts/crm/MicrosoftAjax.js",
				"~/js/charts/crm/Microsoft.Crm.Client.Core.js",
				"~/js/charts/crm/CrmInternalUtility.js",
				"~/js/charts/crm/CrmHighchartsLibrary.js",
				"~/js/charts/crm/PortalChartOrchestrator.js",
				"~/js/charts/chart.js",
				"~/js/jQuery.rateit.min.js",
				"~/js/rating.js",
				"~/js/portal.js")
				.Include(string.Format(CultureInfo.InvariantCulture, "~/js/timeago/locales/jquery.timeago.{0}.js",
					GetTimeAgoLanguageCode(culture)));

			bundles.Add(defaultBundle);
		}
	}
}
