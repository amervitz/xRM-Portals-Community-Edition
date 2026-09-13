/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Configuration;
using Microsoft.Xrm.Client.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// A collection of <see cref="SignUpAttributeElement"/> objects.
	/// </summary>
	[ConfigurationCollection(typeof(SignUpAttributeElement))]
	public sealed class SignUpAttributeElementCollection : ConfigurationElementCollection<SignUpAttributeElement>
	{
		/// <summary>
		/// The name of the element collection.
		/// </summary>
		public const string Name = "attributes";

		/// <summary>
		/// The name of the element collection.
		/// </summary>
		public override string CollectionName
		{
			get { return Name; }
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			var pi = element.GetType().GetProperty("LogicalName");
			return pi.GetValue(element, null);
		}
	}
}
