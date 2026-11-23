namespace Adxstudio.Xrm.Threading
{
    using System;
    using System.Threading.Tasks;
    using Polly;
    using Polly.Retry;

    /// <summary>Creates retry pipelines with the previous fast-first-retry behavior.</summary>
    public static class RetryPolicies
    {
        public static ResiliencePipeline Create(int retryCount, TimeSpan initialInterval, TimeSpan increment, Func<Exception, bool> isTransient)
        {
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
            if (initialInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialInterval));
            if (increment < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(increment));
            if (isTransient == null) throw new ArgumentNullException(nameof(isTransient));
            if (retryCount == 0) return ResiliencePipeline.Empty;

            return new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryCount,
                ShouldHandle = args => new ValueTask<bool>(args.Outcome.Exception != null && isTransient(args.Outcome.Exception)),
                DelayGenerator = args => new ValueTask<TimeSpan?>(args.AttemptNumber == 0
                    ? TimeSpan.Zero
                    : initialInterval + TimeSpan.FromTicks(increment.Ticks * args.AttemptNumber))
            }).Build();
        }
    }
}
