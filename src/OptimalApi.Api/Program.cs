using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OptimalApi.Api.Filters;
using OptimalApi.Api.Http;
using OptimalApi.Api.Infrastructure;
using OptimalApi.Api.Middleware;
using OptimalApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Fail fast at startup rather than on the first request.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is missing. Set it with dotnet user-secrets (see README.md) " +
        "or the ConnectionStrings__Default environment variable.");

builder.Services.AddDbContext<AppDbContext>(options => AppDbContext.Configure(options, connectionString));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());

// Every error uses the ApiResponse envelope, including the ones ASP.NET produces itself.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    // Malformed JSON, wrong types, missing required fields: same 400 shape as FluentValidation errors.
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(ApiErrors.Validation(context.HttpContext, context.ModelState));

    // Stop MVC turning empty 404/415 results into ProblemDetails; UseStatusCodePages below fills them in instead.
    options.SuppressMapClientErrors = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Required by UseExceptionHandler() even though GlobalExceptionHandler writes the actual response;
// the app refuses to start without it.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// The retry policy above would keep a readiness probe hanging for ~30s when Postgres is down.
// Probes must answer fast, so cap every health check.
builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
    {
        registration.Timeout = TimeSpan.FromSeconds(5);
    }
});

var app = builder.Build();

app.UseExceptionHandler();

// Errors that would otherwise have an empty body (unknown URL, wrong HTTP method, wrong content type)
// get the same ApiResponse envelope as everything else.
app.UseStatusCodePages(context => ApiErrors.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up. Readiness: dependencies (Postgres) are reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();

await app.RunAsync();

/// <summary>Exposed so integration tests can bootstrap the app with WebApplicationFactory.</summary>
public partial class Program;
