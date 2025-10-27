using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Net.Http;

using FluentValidation;
using FluentValidation.AspNetCore;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;

using Pipelane.Api.Middleware;
using Pipelane.Api.MultiTenancy;
using Pipelane.Application.Abstractions;
using Pipelane.Application.Ai;
using Pipelane.Application.Hunter;
using Pipelane.Application.Prospecting;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Entities;
using Pipelane.Infrastructure.Automations;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Channels;
using Pipelane.Infrastructure.Demo;
using Pipelane.Infrastructure.Followups;
using Pipelane.Infrastructure.Hunter;
using Pipelane.Infrastructure.Persistence;
using Pipelane.Infrastructure.Reports;
using Pipelane.Infrastructure.Security;
using Pipelane.Infrastructure.Services;
using Pipelane.Infrastructure.Webhooks;

using Quartz;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// DbContext
var conn = builder.Configuration.GetConnectionString("SqlServer")
           ?? Environment.GetEnvironmentVariable("DB_CONNECTION")
           ?? "Server=localhost\\SQLEXPRESS;Database=Pipelane;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(conn));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IDatabaseDiagnostics>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddMemoryCache();

// OpenTelemetry
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
builder.Services.AddOpenTelemetry()
    .WithTracing(b =>
    {
        b.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Pipelane.Api"))
            .AddSource("Pipelane.Messaging", "Pipelane.Webhooks", "Pipelane.Followups")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        b.AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpointUri))
        {
            b.AddOtlpExporter(options => options.Endpoint = endpointUri);
        }
    });

// Auth (JWT optional dev)
var jwtKey = builder.Configuration["JWT_KEY"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev-secret-key-please-change";
var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
if (jwtKeyBytes.Length < 32) jwtKeyBytes = SHA256.HashData(jwtKeyBytes);
var signingKey = new SymmetricSecurityKey(jwtKeyBytes);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:4200", "http://localhost:8080")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var httpContext = context.HttpContext;
        var status = context.ProblemDetails.Status ?? httpContext.Response.StatusCode;
        context.ProblemDetails.Status = status;
        context.ProblemDetails.Title ??= ProblemDetailsFrenchLocalizer.ResolveTitle(status);

        if (string.IsNullOrWhiteSpace(context.ProblemDetails.Detail))
        {
            context.ProblemDetails.Detail = ProblemDetailsFrenchLocalizer.ResolveDetail(status);
        }

        context.ProblemDetails.Instance ??= httpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue("CorrelationId", out var rawCorrelation) &&
            rawCorrelation is string correlation &&
            !string.IsNullOrWhiteSpace(correlation))
        {
            context.ProblemDetails.Extensions["correlationId"] = correlation;
        }
    };
});
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Pipelane.Application.Validators.SendMessageRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db");

// Http clients + Polly
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
})
    .AddPolicyHandler(CreateRetryPolicy())
    .AddPolicyHandler(CreateCircuitBreakerPolicy());
builder.Services.AddHttpClient("OpenAI", client =>
{
    var baseUrl = builder.Configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/";
    client.BaseAddress = new Uri(baseUrl);
})
    .AddPolicyHandler(CreateRetryPolicy())
    .AddPolicyHandler(CreateCircuitBreakerPolicy());
builder.Services.AddHttpClient("Automations")
    .AddPolicyHandler(CreateRetryPolicy())
    .AddPolicyHandler(CreateCircuitBreakerPolicy());
