using System.Net;
using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Shared;

namespace EmployeeTaskTracker.Api.Notifications;

public interface ITaskNotifier
{
    Task NotifyAssignedAsync(TaskDto task, string assignedByName, CancellationToken cancellationToken = default);

    Task NotifyStatusChangedAsync(
        TaskDto task, string previousStatus, string changedByName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns a task event into the emails it should produce, then hands them to the
/// queue. Every method swallows its own failures: a notification is a side
/// effect of the request, and must never be the reason the request fails.
/// </summary>
public sealed class TaskNotifier : ITaskNotifier
{
    private readonly IEmailQueue _queue;
    private readonly IUserRepository _users;
    private readonly ILogger<TaskNotifier> _logger;

    public TaskNotifier(IEmailQueue queue, IUserRepository users, ILogger<TaskNotifier> logger)
    {
        _queue = queue;
        _users = users;
        _logger = logger;
    }

    /// <summary>Tells an employee that a task is now theirs.</summary>
    public async Task NotifyAssignedAsync(
        TaskDto task, string assignedByName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (task.AssignedTo is null)
                return;

            var assignee = await _users.GetByIdAsync(task.AssignedTo.Value, cancellationToken);
            if (assignee is null || string.IsNullOrWhiteSpace(assignee.Email))
                return;

            var due = task.DueDate?.ToString("dd MMM yyyy") ?? "No due date";

            var text =
                $"""
                 Hello {assignee.Name},

                 {assignedByName} has assigned a task to you.

                   Task:     {task.Title}
                   Priority: {task.Priority}
                   Status:   {TaskStatuses.ToDisplay(task.Status)}
                   Due:      {due}
                 {DescriptionBlock(task.Description)}
                 Sign in to the Employee Task Tracker to update its status.
                 """;

            var html = Layout(
                heading: "A task has been assigned to you",
                intro: $"{Escape(assignedByName)} has assigned the following task to you.",
                task: task,
                dueDisplay: due);

            _queue.Enqueue(new EmailMessage
            {
                ToAddress = assignee.Email,
                ToName = assignee.Name,
                Subject = $"New task assigned: {task.Title}",
                TextBody = text,
                HtmlBody = html,
                Kind = "task-assigned"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not queue the assignment notification for task {TaskId}.", task.TaskId);
        }
    }

    /// <summary>
    /// Tells every administrator that somebody moved a task along. All active
    /// admins are notified rather than one hardcoded address, so adding another
    /// administrator needs no code change.
    /// </summary>
    public async Task NotifyStatusChangedAsync(
        TaskDto task, string previousStatus, string changedByName, CancellationToken cancellationToken = default)
    {
        try
        {
            var admins = await _users.GetAdminsAsync(cancellationToken);
            if (admins.Count == 0)
            {
                _logger.LogWarning("No active administrator to notify about task {TaskId}.", task.TaskId);
                return;
            }

            var from = TaskStatuses.ToDisplay(previousStatus);
            var to = TaskStatuses.ToDisplay(task.Status);
            var owner = task.AssignedToName ?? "Unassigned";

            foreach (var admin in admins)
            {
                if (string.IsNullOrWhiteSpace(admin.Email))
                    continue;

                var text =
                    $"""
                     Hello {admin.Name},

                     {changedByName} changed the status of a task.

                       Task:        {task.Title}
                       Assigned to: {owner}
                       Status:      {from} -> {to}
                       Priority:    {task.Priority}
                     {DescriptionBlock(task.Description)}
                     Sign in to the Employee Task Tracker to review it.
                     """;

                var html = Layout(
                    heading: "A task status has changed",
                    intro: $"{Escape(changedByName)} moved this task from "
                           + $"<strong>{Escape(from)}</strong> to <strong>{Escape(to)}</strong>.",
                    task: task,
                    dueDisplay: task.DueDate?.ToString("dd MMM yyyy") ?? "No due date");

                _queue.Enqueue(new EmailMessage
                {
                    ToAddress = admin.Email,
                    ToName = admin.Name,
                    Subject = $"{task.Title} moved to {to}",
                    TextBody = text,
                    HtmlBody = html,
                    Kind = "status-changed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not queue the status notification for task {TaskId}.", task.TaskId);
        }
    }

    private static string DescriptionBlock(string? description)
        => string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"\n  Details:  {description.Trim()}\n";

    /// <summary>
    /// A small inline-styled HTML body. Styles are inline because email clients
    /// strip stylesheets, and the palette matches the application so the message
    /// looks like it came from the same product.
    /// </summary>
    private static string Layout(string heading, string intro, TaskDto task, string dueDisplay)
    {
        var description = string.IsNullOrWhiteSpace(task.Description)
            ? string.Empty
            : $"""
               <tr>
                 <td style="padding:6px 0;color:#5c6675;width:9rem;">Details</td>
                 <td style="padding:6px 0;color:#12151a;">{Escape(task.Description)}</td>
               </tr>
               """;

        return $"""
                <div style="font-family:Segoe UI,system-ui,-apple-system,Helvetica,Arial,sans-serif;
                            background:#fbfaf8;padding:24px;color:#12151a;">
                  <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e1da;
                              border-radius:14px;overflow:hidden;">
                    <div style="background:#12151a;padding:18px 24px;">
                      <span style="color:#ffffff;font-size:13px;font-weight:600;letter-spacing:.14em;
                                   text-transform:uppercase;">Task Tracker</span>
                    </div>
                    <div style="padding:24px;">
                      <h1 style="margin:0 0 8px;font-size:19px;letter-spacing:-.02em;">{Escape(heading)}</h1>
                      <p style="margin:0 0 20px;color:#5c6675;font-size:14px;line-height:1.6;">{intro}</p>

                      <div style="border:1px solid #e5e1da;border-radius:10px;padding:16px;">
                        <div style="font-weight:600;font-size:15px;margin-bottom:12px;">{Escape(task.Title)}</div>
                        <table style="width:100%;border-collapse:collapse;font-size:13px;">
                          <tr>
                            <td style="padding:6px 0;color:#5c6675;width:9rem;">Assigned to</td>
                            <td style="padding:6px 0;color:#12151a;">{Escape(task.AssignedToName ?? "Unassigned")}</td>
                          </tr>
                          <tr>
                            <td style="padding:6px 0;color:#5c6675;">Priority</td>
                            <td style="padding:6px 0;color:#12151a;">{Escape(task.Priority)}</td>
                          </tr>
                          <tr>
                            <td style="padding:6px 0;color:#5c6675;">Status</td>
                            <td style="padding:6px 0;color:#12151a;">{Escape(TaskStatuses.ToDisplay(task.Status))}</td>
                          </tr>
                          <tr>
                            <td style="padding:6px 0;color:#5c6675;">Due date</td>
                            <td style="padding:6px 0;color:#12151a;">{Escape(dueDisplay)}</td>
                          </tr>
                          {description}
                        </table>
                      </div>

                      <p style="margin:20px 0 0;color:#8b94a1;font-size:12px;">
                        Sent automatically by the Employee Task Tracker System.
                      </p>
                    </div>
                  </div>
                </div>
                """;
    }

    /// <summary>
    /// Task titles and descriptions are user input, so they are HTML-encoded
    /// before going into the message body.
    /// </summary>
    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
