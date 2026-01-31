namespace RR.Agent.Evaluation.Models;

/// <summary>
/// Context for retry attempts.
/// </summary>
/// <param name="AttemptNumber">Current attempt number (1-based).</param>
/// <param name="MaxAttempts">Maximum allowed attempts.</param>
/// <param name="PreviousErrors">List of previous error messages.</param>
/// <param name="SuggestedAdjustment">Suggested adjustment for next attempt.</param>
public sealed record RetryContext(
    int AttemptNumber,
    int MaxAttempts,
    IReadOnlyList<string> PreviousErrors,
    string? SuggestedAdjustment)
{
    /// <summary>
    /// Returns true if more retry attempts are available.
    /// </summary>
    public bool CanRetry => AttemptNumber < MaxAttempts;

    /// <summary>
    /// Creates initial retry context.
    /// </summary>
    public static RetryContext Initial(int maxAttempts) => new(
        AttemptNumber: 1,
        MaxAttempts: maxAttempts,
        PreviousErrors: [],
        SuggestedAdjustment: null);

    /// <summary>
    /// Creates next retry context after a failure.
    /// </summary>
    public RetryContext Next(string errorMessage, string? suggestedAdjustment = null) => new(
        AttemptNumber: AttemptNumber + 1,
        MaxAttempts: MaxAttempts,
        PreviousErrors: [..PreviousErrors, errorMessage],
        SuggestedAdjustment: suggestedAdjustment);
}
