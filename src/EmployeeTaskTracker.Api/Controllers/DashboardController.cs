using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTaskTracker.Api.Controllers;

/// <summary>
/// Dashboard reporting: the summary cards and the recent tasks widget.
/// Figures are organisation-wide for an Admin and scoped to their own tasks for
/// an Employee.
/// </summary>
[Authorize]
public sealed class DashboardController : ApiControllerBase
{
    private const int RecentTaskCount = 5;

    private readonly ITaskRepository _tasks;

    public DashboardController(ITaskRepository tasks) => _tasks = tasks;

    /// <summary>Returns the stats and the recent tasks in a single round trip.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var scope = AssigneeScope;

        // Two independent reads, so run them together rather than in sequence.
        var statsTask = _tasks.GetDashboardStatsAsync(scope, cancellationToken);
        var recentTask = _tasks.GetRecentTasksAsync(scope, RecentTaskCount, cancellationToken);

        await Task.WhenAll(statsTask, recentTask);

        return Ok(new DashboardDto
        {
            Stats = await statsTask,
            RecentTasks = [.. await recentTask]
        });
    }
}
