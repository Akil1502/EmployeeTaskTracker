using System.Data;
using EmployeeTaskTracker.Shared;
using Microsoft.Data.SqlClient;

namespace EmployeeTaskTracker.Api.Data;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskDto>> SearchAsync(TaskFilter filter, CancellationToken cancellationToken = default);
    Task<TaskDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(TaskSaveRequest request, int createdBy, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(TaskSaveRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(int taskId, string status, int? restrictToAssignee, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default);
    Task<DashboardStatsDto> GetDashboardStatsAsync(int? assignedTo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskDto>> GetRecentTasksAsync(int? assignedTo, int topCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Task data access. Like <see cref="UserRepository"/>, every call is a stored
/// procedure invocation via ADO.NET - no inline SQL, no ORM.
/// </summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TaskRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<TaskDto>> SearchAsync(TaskFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_Search", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // A blank filter is sent as NULL, which the procedure reads as "no filter".
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value =
            string.IsNullOrWhiteSpace(filter.Search) ? DBNull.Value : filter.Search.Trim();
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value =
            string.IsNullOrWhiteSpace(filter.Status) ? DBNull.Value : filter.Status;
        command.Parameters.Add("@Priority", SqlDbType.NVarChar, 20).Value =
            string.IsNullOrWhiteSpace(filter.Priority) ? DBNull.Value : filter.Priority;
        command.Parameters.Add("@AssignedTo", SqlDbType.Int).Value =
            filter.AssignedTo.HasValue ? filter.AssignedTo.Value : DBNull.Value;

        return await ReadTasksAsync(command, cancellationToken);
    }

    public async Task<TaskDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapTask(reader) : null;
    }

    public async Task<int> CreateAsync(TaskSaveRequest request, int createdBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_Insert", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddTaskParameters(command, request);
        command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

        var newIdParameter = command.Parameters.Add("@NewTaskId", SqlDbType.Int);
        newIdParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return newIdParameter.Value is int newId ? newId : 0;
    }

    public async Task<bool> UpdateAsync(TaskSaveRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_Update", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = request.TaskId;
        AddTaskParameters(command, request);

        var rowsParameter = command.Parameters.Add("@RowsAffected", SqlDbType.Int);
        rowsParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return rowsParameter.Value is int rows && rows > 0;
    }

    public async Task<bool> UpdateStatusAsync(int taskId, string status, int? restrictToAssignee, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_UpdateStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status;

        // Employees are scoped to their own tasks; Admins pass NULL for no scope.
        command.Parameters.Add("@AssignedTo", SqlDbType.Int).Value =
            restrictToAssignee.HasValue ? restrictToAssignee.Value : DBNull.Value;

        var rowsParameter = command.Parameters.Add("@RowsAffected", SqlDbType.Int);
        rowsParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return rowsParameter.Value is int rows && rows > 0;
    }

    public async Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Task_Delete", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;

        var rowsParameter = command.Parameters.Add("@RowsAffected", SqlDbType.Int);
        rowsParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return rowsParameter.Value is int rows && rows > 0;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(int? assignedTo, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Dashboard_GetStats", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@AssignedTo", SqlDbType.Int).Value =
            assignedTo.HasValue ? assignedTo.Value : DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new DashboardStatsDto();

        // With no matching rows the SUM aggregates come back as NULL, so each
        // one is read defensively rather than with GetInt32.
        return new DashboardStatsDto
        {
            TotalTasks = reader.GetInt32("TotalTasks"),
            PendingTasks = reader.GetNullableInt32("PendingTasks") ?? 0,
            InProgressTasks = reader.GetNullableInt32("InProgressTasks") ?? 0,
            CompletedTasks = reader.GetNullableInt32("CompletedTasks") ?? 0,
            HighPriorityTasks = reader.GetNullableInt32("HighPriorityTasks") ?? 0,
            OverdueTasks = reader.GetNullableInt32("OverdueTasks") ?? 0
        };
    }

    public async Task<IReadOnlyList<TaskDto>> GetRecentTasksAsync(int? assignedTo, int topCount, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Dashboard_GetRecentTasks", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@AssignedTo", SqlDbType.Int).Value =
            assignedTo.HasValue ? assignedTo.Value : DBNull.Value;
        command.Parameters.Add("@TopCount", SqlDbType.Int).Value = topCount;

        return await ReadTasksAsync(command, cancellationToken);
    }

    private static void AddTaskParameters(SqlCommand command, TaskSaveRequest request)
    {
        command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = request.Title.Trim();
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value =
            string.IsNullOrWhiteSpace(request.Description) ? DBNull.Value : request.Description.Trim();
        command.Parameters.Add("@AssignedTo", SqlDbType.Int).Value =
            request.AssignedTo.HasValue ? request.AssignedTo.Value : DBNull.Value;
        command.Parameters.Add("@Priority", SqlDbType.NVarChar, 20).Value = request.Priority;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = request.Status;
        command.Parameters.Add("@DueDate", SqlDbType.DateTime).Value =
            request.DueDate.HasValue ? request.DueDate.Value : DBNull.Value;
    }

    private static async Task<IReadOnlyList<TaskDto>> ReadTasksAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var tasks = new List<TaskDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tasks.Add(MapTask(reader));

        return tasks;
    }

    private static TaskDto MapTask(SqlDataReader reader) => new()
    {
        TaskId = reader.GetInt32("TaskId"),
        Title = reader.GetString("Title"),
        Description = reader.GetNullableString("Description"),
        AssignedTo = reader.GetNullableInt32("AssignedTo"),
        AssignedToName = reader.GetNullableString("AssignedToName"),
        Priority = reader.GetString("Priority"),
        Status = reader.GetString("Status"),
        DueDate = reader.GetNullableDateTime("DueDate"),
        CreatedBy = reader.GetNullableInt32("CreatedBy"),
        CreatedByName = reader.GetNullableString("CreatedByName"),
        CreatedAt = reader.GetDateTime("CreatedAt"),
        UpdatedAt = reader.GetNullableDateTime("UpdatedAt")
    };
}
