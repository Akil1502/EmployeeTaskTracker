using System.Text;
using System.Text.Json;
using EmployeeTaskTracker.Api.Data;
using EmployeeTaskTracker.Api.Middleware;
using EmployeeTaskTracker.Api.Security;
using EmployeeTaskTracker.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing from appsettings.json.");

// ---------------------------------------------------------------------------
// Data access - stored procedures over ADO.NET
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

// ---------------------------------------------------------------------------
// Security
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            // Without this a token is accepted for up to five minutes past its
            // stated expiry, which is not what "ExpiryMinutes" should mean.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// MVC / API
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Return model-validation failures in the same shape as every other API error.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join(" ", context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors)
            .Select(error => error.ErrorMessage));

        return new BadRequestObjectResult(new ApiError
        {
            Message = string.IsNullOrWhiteSpace(message) ? "The request was not valid." : message,
            TraceId = context.HttpContext.TraceIdentifier
        });
    };
});

// The Blazor Server frontend calls this API from the server side, so CORS is
// not strictly required. It is configured anyway so the API can be exercised
// from Swagger or a browser-based client during a demo.
const string CorsPolicy = "EmployeeTaskTrackerCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// Swagger, with a button to paste a JWT so the secured endpoints can be tried
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employee Task Tracker API",
        Version = "v1",
        Description = "ASP.NET Core Web API backing the Employee Task Tracker System. "
                    + "All data access is through SQL Server stored procedures."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login."
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = []
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------

// Registered first so it wraps everything that follows.
app.UseApiExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Task Tracker API v1");
    options.DocumentTitle = "Employee Task Tracker API";
});

// Only redirect in a deployed environment. Locally the Blazor Server frontend
// calls the API over plain HTTP, and redirecting would force it to negotiate
// the self-signed development certificate for no benefit.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// A dependency-free readiness probe, handy when verifying a deployment.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
   .AllowAnonymous()
   .ExcludeFromDescription();

app.Run();
