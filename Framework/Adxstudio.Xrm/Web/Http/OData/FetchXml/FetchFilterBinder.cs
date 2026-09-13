/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData.Query;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Services.Query;
using Microsoft.OData.Edm;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Web.Http.OData.FetchXml
{
	/// <summary>
	/// Provides operations to translate <see cref="Microsoft.AspNet.OData.Query.ODataQueryOptions"/> <see cref="Microsoft.AspNet.OData.Query.FilterQueryOption"/> to equivalent filters in FetchXml.
	/// </summary>
	public class FetchFilterBinder
	{
		private bool _applyLogicalNegation;
		/// <summary>
		/// Bind the <see cref="FilterQueryOption"/> to a FetchXml Filter
		/// </summary>
		/// <param name="filterQueryOption"><see cref="FilterQueryOption"/></param>
		/// <returns><see cref="Filter"/></returns>
		public static Filter BindFilterQueryOption(FilterQueryOption filterQueryOption)
		{
			return new FetchFilterBinder().BindFilterClause(filterQueryOption.FilterClause);
		}

		protected Filter BindFilterClause(FilterClause filterClause)
		{
			return Bind(filterClause.Expression);
		}

		protected Filter Bind(QueryNode node)
		{
			var singleValueNode = node as SingleValueNode;

			if (singleValueNode != null)
			{
				switch (node.Kind)
				{
					case QueryNodeKind.BinaryOperator:
						return BindBinaryOperatorNode(node as BinaryOperatorNode);

					case QueryNodeKind.Convert:
						return BindConvertNode(node as ConvertNode);

					case QueryNodeKind.ResourceRangeVariableReference:
						return null;

					case QueryNodeKind.NonResourceRangeVariableReference:
						return null;

					case QueryNodeKind.UnaryOperator:
						return BindUnaryOperatorNode(node as UnaryOperatorNode);

					case QueryNodeKind.SingleValueFunctionCall:
						var condition = BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode);
						return new Filter { Conditions = new List<Condition> { condition } };
				}
			}

			throw new NotSupportedException(string.Format("Query Nodes of type {0} aren't supported.", node.Kind));
		}

		private Condition BindSingleValueFunctionCallNode(SingleValueFunctionCallNode singleValueFunctionCallNode)
		{
			switch (singleValueFunctionCallNode.Name)
			{
				case "startswith":
					return BindStartsWith(singleValueFunctionCallNode);
				case "endswith":
					return BindEndsWith(singleValueFunctionCallNode);
				case "contains":
					return BindContains(singleValueFunctionCallNode);
				default:
					throw new NotSupportedException(string.Format("Function call {0} isn't supported.", singleValueFunctionCallNode.Name));
			}
		}

		private Condition BindStartsWith(SingleValueFunctionCallNode singleValueFunctionCallNode)
		{
			var arguments = singleValueFunctionCallNode.Parameters.ToList();
			if (arguments.Count != 2)
			{
				throw new ODataException(string.Format("Invalid {0} function call. The 2 required parameters have not been specified.", singleValueFunctionCallNode.Name));
			}
			var condition = new Condition { Operator = _applyLogicalNegation ? ConditionOperator.NotLike : ConditionOperator.Like };
			var singleValuePropertyAccessNode = arguments[0] as SingleValuePropertyAccessNode;
			if (singleValuePropertyAccessNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid property name must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "first"));
			}
			condition.Attribute = BindPropertyAccessQueryNode(singleValuePropertyAccessNode);
			var constantNode = arguments[1] as ConstantNode;
			if (constantNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid string value must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "second"));
			}
			var value = BindConstantNode(constantNode);
			if (value == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. Null constant not applicable.", singleValueFunctionCallNode.Name));
			}
			condition.Value = string.Format("{0}%", value);
			return condition;
		}

		private Condition BindEndsWith(SingleValueFunctionCallNode singleValueFunctionCallNode)
		{
			var arguments = singleValueFunctionCallNode.Parameters.ToList();
			if (arguments.Count != 2)
			{
				throw new ODataException(string.Format("Invalid {0} function call. The 2 required parameters have not been specified.", singleValueFunctionCallNode.Name));
			}
			var condition = new Condition { Operator = _applyLogicalNegation ? ConditionOperator.NotLike : ConditionOperator.Like };
			var singleValuePropertyAccessNode = arguments[0] as SingleValuePropertyAccessNode;
			if (singleValuePropertyAccessNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid property name must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "first"));
			}
			condition.Attribute = BindPropertyAccessQueryNode(singleValuePropertyAccessNode);
			var constantNode = arguments[1] as ConstantNode;
			if (constantNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid string value must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "second"));
			}
			var value = BindConstantNode(constantNode);
			if (value == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. Null constant not applicable.", singleValueFunctionCallNode.Name));
			}
			condition.Value = string.Format("%{0}", value);
			return condition;
		}

		private Condition BindContains(SingleValueFunctionCallNode singleValueFunctionCallNode)
		{
			var arguments = singleValueFunctionCallNode.Parameters.ToList();
			if (arguments.Count != 2)
			{
				throw new ODataException(string.Format("Invalid {0} function call. The 2 required parameters have not been specified.", singleValueFunctionCallNode.Name));
			}
			var condition = new Condition { Operator = _applyLogicalNegation ? ConditionOperator.NotLike : ConditionOperator.Like };
			var singleValuePropertyAccessNode = arguments[0] as SingleValuePropertyAccessNode;
			if (singleValuePropertyAccessNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid property name must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "first"));
			}
			condition.Attribute = BindPropertyAccessQueryNode(singleValuePropertyAccessNode);
			var constantNode = arguments[1] as ConstantNode;
			if (constantNode == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. A valid string value must be specified as the {1} parameter.", singleValueFunctionCallNode.Name, "second"));
			}
			var value = BindConstantNode(constantNode);
			if (value == null)
			{
				throw new ODataException(string.Format("Invalid {0} function call. Null constant not applicable.", singleValueFunctionCallNode.Name));
			}
			condition.Value = string.Format("%{0}%", value);
			return condition;
		}

		private Filter BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode)
		{
			switch (unaryOperatorNode.OperatorKind)
			{
				case UnaryOperatorKind.Negate:
					throw new NotSupportedException("The Negate arithmetic operator isn't supported.");
				case UnaryOperatorKind.Not:
					_applyLogicalNegation = !_applyLogicalNegation;
					break;
				default:
					throw new NotSupportedException("Unknown UnaryOperatorKind.");
			}

			try { return Bind(unaryOperatorNode.Operand); }
			finally { _applyLogicalNegation = !_applyLogicalNegation; }
		}

		private string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
		{
			if (singleValuePropertyAccessNode.Source.TypeReference.Definition.TypeKind == EdmTypeKind.Complex)
			{
				var type = singleValuePropertyAccessNode.Source.TypeReference.Definition as EdmComplexType;

				if (type == null)
				{
					return singleValuePropertyAccessNode.Property.Name;
				}

				switch (type.Name)
				{
					case "OptionSet":
					case "EntityReference":
						if (singleValuePropertyAccessNode.Property.Name == "Name")
						{
							throw new ODataException(string.Format("Equality comparison on Complex type {0} property {1} isn't supported.", type.Name, singleValuePropertyAccessNode.Property.Name));
						}
						var sourceSingleValuePropertyAccessNode = singleValuePropertyAccessNode.Source as SingleComplexNode;
						if (sourceSingleValuePropertyAccessNode != null)
						{
							return sourceSingleValuePropertyAccessNode.Property.Name;
						}
						break;
				}
			}

			return singleValuePropertyAccessNode.Property.Name;
		}

		private object BindConstantNode(ConstantNode constantNode)
		{
			return constantNode.Value is DateTimeOffset ? ((DateTimeOffset)constantNode.Value).UtcDateTime : constantNode.Value;
		}

		private Filter BindConvertNode(ConvertNode convertNode)
		{
			return Bind(convertNode.Source);
		}

		private Filter BindBinaryOperatorNode(BinaryOperatorNode binaryOperatorNode)
		{
			var filter = new Filter();

			switch (binaryOperatorNode.OperatorKind)
			{
				case BinaryOperatorKind.And:
					filter.Type = !_applyLogicalNegation ? LogicalOperator.And : LogicalOperator.Or;
					break;
				case BinaryOperatorKind.Or:
					filter.Type = !_applyLogicalNegation ? LogicalOperator.Or : LogicalOperator.And;
					break;
				default:
					filter.Type = !_applyLogicalNegation ? LogicalOperator.And : LogicalOperator.Or;
					break;
			}

			if (binaryOperatorNode.Left is SingleValuePropertyAccessNode)
			{
				filter.Conditions = new List<Condition> { CreateBinaryCondition(binaryOperatorNode) };
			}
			else
			{
				var left = Bind(binaryOperatorNode.Left);
				var right = Bind(binaryOperatorNode.Right);

				if (filter.Filters == null)
				{
					filter.Filters = new List<Filter> { left, right };
				}
				else
				{
					filter.Filters.Add(left);
					filter.Filters.Add(right);
				}
			}

			return filter;
		}

		private Condition CreateBinaryCondition(BinaryOperatorNode binaryOperatorNode)
		{
			if (binaryOperatorNode.OperatorKind == BinaryOperatorKind.And || binaryOperatorNode.OperatorKind == BinaryOperatorKind.Or)
			{
				throw new ODataException(string.Format("A binary condition cannot be created when OperatorKind is of type {0}", binaryOperatorNode.OperatorKind));
			}
			var condition = new Condition();
			var singleValuePropertyAccessNode = binaryOperatorNode.Left as SingleValuePropertyAccessNode;
			condition.Attribute = BindPropertyAccessQueryNode(singleValuePropertyAccessNode);
			object value;
			if (binaryOperatorNode.Right is ConvertNode)
			{
				var convertNode = binaryOperatorNode.Right as ConvertNode;
				var constantNode = convertNode.Source;
				value = BindConstantNode(constantNode as ConstantNode);
			}
			else
			{
				value = BindConstantNode(binaryOperatorNode.Right as ConstantNode);
			}
			if (value == null)
			{
				// OData has a null literal for equality comparison, FetchXml requires either that the Null or NotNull operator is specified without a value parameter on the condition
				switch (binaryOperatorNode.OperatorKind)
				{
					case BinaryOperatorKind.Equal:
						condition.Operator = !_applyLogicalNegation ? ConditionOperator.Null : ConditionOperator.NotNull;
						break;
					case BinaryOperatorKind.NotEqual:
						condition.Operator = !_applyLogicalNegation ? ConditionOperator.NotNull : ConditionOperator.Null;
						break;
					default:
						throw new ODataException(string.Format("The operator {0} isn't supported for the null literal. Only equality checks are supported.", binaryOperatorNode.OperatorKind));
				}
			}
			else
			{
				condition.Operator = ToConditionOperator(binaryOperatorNode.OperatorKind);
				condition.Value = value;
			}
			return condition;
		}

		private ConditionOperator ToConditionOperator(BinaryOperatorKind binaryOperator)
		{
			switch (binaryOperator)
			{
				case BinaryOperatorKind.Equal:
					return !_applyLogicalNegation ? ConditionOperator.Equal : ConditionOperator.NotEqual;
				case BinaryOperatorKind.NotEqual:
					return !_applyLogicalNegation ? ConditionOperator.NotEqual : ConditionOperator.Equal;
				case BinaryOperatorKind.GreaterThan:
					return !_applyLogicalNegation ? ConditionOperator.GreaterThan : ConditionOperator.LessEqual;
				case BinaryOperatorKind.GreaterThanOrEqual:
					return !_applyLogicalNegation ? ConditionOperator.GreaterEqual : ConditionOperator.LessThan;
				case BinaryOperatorKind.LessThan:
					return !_applyLogicalNegation ? ConditionOperator.LessThan : ConditionOperator.GreaterEqual;
				case BinaryOperatorKind.LessThanOrEqual:
					return !_applyLogicalNegation ? ConditionOperator.LessEqual : ConditionOperator.GreaterThan;
				case BinaryOperatorKind.Add:
				case BinaryOperatorKind.Subtract:
				case BinaryOperatorKind.Multiply:
				case BinaryOperatorKind.Divide:
				case BinaryOperatorKind.Modulo:
					throw new NotSupportedException("Arithmetic operators aren't supported.");
				case BinaryOperatorKind.And:
					throw new ODataException("The And operator isn't an applicable equality comparison operator.");
				case BinaryOperatorKind.Or:
					throw new ODataException("The OR operator isn't an applicable equality comparison operator.");
				default:
					throw new NotSupportedException("Unknown operator.");
			}
		}
	}
}
