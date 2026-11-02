using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>One question asked to the AI assistant; counts against the company's monthly AI allowance.</summary>
public class AiMessageLog : TenantEntity
{
    public Guid UserId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    /// <summary>How many times the assistant looked something up while answering.</summary>
    public int ToolCalls { get; set; }
    /// <summary>The question was spoken rather than typed.</summary>
    public bool Voice { get; set; }
    /// <summary>Set when the call failed, so failures are visible without the question text.</summary>
    public string? Error { get; set; }
}
