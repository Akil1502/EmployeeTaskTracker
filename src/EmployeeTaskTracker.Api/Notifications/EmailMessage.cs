namespace EmployeeTaskTracker.Api.Notifications;

/// <summary>One notification waiting to be delivered.</summary>
public sealed class EmailMessage
{
    public required string ToAddress { get; init; }
    public required string ToName { get; init; }
    public required string Subject { get; init; }

    /// <summary>Plain-text alternative, for clients that do not render HTML.</summary>
    public required string TextBody { get; init; }

    public required string HtmlBody { get; init; }

    /// <summary>Short label used in log messages, e.g. "task-assigned".</summary>
    public required string Kind { get; init; }
}
