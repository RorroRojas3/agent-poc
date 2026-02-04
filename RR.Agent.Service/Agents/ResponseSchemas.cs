using System.Text.Json;

namespace RR.Agent.Service.Agents;

/// <summary>
/// Defines JSON schemas for structured output responses from agents.
/// </summary>
public static class ResponseSchemas
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// JSON schema for the Planner agent's response.
    /// </summary>
    public static BinaryData PlannerResponseSchema => BinaryData.FromObjectAsJson(new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "TaskPlanResponse",
            description = "A structured task plan with steps",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    taskAnalysis = new
                    {
                        type = "string",
                        description = "Brief analysis of what needs to be done and the approach"
                    },
                    steps = new
                    {
                        type = "array",
                        description = "List of executable steps",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                stepNumber = new
                                {
                                    type = "integer",
                                    description = "Step number (1-based)"
                                },
                                description = new
                                {
                                    type = "string",
                                    description = "Clear description of what this step accomplishes"
                                },
                                expectedOutput = new
                                {
                                    type = "string",
                                    description = "What success looks like for this step"
                                },
                                requiredPackages = new
                                {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "Python packages required for this step"
                                }
                            },
                            required = new[] { "stepNumber", "description", "expectedOutput", "requiredPackages" },
                            additionalProperties = false
                        }
                    },
                    requiredPackages = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "All unique packages needed for the entire plan"
                    }
                },
                required = new[] { "taskAnalysis", "steps", "requiredPackages" },
                additionalProperties = false
            }
        }
    }, JsonOptions);

    /// <summary>
    /// JSON schema for the Evaluator agent's response.
    /// </summary>
    public static BinaryData EvaluatorResponseSchema => BinaryData.FromObjectAsJson(new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "EvaluationResponse",
            description = "Evaluation of task execution results",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    isSuccessful = new
                    {
                        type = "boolean",
                        description = "Whether the step execution was successful"
                    },
                    isImpossible = new
                    {
                        type = "boolean",
                        description = "Whether the task is determined to be impossible"
                    },
                    reasoning = new
                    {
                        type = "string",
                        description = "Detailed explanation of the assessment"
                    },
                    issues = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "List of specific issues identified"
                    },
                    suggestions = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "List of suggestions for improvement"
                    },
                    shouldRetry = new
                    {
                        type = "boolean",
                        description = "Whether the step should be retried"
                    },
                    revisedApproach = new
                    {
                        type = new[] { "string", "null" },
                        description = "Alternative approach if retry is recommended"
                    },
                    confidenceScore = new
                    {
                        type = "number",
                        description = "Confidence score between 0.0 and 1.0"
                    }
                },
                required = new[] { "isSuccessful", "isImpossible", "reasoning", "issues", "suggestions", "shouldRetry", "revisedApproach", "confidenceScore" },
                additionalProperties = false
            }
        }
    }, JsonOptions);

    /// <summary>
    /// Gets the response format specification for structured JSON output.
    /// </summary>
    public static BinaryData GetJsonSchemaResponseFormat(string schemaName, object schema)
    {
        return BinaryData.FromObjectAsJson(new
        {
            type = "json_schema",
            json_schema = new
            {
                name = schemaName,
                strict = true,
                schema
            }
        }, JsonOptions);
    }
}