var demoMode = builder.Configuration.GetValue<bool?>("DEMO_MODE") ?? false;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var automationLimit = builder.Configuration.GetValue<int?>("Automations:RateLimitPerMinute") ?? 300;

    options.AddPolicy("tenant-ai", httpContext =>
    {
        var tenantProvider = httpContext.RequestServices.GetRequiredService<ITenantProvider>();
        var tenantId = tenantProvider.TenantId.ToString();
        return RateLimitPartition.GetTokenBucketLimiter(
            tenantId,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 120,
                TokensPerPeriod = 120,
                ReplenishmentPeriod = TimeSpan.FromHours(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("automations", httpContext =>
    {
        var key = httpContext.Request.Headers.TryGetValue("X-Automations-Token", out var tokenHeader)
            ? tokenHeader.ToString()
            : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous");

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = automationLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("webhooks", httpContext =>
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<MessagingLimitsOptions>>().Value;
        var perTenant = Math.Max(1, limits.WebhookPerMinutePerTenant);
        var tenantId = ResolveTenantId(httpContext);
        var key = tenantId != Guid.Empty
            ? $"webhook:{tenantId}"
            : $"webhook:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = perTenant,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

builder.Services.Configure<ProspectingAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<TextAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<MessagingLimitsOptions>(builder.Configuration.GetSection("MessagingLimits"));
builder.Services.Configure<AutomationsOptions>(builder.Configuration.GetSection("Automations"));
builder.Services.Configure<DemoOptions>(opts => opts.Enabled = demoMode);
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("followup-scheduler");
    q.AddJob<FollowupScheduler>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("followup-scheduler-trigger")
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(15)).RepeatForever()));

    var sendDueKey = new JobKey("send-due-messages");
    q.AddJob<SendDueMessagesJob>(opts => opts.WithIdentity(sendDueKey));
    q.AddTrigger(opts => opts
        .ForJob(sendDueKey)
        .WithIdentity("send-due-messages-trigger")
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever()));

    var webhookKey = new JobKey("webhook-dead-letter");
    q.AddJob<WebhookRetryJob>(opts => opts.WithIdentity(webhookKey));
    q.AddTrigger(opts => opts
        .ForJob(webhookKey)
        .WithIdentity("webhook-dead-letter-trigger")
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever()));
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = false);

// Application services
builder.Services.AddSingleton<IEncryptionService>(_ => new AesGcmEncryptionService(builder.Configuration["ENCRYPTION_KEY"] ?? "dev-key"));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddScoped<IChannelRulesService, ChannelRulesService>();
builder.Services.AddScoped<IMessageChannel, WhatsAppChannel>();
builder.Services.AddScoped<IMessageChannel, EmailChannel>();
builder.Services.AddScoped<IMessageChannel, SmsChannel>();
builder.Services.AddScoped<IChannelRegistry, ChannelRegistry>();
builder.Services.AddScoped<IChannelConfigurationProvider, ChannelConfigurationProvider>();
builder.Services.AddSingleton<OutboxDispatchExecutor>();
builder.Services.AddScoped<IMessageDispatchGuard, MessageDispatchGuard>();
builder.Services.AddSingleton<IAutomationEventPublisher, AutomationEventPublisher>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDemoExperienceService, DemoExperienceService>();
builder.Services.AddScoped<IHunterDemoSeeder, HunterDemoSeeder>();
builder.Services.AddSingleton<IProviderWebhookVerifier, ResendWebhookVerifier>();
builder.Services.AddScoped<ResendWebhookProcessor>();
builder.Services.AddScoped<IProspectingAiService, ProspectingAiService>();
builder.Services.AddScoped<IProspectingService, ProspectingService>();
builder.Services.AddScoped<ITextAiService, TextAiService>();
builder.Services.AddSingleton<IRateLimitSnapshotStore, RateLimitSnapshotStore>();
builder.Services.AddSingleton<IMessageSendRateLimiter, MessageSendRateLimiter>();
builder.Services.AddScoped<IWebhookDeadLetterStore, WebhookDeadLetterStore>();
builder.Services.AddSingleton<IHunterCsvStore, HunterCsvStore>();
builder.Services.AddSingleton<IHunterScoreService, HunterScoreService>();
builder.Services.AddSingleton<IWhyThisLeadService, WhyThisLeadService>();
builder.Services.AddScoped<IHunterEnrichService, HunterEnrichService>();
builder.Services.AddScoped<IHunterService, HunterService>();
builder.Services.AddScoped<MapsStubLeadProvider>();
builder.Services.AddScoped<ILeadProvider, MapsStubLeadProvider>();
builder.Services.AddScoped<ILeadProvider, CsvLeadProvider>();
builder.Services.AddScoped<ILeadProvider, DirectoryStubLeadProvider>();
builder.Services.AddSingleton<IMessagingLimitsProvider, MessagingLimitsProvider>();
builder.Services.AddSingleton<IFollowupProposalStore, FollowupProposalStore>();

// Background services
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<CampaignRunner>();

var app = builder.Build();

// Apply migrations at startup (dev)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");
    try
    {
        db.Database.Migrate();
        var seeder = new Pipelane.Infrastructure.Persistence.DataSeeder(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Likely no DB available in dev; log at Debug to avoid noise
        logger.LogDebug(ex, "Skipping DB migrations/seeding: {Message}", ex.Message);
    }
}

app.UseMiddleware<RequestLoggingEnrichmentMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantScopeMiddleware>();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false))
    .ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString().ToLowerInvariant(),
                error = entry.Value.Exception?.Message,
                durationMs = entry.Value.Duration.TotalMilliseconds
            }),
            timestamp = DateTimeOffset.UtcNow
        };

        if (report.Status != HealthStatus.Healthy)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HealthChecks");
            logger.LogError("Health check failed with status {Status} {@Checks}", report.Status, payload.checks);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }));
    }
}).AllowAnonymous();

app.MapGet("/health/metrics", async (IServiceProvider services, CancellationToken ct) =>
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var queueDepth = await db.Outbox.AsNoTracking()
            .CountAsync(o => o.Status == OutboxStatus.Queued || o.Status == OutboxStatus.Sending, ct)
            .ConfigureAwait(false);

        var latencies = await db.Messages.AsNoTracking()
            .Where(m => m.Direction == MessageDirection.Out && m.DeliveredAt != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .Select(m => new { m.CreatedAt, m.DeliveredAt })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var avgSendLatencyMs = latencies.Count == 0
            ? 0d
            : latencies.Average(m => (m.DeliveredAt!.Value - m.CreatedAt).TotalMilliseconds);

        var deadWebhookBacklog = await db.FailedWebhooks.AsNoTracking()
            .CountAsync(ct)
            .ConfigureAwait(false);

        return Results.Json(new
        {
            queueDepth,
            avgSendLatencyMs,
            deadWebhookBacklog,
            timestamp = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    })
    .WithName("HealthMetrics")
    .Produces(StatusCodes.Status200OK)
    .AllowAnonymous();

app.Run();

static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => (int)response.StatusCode == StatusCodes.Status429TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => (int)response.StatusCode == StatusCodes.Status429TooManyRequests)
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

static Guid ResolveTenantId(HttpContext context)
{
    if (context.Request.Query.TryGetValue("tenant", out var tenantValues)
        && Guid.TryParse(tenantValues.ToString(), out var queryTenant)
        && queryTenant != Guid.Empty)
    {
        return queryTenant;
    }

    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValues)
        && Guid.TryParse(headerValues.ToString(), out var headerTenant)
        && headerTenant != Guid.Empty)
    {
        return headerTenant;
    }

    return Guid.Empty;
}


