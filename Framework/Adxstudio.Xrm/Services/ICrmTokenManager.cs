/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Threading.Tasks;
	using Microsoft.Identity.Client;

	/// <summary>
	///  ICrmTokenManager interface
	/// </summary>
	public interface ICrmTokenManager
	{
		/// <summary>
		/// Retrieves access token on behalf of a user.
		/// </summary>
		/// <param name="authorizationCode">User's access code.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>The token.</returns>
		Task<string> GetTokenAsync(string authorizationCode = null, Func<AuthenticationResult, Exception> test = null);

		/// <summary>
		/// Retrieves access token on behalf of a user.
		/// </summary>
		/// <param name="authorizationCode">User's access code.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>The token.</returns>
		string GetToken(string authorizationCode = null, Func<AuthenticationResult, Exception> test = null);

		/// <summary>
		/// Resets the manager.
		/// </summary>
		void Reset();
	}
}
