using Microsoft.EntityFrameworkCore;
using Tenant.Api.Data;
using Tenant.Api.Hubs;
using Tenant.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Serilog;
using Tenant.Api.Common;

var builder = WebApplication.CreateBuilder(args);

// Serilog — structured logging with console sink; additional sinks
// (Seq, Application Insights, file) can be added via appsettings.json.
builder.Host.UseSerilog((ctx, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
});

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JWT configuration is missing. Set Jwt:Key, Jwt:Issuer and Jwt:Audience " +
        "via appsettings.Development.json (dev only), user-secrets, or environment " +
        "variables (e.g. Jwt__Key, Jwt__Issuer, Jwt__Audience).");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 bytes (256 bits) for HMAC-SHA256. " +
        "Generate one with e.g. `openssl rand -base64 48`.");
}

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Receipt caching + background warming
builder.Services.Configure<ReceiptCacheOptions>(builder.Configuration.GetSection("Receipts:Cache"));
builder.Services.AddSingleton<IReceiptCache, LocalFileReceiptCache>();
builder.Services.AddSingleton<IReceiptJobQueue, ReceiptJobQueue>();
builder.Services.AddHostedService<ReceiptWarmingWorker>();

// In-memory cache — used by ShareService for share-token lookups.
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

// ---- SignalR with optional Redis backplane --------------------------------
// Enables horizontal scaling of the API: in single-node dev the backplane
// is skipped; in multi-node prod set Redis:ConnectionString in config and
// SignalR automatically fans out messages via Redis pub/sub.
var signalR = builder.Services.AddSignalR();
var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalR.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("TenantApi");
    });
}

// FluentValidation — auto-validates request DTOs on model binding.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

// JWT Authentication Setup
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/shared-dashboard"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Correlation-ID")
              .AllowCredentials(); // Required for SignalR WebSocket + refresh-cookie
    });
});

// ---- Rate limiting ---------------------------------------------------------
// Two named policies, partitioned by client IP. Tuned for a single-tenant
// deployment: 5 login attempts / minute stops credential stuffing cold, and
// 60 share requests / minute is generous enough for the 5-second poll
// fallback × multiple tabs while still blocking scrapers.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too many requests.",
            status = 429,
            detail = "You have exceeded the allowed request rate for this endpoint. Please try again shortly."
        };
        await ctx.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), token);
    };

    options.AddPolicy(RateLimitPolicies.Login, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(RateLimitPolicies.Share, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ---- API versioning --------------------------------------------------------
// AssumeDefaultVersionWhenUnspecified keeps existing URLs (/api/login etc.)
// working without a version segment. Clients can opt into future versions
// via either the URL segment or an X-Api-Version header.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ---- Health checks ---------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        tags: new[] { "ready" });

// ---- OpenTelemetry tracing -------------------------------------------------
// Exports to console by default; set OpenTelemetry:OtlpEndpoint to forward
// spans to Jaeger / Tempo / any OTLP-compatible collector.
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "Tenant.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(o =>
            {
                // Strip health-check spans so they don't drown out real traffic.
                o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/healthz");
            })
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation(o =>
            {
                o.SetDbStatementForText = true;
                o.RecordException = true;
            });

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
        }
        else if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    });

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        if (feature?.Error is not null)
        {
            logger.LogError(feature.Error, "Unhandled exception on {Path}", context.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseStatusCodePages();

// Correlation ID + Serilog request logging must come after the exception
// handler so every log line inside a request carries the correlation id,
// including the handler itself.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// CORS must be before UseAuthentication and UseAuthorization
app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

// Rate limiter must come after auth so policies can see the user if needed.
app.UseRateLimiter();

// Health endpoints — liveness answers as long as the process is up,
// readiness only passes once the DB is reachable. Load balancers should
// probe /healthz/ready; container orchestrators should probe /healthz.
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false, // no checks — pure liveness
    ResponseWriter = HealthResponseWriter.Write
});
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.Write
});

app.MapControllers();
app.MapHub<SharedDashboardHub>("/hubs/shared-dashboard");

app.Run();

// Centralised JSON response writer so health-check payloads are consistent
// with the rest of the API's error shape.
internal static class HealthResponseWriter
{
    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(kvp => new
            {
                name = kvp.Key,
                status = kvp.Value.Status.ToString(),
                durationMs = kvp.Value.Duration.TotalMilliseconds,
                error = kvp.Value.Exception?.Message
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
