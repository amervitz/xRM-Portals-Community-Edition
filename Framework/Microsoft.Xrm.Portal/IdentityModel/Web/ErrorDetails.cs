/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Runtime.Serialization;

namespace Microsoft.Xrm.Portal.IdentityModel.Web
{
	[DataContract]
	public class ErrorStatus
	{
		[DataMember(Name = "errorCode")]
		public string ErrorCode { get; set; }

		[DataMember(Name = "errorMessage")]
		public string ErrorMessage { get; set; }
	}

	[DataContract]
	public class ErrorDetails
	{
		[DataMember(Name = "context")]
		public string Context { get; set; }

		[DataMember(Name = "httpReturnCode")]
		public int HttpReturnCode { get; set; }

		[DataMember(Name = "identityProvider")]
		public string IdentityProvider { get; set; }

		[DataMember(Name = "timeStamp")]
		public DateTime TimeStamp { get; set; }

		[DataMember(Name = "traceId")]
		public Guid TraceId { get; set; }

		[DataMember(Name = "errors")]
		public ErrorStatus[] Errors { get; set; }
	}
}
