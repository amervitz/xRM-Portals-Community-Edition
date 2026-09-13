using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Adxstudio.Xrm.Threading
{
	/// <summary>Shared retry construction for CRM, filesystem and index operations.</summary>
	public static class RetryPolicies
	{
		public static ResiliencePipeline Create(Func<Exception, bool> isTransient, IEnumerable<TimeSpan> retryDelays)
		{
			if (isTransient == null) throw new ArgumentNullException(nameof(isTransient));
			if (retryDelays == null) throw new ArgumentNullException(nameof(retryDelays));
			var delays = retryDelays.ToArray();
			if (delays.Any(delay => delay < TimeSpan.Zero)) throw new ArgumentOutOfRangeException(nameof(retryDelays));
			var builder = new ResiliencePipelineBuilder();
			if (delays.Length != 0)
			{
				builder.AddRetry(new RetryStrategyOptions
				{
					MaxRetryAttempts = delays.Length,
					ShouldHandle = new PredicateBuilder().Handle<Exception>(isTransient),
					DelayGenerator = args => new ValueTask<TimeSpan?>(delays[args.AttemptNumber])
				});
			}
			return builder.Build();
		}

		/// <summary>Enterprise Library's FixedInterval: every retry waits the same interval.</summary>
		public static IEnumerable<TimeSpan> FixedInterval(int retryCount, TimeSpan retryInterval)
		{
			if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
			if (retryInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryInterval));
			return Enumerable.Repeat(retryInterval, retryCount);
		}

		public static IEnumerable<TimeSpan> Incremental(int retryCount, TimeSpan initialInterval, TimeSpan increment)
		{
			if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
			if (initialInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialInterval));
			if (increment < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(increment));
			// Preserve Enterprise Library's immediate first retry, followed by its configured delays:
			// the first retry is not delayed, and the nth delayed retry waits initialInterval plus one
			// increment for each delayed retry already taken.
			return Enumerable.Range(0, retryCount).Select(attempt => attempt == 0
				? TimeSpan.Zero : TimeSpan.FromTicks(checked(initialInterval.Ticks + increment.Ticks * (attempt - 1))));
		}
	}
}
