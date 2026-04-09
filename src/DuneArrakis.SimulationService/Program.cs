using System.Net.Http.Headers;
using DuneArrakis.SimulationService.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────────────
// CORS — permite conexiones desde Unity (localhost cualquier puerto) y Swagger UI
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DunePolicy", policy =>
        policy
            .SetIsOriginAllowed(_ => true) 
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// Soporte para proxies (Railway/Render)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ──────────────────────────────────────────────────────────────────────────────
// Controllers + Swagger
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialización camelCase para que Unity JsonUtility lo deserialice bien
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title   = "🏜 Dune Arrakis Dominion — Simulation Service",
        Version = "v2026.1",
        Description = "Backend de Orquestación Multi-Agente para el demostrador Dune Arrakis Dominion"
    });
});

// ──────────────────────────────────────────────────────────────────────────────
// MediatR — Publicación PARALELA de eventos entre agentes
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    // TaskWhenAllPublisher → todos los INotificationHandler se disparan en paralelo
    cfg.NotificationPublisher     = new MediatR.NotificationPublishers.TaskWhenAllPublisher();
    cfg.NotificationPublisherType = typeof(MediatR.NotificationPublishers.TaskWhenAllPublisher);
});

// ──────────────────────────────────────────────────────────────────────────────
// Options
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddOptions<CrewAiOptions>()
    .Bind(builder.Configuration.GetSection(CrewAiOptions.SectionName));

builder.Services.AddOptions<DecisionCrewAiOptions>()
    .Bind(builder.Configuration.GetSection(DecisionCrewAiOptions.SectionName));

// ──────────────────────────────────────────────────────────────────────────────
// HTTP Clients para CrewAI (helper local)
// ──────────────────────────────────────────────────────────────────────────────
static void ConfigureCrewAiHttpClient(HttpClient client, string baseUrl, int timeout, string token)
{
    if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;

    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeout));

    if (!string.IsNullOrWhiteSpace(token))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

builder.Services.AddHttpClient<ICrewAiClient, CrewAiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<CrewAiOptions>>().Value;
    ConfigureCrewAiHttpClient(client, opts.BaseUrl, opts.RequestTimeoutSeconds, opts.BearerToken);
});

builder.Services.AddHttpClient<IDecisionCrewAiClient, DecisionCrewAiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<DecisionCrewAiOptions>>().Value;
    ConfigureCrewAiHttpClient(client, opts.BaseUrl, opts.RequestTimeoutSeconds, opts.BearerToken);
});

// ──────────────────────────────────────────────────────────────────────────────
// Servicios de Dominio
// IMPORTANTE: SimulationEngine usa IPublisher (scoped en MediatR), por eso
// lo registramos como Scoped (no Singleton) para evitar captive-dependency.
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISimulationEngine, SimulationEngine>();
builder.Services.AddSingleton<ICrewAiAdvisor, CrewAiAdvisor>();
builder.Services.AddSingleton<ICrewAiWebhookStore, CrewAiWebhookStore>();
builder.Services.AddSingleton<IMonthlyDecisionAutomationService, MonthlyDecisionAutomationService>();

// ──────────────────────────────────────────────────────────────────────────────
// Build & Middleware Pipeline
// ──────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Always expose Swagger (útil para demos)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dune Simulation Service v2026.1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Dune Arrakis — API Docs";
});

app.UseForwardedHeaders();

app.UseCors("DunePolicy");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
