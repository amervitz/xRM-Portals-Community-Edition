/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;

namespace Adxstudio.Xrm.Web.UI.EntityList.OData
{
	/// <summary>
	/// An implementation of <see cref="IODataRoutingConvention"/> that handles entity sets.
	/// </summary>
	public class EntitySetODataRoutingConvention : IODataRoutingConvention
	{
		private readonly string _controllerName;

		/// <summary>
		/// EntitySetODataRoutingConvention constructor
		/// </summary>
		/// <param name="controllerName">Name of the controller that handles the OData route requested</param>
		public EntitySetODataRoutingConvention(string controllerName = "ODataEntitySet")
		{
			_controllerName = controllerName;
		}

		public string SelectController(ODataPath odataPath, HttpRequestMessage request)
		{
			var firstSegment = odataPath.Segments.FirstOrDefault();

			if (firstSegment is Microsoft.OData.UriParser.EntitySetSegment)
			{
				return _controllerName;
			}

			return null;
		}

		public string SelectAction(ODataPath odataPath, HttpControllerContext controllerContext, ILookup<string, HttpActionDescriptor> actionMap)
		{
			return null;
		}
	}
}
