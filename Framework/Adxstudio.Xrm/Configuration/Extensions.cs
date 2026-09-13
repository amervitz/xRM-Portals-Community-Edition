/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Configuration
{
	using System;
	using System.Configuration;

	/// <summary>
	/// Helpers related to configuration.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Returns the appSetting value for the given name.
		/// </summary>
		/// <param name="name">The appSetting name.</param>
		/// <returns>The appSetting value.</returns>
		public static string ResolveAppSetting(this string name)
		{
			return ConfigurationManager.AppSettings[name];
		}

		/// <summary>
		/// Converts a <see cref="string"/> to <see cref="bool"/>.
		/// </summary>
		/// <param name="text">The boolean as a string.</param>
		/// <returns>The converted boolean value if successful; otherwise, null.</returns>
		public static bool? ToBoolean(this string text)
		{
			bool result;

			return bool.TryParse(text, out result) ? result : (bool?)null;
		}

		/// <summary>
		/// Converts a <see cref="string"/> to <see cref="TimeSpan"/>.
		/// </summary>
		/// <param name="text">The TimeSpan as a string.</param>
		/// <returns>The converted TimeSpan value if successful; otherwise, null.</returns>
		public static TimeSpan? ToTimeSpan(this string text)
		{
			TimeSpan result;

			return TimeSpan.TryParse(text, out result) ? result : (TimeSpan?)null;
		}
	}
}
