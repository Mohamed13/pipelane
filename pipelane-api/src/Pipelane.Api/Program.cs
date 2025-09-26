using System.Reflection;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pipelane.Api.MultiTenancy;
using Pipelane.Application.Abstractions;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Channels;
using Pipelane.Infrastructure.Persistence;
using Pipelane.Infrastructure.Security;
using Serilog;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// DbContext
var conn = builder.Configuration.GetConnectionString("Postgres")
           ?? Environment.GetEnvironmentVariable("DB_CONNECTION")
           ?? "Host=localhost;Port=5432;Database=pipelane;Username=postgres;Password=postgres";
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Pipelane.Api"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Auth (JWT optional dev)
var jwtKey = builder.Configuration["JWT_KEY"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev-secret-key-please-change";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
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

builder.Services.AddProblemDetails();
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

// Http clients + Polly
builder.Services.AddHttpClient();

// Application services
builder.Services.AddSingleton<IEncryptionService>(_ => new AesGcmEncryptionService(builder.Configuration["ENCRYPTION_KEY"] ?? "dev-key"));
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddScoped<IChannelRulesService, ChannelRulesService>();
builder.Services.AddSingleton<IMessageChannel, WhatsAppChannel>();
builder.Services.AddSingleton<IMessageChannel, EmailChannel>();
builder.Services.AddSingleton<IMessageChannel, SmsChannel>();
builder.Services.AddSingleton<IChannelRegistry, ChannelRegistry>();
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Background services
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<CampaignRunner>();
builder.Services.AddHostedService<FollowupScheduler>();

var app = builder.Build();

// Apply migrations at startup (dev)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        db.Database.Migrate();
        var seeder = new Pipelane.Infrastructure.Persistence.DataSeeder(db);
        await seeder.SeedAsync();
    } catch { /* swallow on first run without DB */ }
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
