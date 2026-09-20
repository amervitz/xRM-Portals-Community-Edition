/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.UI.WebForms
{
	/// <summary>
	/// Additional behavior modification logic to augment or override the functionality of form fields that is not possible with CRM’s entity and form metadata.
	/// </summary>
	public static class WebFormMetadata
	{
		/// <summary>
		/// Enumeration of the Web Form Metadata Type Option Set.
		/// </summary>
		public enum Type
		{
			/// <summary>
			/// Metadata is scoped to an attribute.
			/// </summary>
			Attribute = 100000000,
			/// <summary>
			/// Metadata is scoped to a section.
			/// </summary>
			Section = 100000001,
			/// <summary>
			/// Metadata is scoped to a tab.
			/// </summary>
			Tab = 100000002
		}

		/// <summary>
		/// Enumeration of the Web Form Metadata Control Style Option Set.
		/// </summary>
		public enum ControlStyle
		{
			/// <summary>
			/// Render Option Set (Picklist) as Vertical Radio Button List
			/// </summary>
			VerticalRadioButtonList = 100000000,
			/// <summary>
			/// Render Option Set (Picklist) as Horizontal Radio Button List
			/// </summary>
			HorizontalRadioButtonList = 100000001,
			/// <summary>
			/// Render Single Line of Text as Geolocation Lookup Validator
			/// </summary>
			GeolocationLookupValidator = 100000002,
			/// <summary>
			/// Group Whole Number as Constant Sum
			/// </summary>
			ConstantSum = 100000003,
			/// <summary>
			/// Group Whole Number as Rank Order Scale No Ties
			/// </summary>
			RankOrderNoTies = 100000004,
			/// <summary>
			/// Group Whole Number as Rank Order Scale Allow Ties
			/// </summary>
			RankOrderAllowTies = 100000005,
			/// <summary>
			/// Render Option Set (Picklist) as Matrix of Horizontal Radio Button List with labels on top.
			/// </summary>
			MultipleChoiceMatrix = 100000006,
			/// <summary>
			/// Render as part of a group of Two Option (Boolean) controls with a maximum number of selectable choices.
			/// </summary>
			MultipleChoice = 100000007,
			/// <summary>
			/// Group Whole Number as Stack Rank.
			/// </summary>
			StackRank = 100000008,
			/// <summary>
			/// Render a lookup as a dropdown.
			/// </summary>
			LookupDropdown = 756150000
		}

		/// <summary>
		/// Enumeration of the Web Form Metadata Prepopulate Type Option Set.
		/// </summary>
		public enum PrepopulateType
		{
			/// <summary>
			/// Prepopulate field with the specified value.
			/// </summary>
			Value = 100000000,
			/// <summary>
			/// Prepopulate field with today's date.
			/// </summary>
			TodaysDate = 100000001,
			/// <summary>
			/// Prepopulate field with attribute value from the current user's contact record.
			/// </summary>
			CurrentPortalUser = 100000002
		}

		/// <summary>
		/// The position to render the description relative to the field it is associated to.
		/// </summary>
		public enum DescriptionPosition
		{
			/// <summary>
			/// The description will be rendered above the field.
			/// </summary>
			AboveControl = 100000000,
			/// <summary>
			/// The description will be rendered below the field.
			/// </summary>
			BelowControl = 100000001,
			/// <summary>
			/// The description will be rendered above the field's label.
			/// </summary>
			AboveLabel = 100000002,
		}
	}
}
