using System.ComponentModel.DataAnnotations;

namespace EmployeeTaskTracker.Shared;

/// <summary>A task row as returned by the API, flattened with assignee names.</summary>
public sealed class TaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string Priority { get; set; } = TaskPriorities.Medium;
    public string Status { get; set; } = TaskStatuses.Pending;
    public DateTime? DueDate { get; set; }
    public int? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string StatusDisplay => TaskStatuses.ToDisplay(Status);

    /// <summary>Past its due date and not yet finished.</summary>
    public bool IsOverdue =>
        DueDate.HasValue
        && DueDate.Value.Date < DateTime.Now.Date
        && Status != TaskStatuses.Completed;
}

/// <summary>Payload for creating or editing a task (Admin only).</summary>
public sealed class TaskSaveRequest
{
    public int TaskId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    public int? AssignedTo { get; set; }

    [Required(ErrorMessage = "Priority is required.")]
    public string Priority { get; set; } = TaskPriorities.Medium;

    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = TaskStatuses.Pending;

    public DateTime? DueDate { get; set; }
}

/// <summary>Payload for the status-only update an Employee may perform.</summary>
public sealed class TaskStatusUpdateRequest
{
    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = TaskStatuses.Pending;
}

/// <summary>
/// Search and filter arguments for the task list. Any null or blank member
/// means "do not filter on this column".
/// </summary>
public sealed class TaskFilter
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? AssignedTo { get; set; }

    /// <summary>Renders the filter as a query string for the API call.</summary>
    public string ToQueryString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Search))
            parts.Add($"search={Uri.EscapeDataString(Search.Trim())}");
        if (!string.IsNullOrWhiteSpace(Status))
            parts.Add($"status={Uri.EscapeDataString(Status)}");
        if (!string.IsNullOrWhiteSpace(Priority))
            parts.Add($"priority={Uri.EscapeDataString(Priority)}");
        if (AssignedTo.HasValue)
            parts.Add($"assignedTo={AssignedTo.Value}");

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(Search)
        || !string.IsNullOrWhiteSpace(Status)
        || !string.IsNullOrWhiteSpace(Priority)
        || AssignedTo.HasValue;
}

/// <summary>The dashboard summary cards.</summary>
public sealed class DashboardStatsDto
{
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int HighPriorityTasks { get; set; }
    public int OverdueTasks { get; set; }

    /// <summary>Completion rate as a whole percentage, 0 when there are no tasks.</summary>
    public int CompletionPercent =>
        TotalTasks == 0 ? 0 : (int)Math.Round(CompletedTasks * 100.0 / TotalTasks);
}

/// <summary>Everything the dashboard page needs, fetched in one round trip.</summary>
public sealed class DashboardDto
{
    public DashboardStatsDto Stats { get; set; } = new();
    public List<TaskDto> RecentTasks { get; set; } = [];
}

/// <summary>Uniform error body returned by the API's exception handler.</summary>
public sealed class ApiError
{
    public string Message { get; set; } = string.Empty;
    public string? TraceId { get; set; }
}
