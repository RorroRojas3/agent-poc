namespace RR.Agent.Exceptions;

/// <summary>
/// Base exception for agent-related errors.
/// </summary>
public class AgentException : Exception
{
    public AgentException(string message) : base(message) { }

    public AgentException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when planning fails.
/// </summary>
public class PlanningException : AgentException
{
    /// <summary>
    /// The original user request that failed to plan.
    /// </summary>
    public string UserRequest { get; }

    public PlanningException(string userRequest, string message)
        : base(message)
    {
        UserRequest = userRequest;
    }

    public PlanningException(string userRequest, string message, Exception innerException)
        : base(message, innerException)
    {
        UserRequest = userRequest;
    }
}

/// <summary>
/// Thrown when execution fails after all retries.
/// </summary>
public class ExecutionException : AgentException
{
    /// <summary>
    /// The step order that failed.
    /// </summary>
    public int StepOrder { get; }

    /// <summary>
    /// Number of attempts made before failing.
    /// </summary>
    public int AttemptsMade { get; }

    public ExecutionException(int stepOrder, int attemptsMade, string message)
        : base(message)
    {
        StepOrder = stepOrder;
        AttemptsMade = attemptsMade;
    }

    public ExecutionException(int stepOrder, int attemptsMade, string message, Exception innerException)
        : base(message, innerException)
    {
        StepOrder = stepOrder;
        AttemptsMade = attemptsMade;
    }
}

/// <summary>
/// Thrown when a task is determined to be impossible.
/// </summary>
public class ImpossibleTaskException : AgentException
{
    /// <summary>
    /// List of reasons why the task is impossible.
    /// </summary>
    public IReadOnlyList<string> FailureReasons { get; }

    public ImpossibleTaskException(IReadOnlyList<string> reasons, string explanation)
        : base(explanation)
    {
        FailureReasons = reasons;
    }
}
