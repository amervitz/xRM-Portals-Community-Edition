/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// Arguments for the configuration creation event.
	/// </summary>
	public class IdentityModelSectionCreatedEventArgs : EventArgs
	{
		public IdentityModelSection Configuration { get; set; }
	}
}
