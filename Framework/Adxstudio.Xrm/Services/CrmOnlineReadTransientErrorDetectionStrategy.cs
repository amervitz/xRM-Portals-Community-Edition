/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using Polly;
using Adxstudio.Xrm.Threading;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Services
{
	public class CrmOnlineReadTransientErrorDetectionStrategy
	{
		private static readonly IEnumerable<string> _transientWebExceptions = new[]
		{
			"The request was aborted: The request was canceled.",
			"The underlying connection was closed: An unexpected error occurred on a receive.",
			"The underlying connection was closed: A connection that was expected to be kept alive was closed by the server.",
		};

		public virtual bool IsTransient(Exception ex)
		{
			if (IsTransientOrganizationServiceFaultException(ex)
				|| IsTransientTargetInvocationException(ex)
				|| IsTransientCommunicationException(ex)
				|| IsTransientWebException(ex)
				|| ex is SqlException
				|| ex is TimeoutException)
			{
				return true;
			}

			return false;
		}

		private bool IsTransientOrganizationServiceFaultException(Exception ex)
		{
			var fe = ex as FaultException<OrganizationServiceFault>;
			return fe != null && IsTransient(fe.InnerException);
		}

		private static bool IsTransientTargetInvocationException(Exception ex)
		{
			var tie = ex as TargetInvocationException;
			return tie != null && IsTransientCommunicationException(tie.InnerException);
		}

		private static bool IsTransientCommunicationException(Exception ex)
		{
			var ce = ex as CommunicationException;
			return ce != null && IsTransientWebException(ce.InnerException);
		}

		private static bool IsTransientWebException(Exception ex)
		{
			var we = ex as WebException;
			return we != null && _transientWebExceptions.Contains(we.Message);
		}
	}
}
