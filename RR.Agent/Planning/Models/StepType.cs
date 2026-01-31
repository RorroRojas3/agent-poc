namespace RR.Agent.Planning.Models;

/// <summary>
/// Defines the type of action a plan step performs.
/// </summary>
public enum StepType
{
    /// <summary>Script execution via code interpreter.</summary>
    CodeExecution,

    /// <summary>File read operation.</summary>
    FileRead,

    /// <summary>File write operation.</summary>
    FileWrite,

    /// <summary>Analysis or reasoning step.</summary>
    Analysis,

    /// <summary>User interaction required.</summary>
    UserInput
}
