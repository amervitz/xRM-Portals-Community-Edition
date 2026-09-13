/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.IdentityModel.ActiveDirectory
{
	using System;
	using Microsoft.Identity.Client;

	/// <summary>
	/// A portal specific container for <see cref="MsalServiceException"/>.
	/// </summary>
	public class PortalAdalServiceException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PortalAdalServiceException" /> class.
		/// </summary>
		/// <param name="message">The error message.</param>
		public PortalAdalServiceException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PortalAdalServiceException" /> class.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		public PortalAdalServiceException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
