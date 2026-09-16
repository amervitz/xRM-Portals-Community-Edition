/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm
{
    /// <summary>
    /// An enum of features which will have telemetry emitted.
    /// </summary>
    /// <remarks>
    /// Values are emitted as the numeric payload of ETW events, so they are assigned
    /// explicitly to keep them stable. Value 16 previously belonged to the removed DCI
    /// category and is intentionally left unused. Append new categories rather than
    /// inserting them.
    /// </remarks>
    public enum FeatureTraceCategory
    {
        Authentication = 0,
        Blog = 1,
        Case = 2,
        Event = 3,
        Feedback = 4,
        Idea = 5,
        Issue = 6,
        KnowledgeArticle = 7,
        Note = 8,
        ProductBrand = 9,
        Product = 10,
        ProductReview = 11,
        Forum = 12,
        SessionStart = 13,
        Comments = 14,
        Forms = 15,
        Search = 17
    }
}
