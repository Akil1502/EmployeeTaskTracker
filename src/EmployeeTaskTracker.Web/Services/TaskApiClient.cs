using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EmployeeTaskTracker.Shared;

namespace EmployeeTaskTracker.Web.Services;

/// <summary>Raised when the API rejects a call, carrying its message.</summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(HttpStatusCode statusCode, string message) : base(message)
        => StatusCode = statusCode;
}

/// <summary>
/// The single point through which the Blazor frontend talks to the Web API. It
/// attaches the bearer token to every request and turns an error response into
/// an <see cref="ApiException"/> carrying the API's own message, so pages can
/// show something meaningful instead of a raw status code.
/// </summary>
public sealed class TaskApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TokenStore _tokenStore;
    private readonly ILogger<TaskApiClient> _logger;

    public TaskApiClient(HttpClient http, TokenStore tokenStore, ILogger<TaskApiClient> logger)
    {
        _http = http;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // -- Authentication ------------------------------------------------------

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", request, JsonOptions, ct);
        return await ReadAsync<LoginResponse>(response, ct);
    }

    // -- Dashboard -----------------------------------------------------------

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
        => SendAsync<DashboardDto>(HttpMethod.Get, "api/dashboard", null, ct);

    // -- Tasks ---------------------------------------------------------------

    public Task<List<TaskDto>> GetTasksAsync(TaskFilter filter, CancellationToken ct = default)
        => SendAsync<List<TaskDto>>(HttpMethod.Get, $"api/tasks{filter.ToQueryString()}", null, ct);

    public Task<TaskDto> CreateTaskAsync(TaskSaveRequest request, CancellationToken ct = default)
        => SendAsync<TaskDto>(HttpMethod.Post, "api/tasks", request, ct);

    public Task<TaskDto> UpdateTaskAsync(TaskSaveRequest request, CancellationToken ct = default)
        => SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{request.TaskId}", request, ct);

    public Task UpdateTaskStatusAsync(int taskId, string status, CancellationToken ct = default)
        => SendAsync(HttpMethod.Patch, $"api/tasks/{taskId}/status",
            new TaskStatusUpdateRequest { Status = status }, ct);

    public Task DeleteTaskAsync(int taskId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/tasks/{taskId}", null, ct);

    // -- Users ---------------------------------------------------------------

    public Task<List<UserDto>> GetEmployeesAsync(CancellationToken ct = default)
        => SendAsync<List<UserDto>>(HttpMethod.Get, "api/users/employees", null, ct);

    // -- Plumbing ------------------------------------------------------------

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, ct);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        var token = await _tokenStore.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the API at {BaseAddress}{Url}.", _http.BaseAddress, url);
            throw new ApiException(HttpStatusCode.ServiceUnavailable,
                "Could not reach the Employee Task Tracker API. Confirm the API project is running and that "
                + "ApiBaseUrl in appsettings.json points at it.");
        }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, ct);

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);

        return value ?? throw new ApiException(response.StatusCode,
            "The API returned an empty response where data was expected.");
    }

    private async Task<ApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // The API reports failures as an ApiError body; fall back to the status
        // code if the body is missing or is not JSON.
        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, ct);
            message = error?.Message;
        }
        catch (Exception)
        {
            // Ignored - handled by the fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
                HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
                HttpStatusCode.NotFound => "The requested item no longer exists.",
                _ => $"The request failed with status {(int)response.StatusCode} ({response.ReasonPhrase})."
            };
        }

        return new ApiException(response.StatusCode, message);
    }
}
