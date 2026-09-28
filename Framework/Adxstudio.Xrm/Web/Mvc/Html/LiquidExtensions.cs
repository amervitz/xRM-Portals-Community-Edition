/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Adxstudio.Xrm.Forums;
using Adxstudio.Xrm.Performance;
using Adxstudio.Xrm.Services;
using Adxstudio.Xrm.Services.Query;
using Adxstudio.Xrm.Web.Mvc.Liquid;
using DotLiquid;
using DotLiquid.Exceptions;
using DotLiquid.NamingConventions;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Web.Mvc.Html
{
	/// <summary>
	/// Liquid Templating Extensions
	/// </summary>
	public static class LiquidExtensions
	{
		internal const bool LiquidEnabledDefault = true;

		static LiquidExtensions()
		{
			// Call the DotLiquid.Liquid class's constructor first before registering our custom tags and filters.
			// Need to do this because DotLiquid has (and may again in the future) introduce tags/filters with the
			// same name as our custom ones. So we need to add ours AFTER in order for ours to take precedent.
			InitializeDotLiquid();

			Template.RegisterTag<Liquid.Tags.Editable>("editable");
			Template.RegisterTag<Liquid.Tags.EntityList>("entitylist");
			Template.RegisterTag<Liquid.Tags.EntityForm>("entityform");
			Template.RegisterTag<Liquid.Tags.WebForm>("webform");
			Template.RegisterTag<Liquid.Tags.EntityView>("entityview");
			Template.RegisterTag<Liquid.Tags.FetchXml>("fetchxml");
			Template.RegisterTag<Liquid.Tags.OutputCache>("outputcache");
			Template.RegisterTag<Liquid.Tags.SearchIndex>("searchindex");
			Template.RegisterTag<Liquid.Tags.Chart>("chart");
			Template.RegisterTag<Liquid.Tags.Redirect>("redirect");
			Template.RegisterTag<Liquid.Tags.Rating>("rating");
			Template.RegisterTag<Liquid.Tags.Substitution>("substitution");

			Template.RegisterFilter(typeof(Filters));
			Template.RegisterFilter(typeof(DateFilters));
			Template.RegisterFilter(typeof(EntityListFilters));
			Template.RegisterFilter(typeof(EnumerableFilters));
			Template.RegisterFilter(typeof(MathFilters));
			Template.RegisterFilter(typeof(TypeFilters));
			Template.RegisterFilter(typeof(StringFilters));
			Template.RegisterFilter(typeof(NumberFormatFilters));
			Template.RegisterFilter(typeof(UrlFilters));
			Template.RegisterFilter(typeof(SearchFilterOptionFilters));

			Template.NamingConvention = new InvariantCultureNamingConvention();
		}

		/// <summary>
		/// Calls the constructor of DotLiquid.Liquid class.
		/// </summary>
		private static void InitializeDotLiquid()
		{
			// Force a call to the static constructor.
			var temp = DotLiquid.Liquid.UseRubyDateFormat;
		}

		/// <summary>
		/// Invariant Culture Naming convention for Liquid- For Turkish, the field "id" does not get mapped correctly to the .net property.
		/// Using toLowerInvariant for member name check below fixes the problem
		/// </summary>
		private class InvariantCultureNamingConvention : INamingConvention
		{
			private readonly Regex _regex1 = new Regex(@"([A-Z]+)([A-Z][a-z])");
			private readonly Regex _regex2 = new Regex(@"([a-z\d])([A-Z])");

			public StringComparer StringComparer
			{
				get { return StringComparer.OrdinalIgnoreCase; }
			}

			public bool OperatorEquals(string testedOperator, string referenceOperator)
			{
				return StringComparer.Equals(testedOperator, referenceOperator);
			}

			public string GetMemberName(string name)
			{				
				return _regex2.Replace(_regex1.Replace(name, "$1_$2"), "$1_$2").ToLowerInvariant();
			}
		}

		private static IDictionary<string, Func<HtmlHelper, object>> _globalVariableFactories = new Dictionary<string, Func<HtmlHelper, object>>();

		/// <summary>
		/// Add a named variable value to be included in the global rendering scope.
		/// </summary>
		/// <param name="name">Variable name.</param>
		/// <param name="factory">Delegate to create the Liquid drop object.</param>
		public static void RegisterGlobalVariable(string name, Func<HtmlHelper, object> factory)
		{
			_globalVariableFactories[name] = factory;
		}
		
		private class LiquidEnvironment
		{
			public LiquidEnvironment(Hash globals, Hash registers)
			{
				if (globals == null) throw new ArgumentNullException("globals");
				if (registers == null) throw new ArgumentNullException("registers");

				Globals = globals;
				Registers = registers;
			}

			public Hash Globals { get; private set; }

			public Hash Registers { get; private set; }
		}

		private static LiquidEnvironment GetLiquidEnvironment(this HtmlHelper html)
		{
			const string environmentKey = "Adxstudio.Xrm.Web.Mvc.LiquidExtensions.GetLiquidEnvironment:Data";

			LiquidEnvironment environment;

			object data;

			if (html.ViewContext.TempData.TryGetValue(environmentKey, out data))
			{
				environment = data as LiquidEnvironment;

				if (environment != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Reusing Liquid environment.");

					return environment;
				}
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Creating new Liquid environment.");

			var portal = PortalCrmConfigurationManager.CreatePortalContext();

			var portalViewContext = PortalExtensions.GetPortalViewContext(html);
			var portalLiquidContext = new PortalLiquidContext(html, portalViewContext);

			var forumDependencies = new PortalContextDataAdapterDependencies(
				portal,
				new PaginatedLatestPostUrlProvider("page", html.IntegerSetting("Forums/PostsPerPage").GetValueOrDefault(20)));

			var blogDependencies = new Blogs.PortalConfigurationDataAdapterDependencies();
			var knowledgeDependencies = new KnowledgeArticles.PortalConfigurationDataAdapterDependencies();
			var contextDrop = new PortalViewContextDrop(portalLiquidContext, forumDependencies);
			var requestDrop = RequestDrop.FromHtmlHelper(portalLiquidContext, html);
			var siteMapDrop = new SiteMapDrop(portalLiquidContext, portalViewContext.SiteMapProvider);

			var globals = new Hash
			{
				{ "context", contextDrop },
				{ "entities", new EntitiesDrop(portalLiquidContext) },
				{ "now", DateTime.UtcNow },
				{ "params", requestDrop == null ? null : requestDrop.Params },
				{ "request", requestDrop },
				{ "settings", new SettingsDrop(portalViewContext.Settings) },
				{ "sharepoint", new SharePointDrop(portalLiquidContext) },
				{ "sitemap", siteMapDrop },
				{ "sitemarkers", new SiteMarkersDrop(portalLiquidContext, portalViewContext.SiteMarkers) },
				{ "snippets", new SnippetsDrop(portalLiquidContext, portalViewContext.Snippets) },
				{ "user", contextDrop.User },
				{ "weblinks", new WebLinkSetsDrop(portalLiquidContext, portalViewContext.WebLinks) },
				{ "ads", new AdsDrop(portalLiquidContext, portalViewContext.Ads) },
				{ "polls", new PollsDrop(portalLiquidContext, portalViewContext.Polls) },
				{ "forums", new ForumsDrop(portalLiquidContext, forumDependencies) },
				{ "events", new EventsDrop(portalLiquidContext, forumDependencies) },
				{ "blogs", new BlogsDrop(portalLiquidContext, blogDependencies) },
				{ "website", contextDrop.Website },
				{ "resx", new ResourceManagerDrop(portalLiquidContext) },
				{ "knowledge", new KnowledgeDrop(portalLiquidContext, knowledgeDependencies) },
				{ "uniqueId", new UniqueDrop() }
			};

			if (portalViewContext.Entity != null && siteMapDrop.Current != null)
			{
				globals["page"] = new PageDrop(portalLiquidContext, portalViewContext.Entity, siteMapDrop.Current);
			}

			foreach (var factory in _globalVariableFactories)
			{
				globals[factory.Key] = factory.Value(html);
			}

			environment = new LiquidEnvironment(globals, new Hash
			{
				{ "htmlHelper", html },
				{ "file_system", new CompositeFileSystem(
					new EntityFileSystem(portalViewContext, "adx_webtemplate", "adx_name", "adx_source"),
					new EmbeddedResourceFileSystem(typeof(LiquidExtensions).Assembly, "Adxstudio.Xrm.Liquid")) },
				{ "portalLiquidContext", portalLiquidContext }
			});

			html.ViewContext.TempData[environmentKey] = environment;

			return environment;
		}

		/// <summary>
		/// Given a <paramref name="webTemplateReference">Web Template (adx_webtemplate) reference</paramref>, render
		/// its Source (adx_source) attribute as a Liquid template.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="webTemplateReference">The Web Template (adx_webtemplate) to render.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		/// <param name="fallback">Optional fallback function to execute in the case that <paramref name="webTemplateReference"/> is null, or the full entity is not found.</param>
		public static string WebTemplate(this HtmlHelper html, EntityReference webTemplateReference, IDictionary<string, object> variables = null, Action fallback = null)
		{
			using (var output = new StringWriter())
			{
				RenderWebTemplate(html, webTemplateReference, output, variables, fallback);
				return output.ToString();
			}
		}

		/// <summary>
		/// Given a <paramref name="webTemplateReference">Web Template (adx_webtemplate) reference</paramref>, render
		/// its Source (adx_source) attribute as a Liquid template.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="webTemplateReference">The Web Template (adx_webtemplate) to render.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		/// <param name="fallback">Optional fallback function to execute in the case that <paramref name="webTemplateReference"/> is null, or the full entity is not found.</param>
		public static void RenderWebTemplate(this HtmlHelper html, EntityReference webTemplateReference, IDictionary<string, object> variables = null, Action fallback = null)
		{
			RenderWebTemplate(html, webTemplateReference, html.ViewContext.Writer, variables, fallback);
		}

		/// <summary>
		/// Given a <paramref name="webTemplateReference">Web Template (adx_webtemplate) reference</paramref>, render
		/// its Source (adx_source) attribute as a Liquid template.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="webTemplateReference">The Web Template (adx_webtemplate) to render.</param>
		/// <param name="output">Output to which rendered Liquid will be written.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		/// <param name="fallback">Optional fallback function to execute in the case that <paramref name="webTemplateReference"/> is null, or the full entity is not found.</param>
		public static void RenderWebTemplate(this HtmlHelper html, EntityReference webTemplateReference, TextWriter output, IDictionary<string, object> variables = null, Action fallback = null)
		{
			if (webTemplateReference == null)
			{
				if (fallback != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, "No template reference provided, rendering fallback.");
					fallback();
				}
				return;
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Rendering template {0}:{1}", webTemplateReference.LogicalName, webTemplateReference.Id));

			var webTemplate = FetchWebTemplate(html, webTemplateReference.Id);
			if (webTemplate == null)
			{
				if (fallback != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Template {0}:{1} not found, rendering fallback.", webTemplateReference.LogicalName, webTemplateReference.Id));
					fallback();
				}
				return;
			}

			RenderLiquid(html, webTemplate.GetAttributeValue<string>("adx_source"), string.Format("{0}:{1}", webTemplateReference.LogicalName, webTemplateReference.Id), output, variables);
		}
		
		internal static string WebTemplate(this HtmlHelper html, EntityReference webTemplateReference, Context context)
		{
			if (webTemplateReference == null) throw new ArgumentNullException("webTemplateReference");

			using (var output = new StringWriter())
			{
				RenderWebTemplate(html, webTemplateReference, output, context);
				return output.ToString();
			}
		}

		internal static void RenderWebTemplate(this HtmlHelper html, EntityReference webTemplateReference, TextWriter output, Context context)
		{
			if (webTemplateReference == null) throw new ArgumentNullException("webTemplateReference");

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Rendering template {0}:{1}", webTemplateReference.LogicalName, webTemplateReference.Id));

			var webTemplate = FetchWebTemplate(html, webTemplateReference.Id);
			if (webTemplate == null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Template {0}:{1} not found.", webTemplateReference.LogicalName, webTemplateReference.Id));
				return;
			}

			InternalRenderLiquid(webTemplate.GetAttributeValue<string>("adx_source"), string.Format("{0}:{1}", webTemplateReference.LogicalName, webTemplateReference.Id), output, context);
		}

		/// <summary>
		/// Fetches a web template with the given ID. Only attribute fetched is "adx_source". If none is found, null will be returned.
		/// </summary>
		/// <param name="html">Html context.</param>
		/// <param name="webTemplateId">ID of the web template to fetch.</param>
		/// <returns>Fetched web template entity.</returns>
		private static Entity FetchWebTemplate(HtmlHelper html, Guid webTemplateId)
		{
			var fetch = new Fetch
			{
				Entity = new FetchEntity("adx_webtemplate")
				{
					Attributes = new[] { new FetchAttribute("adx_source") },
					Filters = new[]
					{
						new Services.Query.Filter
						{
							Conditions = new[]
							{
								new Services.Query.Condition("adx_webtemplateid", ConditionOperator.Equal, webTemplateId),
								new Services.Query.Condition("statecode", ConditionOperator.Equal, 0)
							}
						}
					}
				}
			};

			var portalOrgService = html.ViewContext.HttpContext.GetOrganizationService();
			var webTemplate = portalOrgService.RetrieveSingle(fetch);
			return webTemplate;
		}

		/// <summary>
		/// Render a Liquid template source string.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		/// <returns>Source string with Liquid tags and variables rendered.</returns>
		public static string Liquid(this HtmlHelper html, IHtmlString source, IDictionary<string, object> variables = null)
		{
			return Liquid(html, source.ToString(), variables);
		}

		/// <summary>
		/// Render a Liquid template source string.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		/// <returns>Source string with Liquid tags and variables rendered.</returns>
		public static string Liquid(this HtmlHelper html, string source, IDictionary<string, object> variables = null)
		{
			using (var output = new StringWriter())
			{
				RenderLiquid(html, source, null, output, variables);
				return output.ToString();
			}
		}

		internal static string Liquid(this HtmlHelper html, string source, Context context)
		{
			using (var output = new StringWriter())
			{
				InternalRenderLiquid(source, null, output, context);
				return output.ToString();
			}
		}
		
		/// <summary>
		/// Render a Liquid template source string to the current output stream.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		public static void RenderLiquid(this HtmlHelper html, IHtmlString source, IDictionary<string, object> variables = null)
		{
			RenderLiquid(html, source.ToString(), variables);
		}

		/// <summary>
		/// Render a Liquid template source string to the current output stream.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		public static void RenderLiquid(this HtmlHelper html, string source, IDictionary<string, object> variables = null)
		{
			RenderLiquid(html, source, null, html.ViewContext.Writer, variables);
		}

		/// <summary>
		/// Render a Liquid template source string to a given output stream.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="output">Output to which rendered Liquid will be written.</param>
		/// <param name="variables">Named variable values to include in the global rendering scope.</param>
		public static void RenderLiquid(this HtmlHelper html, string source, TextWriter output, object variables)
		{
			RenderLiquid(html, source, null, output, Hash.FromAnonymousObject(variables));
		}

		/// <summary>
		/// Render a Liquid template source string to a given output stream.
		/// </summary>
		/// <param name="html"></param>
		/// <param name="source">The Liquid template source string.</param>
		/// <param name="sourceIdentifier">For telemetry purposes, optional string identifying what's being rendered.</param>
		/// <param name="output">Output to which rendered Liquid will be written.</param>
		/// <param name="variables">Optional named variable values to include in the global rendering scope.</param>
		public static void RenderLiquid(this HtmlHelper html, string source, string sourceIdentifier, TextWriter output, IDictionary<string, object> variables = null)
		{
			if (string.IsNullOrEmpty(source))
			{
				return;
			}

			if (!html.BooleanSetting("Liquid/Enabled").GetValueOrDefault(true))
			{
				output.Write(source);
				return;
			}

			var environment = html.GetLiquidEnvironment();
			var localVariables = Hash.FromDictionary(environment.Globals);

			if (variables != null)
			{
				localVariables.Merge(variables);
			}

			// Save a reference to this HtmlHelper to the liquid context so that any child "Donut Drops" can 
			// also access the same custom ViewBag information like "ViewSupportsDonuts".
			var registers = Hash.FromDictionary(environment.Registers);
			registers["htmlHelper"] = html;
			var context = new Context(new List<Hash> { localVariables }, new Hash(), registers, ErrorsOutputMode.Display, 0, System.Globalization.CultureInfo.CurrentCulture, System.Threading.CancellationToken.None);

			InternalRenderLiquid(source, sourceIdentifier, output, context);
		}

		/// <summary>
		/// Actually use DotLiquid to render a liquid string into output.
		/// </summary>
		/// <param name="source">Liquid source to render.</param>
		/// <param name="sourceIdentifier">For telemetry purposes, optional string identifying what's being rendered.</param>
		/// <param name="output">TextWriter to render output to.</param>
		/// <param name="context">DotLiquid context.</param>
		private static void InternalRenderLiquid(string source, string sourceIdentifier, TextWriter output, Context context)
		{
			Template template;
			if (!string.IsNullOrEmpty(sourceIdentifier))
			{
				sourceIdentifier = string.Format(" ({0})", sourceIdentifier);
			}

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.LiquidExtension, PerformanceMarkerArea.Liquid, PerformanceMarkerTagName.LiquidSourceParsed))
				{
					template = Template.Parse(source);
				}
			}
			catch (SyntaxException e)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("Liquid parse error{0}: {1}", sourceIdentifier, e.ToString()));
				output.Write(e.Message);
				return;
			}
			
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Rendering Liquid{0}", sourceIdentifier));

			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.LiquidExtension, PerformanceMarkerArea.Liquid, PerformanceMarkerTagName.RenderLiquid))
			{
				template.Render(output, RenderParameters.FromContext(context, System.Globalization.CultureInfo.CurrentCulture));
			}

			foreach (var error in template.Errors)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format("Liquid rendering error{0}: {1}", sourceIdentifier, error.ToString()));
			}
		}
	}
}
