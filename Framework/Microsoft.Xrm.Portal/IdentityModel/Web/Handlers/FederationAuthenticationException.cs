/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Runtime.Serialization;
using System.IdentityModel.Services;

namespace Microsoft.Xrm.Portal.IdentityModel.Web.Handlers
{
	public enum FederationAuthenticationErrorReason
	{
		MissingChallengeAnswer,
	}

	[Serializable]
	public class FederationAuthenticationException : FederationException
	{
		public FederationAuthenticationErrorReason? Reason { get; private set; }

		public FederationAuthenticationException()
		{
		}

		public FederationAuthenticationException(string message)
			: base(message)
		{
		}

		public FederationAuthenticationException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		protected FederationAuthenticationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		public FederationAuthenticationException(FederationAuthenticationErrorReason reason)
		{
			Reason = reason;
		}
	}
}
