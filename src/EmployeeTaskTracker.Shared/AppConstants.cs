namespace EmployeeTaskTracker.Shared;

/// <summary>
/// The role names stored in the Users.Role column. A single Users table holds
/// both kinds of account and Role is what separates them.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";

    public static readonly IReadOnlyList<string> All = [Admin, Employee];
}

/// <summary>
/// Values allowed in the Tasks.Status column. These are mirrored by the
/// CK_Tasks_Status check constraint in database/setup.sql.
/// </summary>
public static class TaskStatuses
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static readonly IReadOnlyList<string> All = [Pending, InProgress, Completed];

    /// <summary>Turns "InProgress" into "In Progress" for display.</summary>
    public static string ToDisplay(string? status) => status switch
    {
        InProgress => "In Progress",
        null => string.Empty,
        _ => status
    };
}

/// <summary>
/// Values allowed in the Tasks.Priority column, mirrored by CK_Tasks_Priority.
/// </summary>
public static class TaskPriorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High];
}
