/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.Cms
{
	using System;
	using System.Text;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Crm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Query;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Services.Query;

	/// <summary>
	/// Class for Crm Portal Solutions Details
	/// </summary>
	public class PortalSolutions : IDisposable
	{
		/// <summary>
		/// The Portal solution names
		/// </summary>
		public static class SolutionNames
		{
			/// <summary>
			/// Base soltuion name
			/// </summary>
			public static readonly string BaseSolutionName = GetBaseSolutionName();

			private static string GetBaseSolutionName()
			{
				var configured = System.Configuration.ConfigurationManager.AppSettings["PortalBaseSolution"];
				return string.IsNullOrWhiteSpace(configured) ? "MicrosoftCrmPortalBase" : configured.Trim();
			}

			/// <summary>
			/// Blogs solution name
			/// </summary>
			public static readonly string BlogsSolutionName = "MicrosoftBlogs";

			/// <summary>
			/// Commerce solution name
			/// </summary>
			public static readonly string CommerceSolutionName = "AdxstudioCommerce";

			/// <summary>
			/// Event solution name
			/// </summary>
			public static readonly string EventsSolutionName = "AdxstudioEventManagement";

			/// <summary>
			/// Forums solution name
			/// </summary>
			public static readonly string ForumsSolutionName = "MicrosoftForums";

			/// <summary>
			/// Web forms solution name
			/// </summary>
			public static readonly string WebFormsSolutionName = "MicrosoftWebForms";

			/// <summary>
			/// Ideas solution name
			/// </summary>
			public static readonly string IdeasSolutionName = "MicrosoftIdeas";

			/// <summary>
			/// Complete solution name
			/// </summary>
			public static readonly string CompleteSolutionName = "AdxstudioPortalsComplete";
		}

		/// <summary>
		/// The base solution version
		/// </summary>
		private readonly Version baseSolutionCrmVersion;

		/// <summary>
		/// Returns the base solution version
		/// </summary>
		public Version BaseSolutionCrmVersion
		{
			get { return this.baseSolutionCrmVersion; }
		}

		/// <summary>
		/// All available solutions.
		/// </summary>
		public IDictionary<string, SolutionInfo> Solutions { get; private set; }

		/// <summary>
		/// CRM Version
		/// </summary>
		public Version CrmVersion { get; private set; }

		/// <summary>
		/// Gets the organization base language code.
		/// </summary>
		public int OrganizationBaseLanguageCode { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PortalSolutions" /> class.
		/// </summary>
		/// <param name="context">The context.</param>
		public PortalSolutions(CrmDbContext context)
		{
			this.Solutions = LoadSolutions(context);
			this.CrmVersion = LoadCrmVersion(context);
			this.OrganizationBaseLanguageCode = LoadOrganizationBaseLanguage(context);

			this.baseSolutionCrmVersion = RetrieveSolutionVersion(SolutionNames.BaseSolutionName, this.Solutions);

			this.TraceSolutions();
		}

		/// <summary>
		/// Loads CRM solutions
		/// </summary>
		/// <param name="solutionName">Name of the solution.</param>
		/// <param name="solutions">All solutions.</param>
		/// <returns> Returns crm version of the solution.</returns>
		private static Version RetrieveSolutionVersion(string solutionName, IDictionary<string, SolutionInfo> solutions)
		{
			SolutionInfo solution;

			if (solutions.TryGetValue(solutionName, out solution))
			{
				return solution.SolutionVersion;
			}

			throw new Exception(string.Format("Solution with solution name: {0}, doesn't exist.", solutionName));
		}

		/// <summary>
		/// Loads CRM solutions
		/// </summary>
		/// <param name="context">The context.</param>
		/// <returns>All solutions.</returns>
		private static IDictionary<string, SolutionInfo> LoadSolutions(CrmDbContext context)
		{
			var fetch = new Fetch
			{
				Entity = new FetchEntity("solution", new[] { "uniquename", "version", "installedon" })
				{
					Links = new[]
					{
						new Link
						{
							Name = "publisher",
							FromAttribute = "publisherid",
							ToAttribute = "publisherid",
							Type = JoinOperator.Inner,
							Filters = new[]
							{
								new Filter
								{
									Type = LogicalOperator.Or,
									Conditions = new[] // fetch only adx solutions
									{
										new Condition("uniquename", ConditionOperator.Equal, "microsoftdynamics"),
										new Condition("uniquename", ConditionOperator.Equal, "adxstudio")
									}
								}
							}
						}
					}
				}
			};

			var result = context.Service.RetrieveMultiple(fetch);

			var solutions = result.Entities.ToDictionary(
				solution => solution.GetAttributeValue<string>("uniquename"),
				solution => new SolutionInfo(
					solution.GetAttributeValue<string>("uniquename"),
					Version.Parse(solution.GetAttributeValue<string>("version")),
					solution.GetAttributeValue<DateTime>("installedon")));

			return solutions;
		}

		/// <summary>
		/// Loads CRM version
		/// </summary>
		/// <param name="context">Crm Db context</param>
		/// <returns>CRM version</returns>
		private static Version LoadCrmVersion(CrmDbContext context)
		{
			Version version = null;

			try
			{
				var versionResponse = (RetrieveVersionResponse)context.Service.Execute(new RetrieveVersionRequest());
				version = new Version(versionResponse.Version);
			}
			catch (Exception e)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Exception, string.Format("Not able to read solution information{0}{1}", Environment.NewLine, e));
			}

			return version;
		}

		/// <summary> The load organization base lanugage. </summary>
		/// <param name="context"> The context. </param>
		/// <returns> The organization base language code </returns>
		private static int LoadOrganizationBaseLanguage(CrmDbContext context)
		{
			var result = 0;

			try
			{
				var fetch = new Fetch { Entity = new FetchEntity("organization", new[] { "languagecode" }) };

				result = context.Service.RetrieveSingle(fetch).GetAttributeValue<int>("languagecode");
			}
			catch (Exception e)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Exception, string.Format("Failed to retrieve the base language of the organization:{0}{1}", Environment.NewLine, e));
			}

			return result;
		}

		/// <summary>
		/// Logs crm solutions to adx trace
		/// </summary>
		private void TraceSolutions()
		{
			var stringBuilder = new StringBuilder();

			var tableDifinition = new Dictionary<string, Func<SolutionInfo, string>>
			{
				{ "Unique name", s => s.Name },
				{ "Version", s => s.SolutionVersion.ToString() },
				{ "Installed on", s => s.InstalledOn.ToString() }
			};

			var columnFormat = new Dictionary<string, string>();

			// Calcule width of each column and write header
			foreach (var columnDefinition in tableDifinition)
			{
				var maxWidth = this.Solutions.Values.Max(solution => tableDifinition[columnDefinition.Key](solution).Length);
				var format = string.Format("{{0, -{0}}}", maxWidth);
				columnFormat[columnDefinition.Key] = format;

				stringBuilder.AppendFormat(format, columnDefinition.Key);
				stringBuilder.Append(" ");
			}
			stringBuilder.AppendLine();

			// Render rows
			foreach (var solution in this.Solutions.Values)
			{
				foreach (var columnDefinition in tableDifinition)
				{
					stringBuilder.AppendFormat(columnFormat[columnDefinition.Key], columnDefinition.Value(solution));
					stringBuilder.Append(" ");
				}
				stringBuilder.AppendLine();
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Installed portal solutions on CRM {0}:{1}{2}", this.CrmVersion, Environment.NewLine, stringBuilder));
		}

		/// <summary>
		/// Dispose method
		/// </summary>
		void IDisposable.Dispose()
		{
		}
	}
}
