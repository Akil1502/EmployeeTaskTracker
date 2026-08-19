using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeTaskTracker.Api.Controllers;

/// <summary>
/// Task management. Creating, editing and deleting are Admin-only; an Employee
/// may read the tasks assigned to them and change their status.
/// </summary>
[Authorize]
public sealed class TasksController : ApiControllerBase
{
    private readonly ITaskRepository _tasks;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskRepository tasks, ILogger<TasksController> logger)
    {
        _tasks = tasks;
        _logger = logger;
    }

    /// <summary>
    /// Lists tasks with optional search and filters. An Admin sees every task;
    /// an Employee only ever sees their own, regardless of what they pass.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? assignedTo,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatusFilter(status))
            return BadRequest(new ApiError { Message = $"Unknown status filter '{status}'." });

        if (!IsValidPriorityFilter(priority))
            return BadRequest(new ApiError { Message = $"Unknown priority filter '{priority}'." });

        var filter = new TaskFilter
        {
            Search = search,
            Status = status,
            Priority = priority,
            // An Employee's scope is forced to their own id; only an Admin may
            // filter by an arbitrary assignee.
            AssignedTo = IsAdmin ? assignedTo : CurrentUserId
        };

        var results = await _tasks.SearchAsync(filter, cancellationToken);
        return Ok(results);
    }

    /// <summary>Fetches a single task.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(id, cancellationToken);
        if (task is null)
            return NotFound(new ApiError { Message = $"Task {id} was not found." });

        // Hide other people's tasks from an Employee rather than confirming the
        // id exists.
        if (!IsAdmin && task.AssignedTo != CurrentUserId)
            return NotFound(new ApiError { Message = $"Task {id} was not found." });

        return Ok(task);
    }

    /// <summary>Creates a task. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskDto>> Create(
        [FromBody] TaskSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatus(request.Status) || !IsValidPriority(request.Priority))
            return BadRequest(new ApiError { Message = "Status or priority is not a recognised value." });

        var newId = await _tasks.CreateAsync(request, CurrentUserId, cancellationToken);
        _logger.LogInformation("Task {TaskId} created by user {UserId}.", newId, CurrentUserId);

        var created = await _tasks.GetByIdAsync(newId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = newId }, created);
    }

    /// <summary>Edits a task in full. Admin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Update(
        int id,
        [FromBody] TaskSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatus(request.Status) || !IsValidPriority(request.Priority))
            return BadRequest(new ApiError { Message = "Status or priority is not a recognised value." });

        request.TaskId = id;

        var updated = await _tasks.UpdateAsync(request, cancellationToken);
        if (!updated)
            return NotFound(new ApiError { Message = $"Task {id} was not found." });

        _logger.LogInformation("Task {TaskId} updated by user {UserId}.", id, CurrentUserId);

        return Ok(await _tasks.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Updates only the status. Available to both roles - an Employee is scoped
    /// by the repository to tasks assigned to them.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] TaskStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatus(request.Status))
            return BadRequest(new ApiError { Message = $"Unknown status '{request.Status}'." });

        var updated = await _tasks.UpdateStatusAsync(id, request.Status, AssigneeScope, cancellationToken);
        if (!updated)
        {
            return NotFound(new ApiError
            {
                Message = $"Task {id} was not found, or it is not assigned to you."
            });
        }

        _logger.LogInformation("Task {TaskId} moved to {Status} by user {UserId}.", id, request.Status, CurrentUserId);
        return NoContent();
    }

    /// <summary>Deletes a task. Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _tasks.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound(new ApiError { Message = $"Task {id} was not found." });

        _logger.LogInformation("Task {TaskId} deleted by user {UserId}.", id, CurrentUserId);
        return NoContent();
    }

    private static bool IsValidStatus(string value) =>
        TaskStatuses.All.Contains(value, StringComparer.Ordinal);

    private static bool IsValidPriority(string value) =>
        TaskPriorities.All.Contains(value, StringComparer.Ordinal);

    private static bool IsValidStatusFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) || IsValidStatus(value);

    private static bool IsValidPriorityFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) || IsValidPriority(value);
}
