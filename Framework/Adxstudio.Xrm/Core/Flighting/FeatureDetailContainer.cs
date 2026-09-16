/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Core.Flighting
{
	using System.Collections.Generic;
	using Configuration;

	/// <summary>
	/// The class which holds all the FCBs default values
	/// </summary>
	public sealed class FeatureDetailContainer : IFeatureDetailContainer
	{
		public FeatureDetailContainer()
		{
			this.InitializeFeatureMetadata();
		}

		public Dictionary<string, IFeatureDetail> Features { get; set; } = new Dictionary<string, IFeatureDetail>();

		/// <summary>
		/// Creates feature detail
		/// </summary>
		/// <param name="name"></param>
		/// <param name="isEnabled"></param>
		/// <param name="location"></param>
		/// <returns></returns>
		private IFeatureDetail Feature(string name, bool isEnabled, FeatureLocation location)
		{
			return new FeatureDetail(name, isEnabled, location);
		}

		/// <summary>
		/// Adds a feature set for FeatureLocation = Global
		/// </summary>
		/// <param name="feature"></param>
		/// <param name="isEnabled"></param>
		private void AddGlobalFeature(string feature, bool isEnabled)
		{
			this.Features.Add(feature, this.Feature(feature, isEnabled, FeatureLocation.Global));
		}

		/// <summary>
		/// Default list of FCBs and their details
		/// </summary>
		void InitializeFeatureMetadata()
		{
			this.AddGlobalFeature(FeatureNames.Web2Case, true);
			this.AddGlobalFeature(FeatureNames.Feedback, true);
			this.AddGlobalFeature(FeatureNames.EventHubCacheInvalidation, true);
			this.AddGlobalFeature(FeatureNames.Categories, true);
			this.AddGlobalFeature(FeatureNames.TelemetryFeatureUsage, true);
			this.AddGlobalFeature(FeatureNames.PortalFacetedNavigation, true);
			this.AddGlobalFeature(FeatureNames.CmsEnabledSearching, true);
			this.AddGlobalFeature(FeatureNames.EntityPermissionFetchUnionHint, true);
			this.AddGlobalFeature(FeatureNames.PortalAllowStaleData, "PortalAllowStaleData".ResolveAppSetting().ToBoolean().GetValueOrDefault());
			this.AddGlobalFeature(FeatureNames.WebProxyClientFailover, "PortalWebProxyClientFailover".ResolveAppSetting().ToBoolean().GetValueOrDefault());

			this.AddGlobalFeature(FeatureNames.CALProductSearchPostFiltering, false);
		}
	}
}
