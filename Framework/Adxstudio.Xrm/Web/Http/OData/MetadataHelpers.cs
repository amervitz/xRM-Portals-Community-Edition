/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using Microsoft.OData.Edm;
using Microsoft.Xrm.Sdk.Metadata;

namespace Adxstudio.Xrm.Web.Http.OData
{
	/// <summary>
	/// Helper class for working with OData typeless contructs.
	/// </summary>
	public static class MetadataHelpers
	{
		/// <summary>
		/// Get the EdmPrimitiveTypeKind equivalent for the AttributeType for a given AttributeMetadata
		/// </summary>
		/// <param name="attributeMetadata">AttributeMetadata</param>
		/// <returns>For primitive types, EdmPrimitiveTypeKind is returned, otherwise null is returned for complex types.</returns>
		public static EdmPrimitiveTypeKind? GetEdmPrimitiveTypeKindFromAttributeMetadata(AttributeMetadata attributeMetadata)
		{
			if (attributeMetadata == null || attributeMetadata.AttributeType == null)
			{
				return EdmPrimitiveTypeKind.None;
			}

			switch (attributeMetadata.AttributeType)
			{
				case AttributeTypeCode.BigInt:
					return EdmPrimitiveTypeKind.Int64;
				case AttributeTypeCode.Boolean:
					return EdmPrimitiveTypeKind.Boolean;
				case AttributeTypeCode.Customer:
					return null;
				case AttributeTypeCode.DateTime:
					return EdmPrimitiveTypeKind.DateTimeOffset;
				case AttributeTypeCode.Decimal:
					return EdmPrimitiveTypeKind.Decimal;
				case AttributeTypeCode.Double:
					return EdmPrimitiveTypeKind.Double;
				case AttributeTypeCode.EntityName:
					return EdmPrimitiveTypeKind.String;
				case AttributeTypeCode.Integer:
					return EdmPrimitiveTypeKind.Int32;
				case AttributeTypeCode.Lookup:
					return null;
				case AttributeTypeCode.Memo:
					return EdmPrimitiveTypeKind.String;
				case AttributeTypeCode.Money:
					return EdmPrimitiveTypeKind.Decimal;
				case AttributeTypeCode.Owner:
					return null;
				case AttributeTypeCode.PartyList:
					return null;
				case AttributeTypeCode.Picklist:
					return null;
				case AttributeTypeCode.State:
					return null;
				case AttributeTypeCode.Status:
					return null;
				case AttributeTypeCode.String:
					return EdmPrimitiveTypeKind.String;
				case AttributeTypeCode.Uniqueidentifier:
					return EdmPrimitiveTypeKind.Guid;
				default:
					return EdmPrimitiveTypeKind.None;
			}
		}
	}
}
