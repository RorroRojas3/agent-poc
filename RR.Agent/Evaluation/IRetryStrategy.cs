namespace RR.Agent.Evaluation;

using RR.Agent.Evaluation.Models;

/// <summary>
/// Defines retry behavior for failed operations.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Determines if another retry should be attempted.
    /// </summary>
    /// <param name="context">Current retry context.</param>
    /// <returns>True if should retry.</returns>
    bool ShouldRetry(RetryContext context);

    /// <summary>
    /// Gets the delay before the next retry attempt.
    /// </summary>
    /// <param name="attemptNumber">Current attempt number.</param>
    /// <returns>Delay timespan.</returns>
    TimeSpan GetDelay(int attemptNumber);

    /// <summary>
    /// Creates initial retry context for a new step.
    /// </summary>
    /// <param name="maxAttempts">Maximum retry attempts.</param>
    /// <returns>Initial retry context.</returns>
    RetryContext CreateInitialContext(int maxAttempts);
}
