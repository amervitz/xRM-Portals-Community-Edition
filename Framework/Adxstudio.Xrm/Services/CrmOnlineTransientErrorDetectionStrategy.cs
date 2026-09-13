/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using Polly;
using Adxstudio.Xrm.Threading;

namespace Adxstudio.Xrm.Services
{
	public class CrmOnlineTransientErrorDetectionStrategy
	{
		public virtual bool IsTransient(Exception ex)
		{
			return false;
		}
	}
}
