namespace RR.Agent.Model.Dtos;

/// <summary>
/// Represents the evaluation of a task step execution by the Evaluator agent.
/// </summary>
public sealed class EvaluationResult
{
    /// <summary>
    /// Whether the step execution was successful and met the expected outcome.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Whether the task has been determined to be impossible to complete.
    /// </summary>
    public bool IsImpossible { get; set; }

    /// <summary>
    /// The evaluator's reasoning for the assessment.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// List of specific issues identified in the execution.
    /// </summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// List of suggestions for improving the execution.
    /// </summary>
    public List<string> Suggestions { get; set; } = [];

    /// <summary>
    /// Whether the step should be retried with modifications.
    /// </summary>
    public bool ShouldRetry { get; set; }

    /// <summary>
    /// Alternative approach suggested if retry is recommended.
    /// </summary>
    public string? RevisedApproach { get; set; }

    /// <summary>
    /// Confidence score for the evaluation (0.0 to 1.0).
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Creates a successful evaluation result.
    /// </summary>
    public static EvaluationResult Successful(string reasoning, double confidence = 1.0) => new()
    {
        IsSuccessful = true,
        Reasoning = reasoning,
        ConfidenceScore = confidence
    };

    /// <summary>
    /// Creates a failed evaluation result with retry recommendation.
    /// </summary>
    public static EvaluationResult FailedWithRetry(string reasoning, List<string> issues, List<string> suggestions, string? revisedApproach = null) => new()
    {
        IsSuccessful = false,
        Reasoning = reasoning,
        Issues = issues,
        Suggestions = suggestions,
        ShouldRetry = true,
        RevisedApproach = revisedApproach
    };

    /// <summary>
    /// Creates an impossible task evaluation result.
    /// </summary>
    public static EvaluationResult Impossible(string reasoning, List<string> issues) => new()
    {
        IsSuccessful = false,
        IsImpossible = true,
        Reasoning = reasoning,
        Issues = issues,
        ShouldRetry = false
    };
}
