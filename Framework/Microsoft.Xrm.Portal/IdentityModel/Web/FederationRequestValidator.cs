/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Web;
using System.Web.Util;
using System.IdentityModel.Services;

namespace Microsoft.Xrm.Portal.IdentityModel.Web
{
	/// <summary>
	/// A request validator to accept federated sign-in response messages.
	/// </summary>
	public sealed class FederationRequestValidator : RequestValidator
	{
		protected override bool IsValidRequestString(
			HttpContext context,
			string value,
			RequestValidationSource requestValidationSource,
			string collectionKey,
			out int validationFailureIndex)
		{
			validationFailureIndex = 0;

			if (requestValidationSource == RequestValidationSource.Form
				&& collectionKey.Equals("wresult", StringComparison.Ordinal))
			{
				var message = WSFederationMessage.CreateFromFormPost(new HttpRequestWrapper(context.Request)) as SignInResponseMessage;

				if (message != null)
				{
					return true;
				}
			}

			return base.IsValidRequestString(context, value, requestValidationSource, collectionKey, out validationFailureIndex);
		}
	}
}
