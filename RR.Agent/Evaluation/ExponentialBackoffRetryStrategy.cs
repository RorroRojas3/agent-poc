namespace RR.Agent.Evaluation;

using RR.Agent.Evaluation.Models;

/// <summary>
/// Retry strategy using exponential backoff.
/// </summary>
public sealed class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
    private readonly double _multiplier = 2.0;
    private readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(30);

    public bool ShouldRetry(RetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.CanRetry;
    }

    public TimeSpan GetDelay(int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return _baseDelay;
        }

        var delay = TimeSpan.FromTicks(
            (long)(_baseDelay.Ticks * Math.Pow(_multiplier, attemptNumber - 1)));

        return delay > _maxDelay ? _maxDelay : delay;
    }

    public RetryContext CreateInitialContext(int maxAttempts)
    {
        return RetryContext.Initial(maxAttempts);
    }
}
