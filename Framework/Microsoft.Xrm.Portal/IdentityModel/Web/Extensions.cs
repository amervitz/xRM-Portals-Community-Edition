/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Web;
using System.Web.Script.Serialization;

namespace Microsoft.Xrm.Portal.IdentityModel.Web
{
	public static class Extensions
	{
		public static ErrorDetails GetSignInResponseError(this HttpRequest request)
		{
			var details = request["ErrorDetails"];

			if (!string.IsNullOrWhiteSpace(details))
			{
				var serializer = new JavaScriptSerializer();
				var error = serializer.Deserialize<ErrorDetails>(details);

				return error;
			}

			return null;
		}
	}
}
