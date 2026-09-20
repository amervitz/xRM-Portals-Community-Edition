/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Core.Flighting
{
	/// <summary>
	/// Includes the list of feature FCB names. you need to create a constant for the FCB you create ex: public const string KnowledgeArticle = "FCB.KnowledgeArticle";
	/// </summary>
	public static class FeatureNames
	{
		public const string Feedback = "FCB.Feedback";
		public const string EventHubCacheInvalidation = "FCB.EventHubCacheInvalidation";
		public const string PortalFacetedNavigation = "FCB.PortalFacetedNavigation";
		public const string TelemetryFeatureUsage = "FCB.TelemetryFeatureUsage";
		public const string CmsEnabledSearching = "FCB.CmsEnabledSearching";
		public const string CALProductSearchPostFiltering = "FCB.CALProductSearchPostFiltering";
		public const string EntityPermissionFetchUnionHint = "FCB.EntityPermissionFetchUnionHint";
		public const string PortalAllowStaleData = "FCB.PortalAllowStaleData";
		public const string WebProxyClientFailover = "FCB.WebProxyClientFailover";
	}
}
