namespace Adxstudio.Xrm.Threading
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Polly;
	using Polly.Retry;

	/// <summary>
	/// Builds retry pipelines that reproduce the retry strategies of the retired Enterprise Library
	/// Transient Fault Handling Application Block.
	/// </summary>
	/// <remarks>
	/// Enterprise Library retried the first failure immediately and only applied a strategy's delay
	/// from the second retry onwards, so each strategy here returns a zero delay for the first retry.
	/// </remarks>
	public static class RetryPolicies
	{
		/// <summary>
		/// Creates a pipeline that retries exceptions flagged as transient, waiting the given delay before each retry.
		/// </summary>
		/// <param name="isTransient">Flags the exceptions that should be retried.</param>
		/// <param name="retryDelays">The delay before each retry; the number of delays is the number of retries.</param>
		/// <returns>The pipeline.</returns>
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
					ShouldHandle = new PredicateBuilder().Handle(isTransient),
					DelayGenerator = args => new ValueTask<TimeSpan?>(delays[args.AttemptNumber]),
				});
			}

			return builder.Build();
		}

		/// <summary>
		/// The delays of Enterprise Library's <c>FixedInterval</c> strategy: every retry after the first waits the same interval.
		/// </summary>
		/// <param name="retryCount">The number of retries.</param>
		/// <param name="retryInterval">The interval between retries.</param>
		/// <returns>The delay before each retry.</returns>
		public static IEnumerable<TimeSpan> FixedInterval(int retryCount, TimeSpan retryInterval)
		{
			if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
			if (retryInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryInterval));

			return Enumerable.Range(0, retryCount).Select(retry => retry == 0 ? TimeSpan.Zero : retryInterval);
		}

		/// <summary>
		/// The delays of Enterprise Library's <c>Incremental</c> strategy: retry n after the first waits
		/// <paramref name="initialInterval"/> plus n times <paramref name="increment"/>.
		/// </summary>
		/// <param name="retryCount">The number of retries.</param>
		/// <param name="initialInterval">The base interval.</param>
		/// <param name="increment">The amount the interval grows by for each retry.</param>
		/// <returns>The delay before each retry.</returns>
		public static IEnumerable<TimeSpan> Incremental(int retryCount, TimeSpan initialInterval, TimeSpan increment)
		{
			if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
			if (initialInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialInterval));
			if (increment < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(increment));

			return Enumerable.Range(0, retryCount).Select(retry => retry == 0
				? TimeSpan.Zero
				: TimeSpan.FromTicks(checked(initialInterval.Ticks + (increment.Ticks * retry))));
		}
	}
}
