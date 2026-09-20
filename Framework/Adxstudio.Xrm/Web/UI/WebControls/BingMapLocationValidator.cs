/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.ComponentModel;
using System.Web.UI;
using System.Web.UI.WebControls;
using Adxstudio.Xrm.Net;

namespace Adxstudio.Xrm.Web.UI.WebControls
{
	/// <summary>
	/// A validator control to validate if a location can be resolved by a query of the Bing Map REST API.
	/// </summary>
	[DefaultProperty("BingMapRestUrl")]
	[ToolboxData("<{0}:BingMapLocationValidator runat=server></{0}:BingMapLocationValidator>")]
	public class BingMapLocationValidator : BaseValidator
	{
		/// <summary>
		/// The URL for making calls to the Bing Maps REST Services
		/// </summary>
		[Description("The URL for making calls to the Bing Maps REST Services.")]
		public string BingMapRestUrl { get; set; }

		/// <summary>
		/// The Bing Maps Key to use for authenticating the request.
		/// </summary>
		[Description("The Bing Maps Key to use for authenticating the request.")]
		public string BingMapKey { get; set; }

		/// <summary>
		/// (Optional) The user’s current position. A point on the earth specified as a latitude and longitude. When you specify this parameter, the user’s location is taken into account and the results returned may be more relevant to the user. Example: userLocation=51.504360719046616,-0.12600176611298197
		/// </summary>
		[Description("(Optional) The user’s current position. A point on the earth specified as a latitude and longitude. When you specify this parameter, the user’s location is taken into account and the results returned may be more relevant to the user. Example: userLocation=51.504360719046616,-0.12600176611298197")]
		public string UserLocation { get; set; }

		/// <summary>
		/// (Optional) Specifies to include the neighborhood with the address information the response when it is available. One of the following values: 1=Include neighborhood information when available, 0=[default]Do not include neighborhood information.
		/// </summary>
		[Description("(Optional) Specifies to include the neighborhood with the address information the response when it is available. One of the following values: 1=Include neighborhood information when available, 0=[default]Do not include neighborhood information.")]
		public int? IncludeNeighborhood { get; set; }

		protected override bool EvaluateIsValid()
		{
			if (string.IsNullOrWhiteSpace(BingMapRestUrl))
			{
				throw new ArgumentNullException(BingMapRestUrl);
			}

			if (string.IsNullOrWhiteSpace(BingMapKey))
			{
				throw new ArgumentNullException(BingMapKey);
			}

			var query = GetControlValidationValue(ControlToValidate);

			var bingMapLookup = new BingMapLookup(BingMapRestUrl, BingMapKey, UserLocation, IncludeNeighborhood ?? 0);

			return bingMapLookup.IsLocationValid(query);
		}
	}
}
