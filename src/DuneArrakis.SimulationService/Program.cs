using System.Net.Http.Headers;
using DuneArrakis.SimulationService.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Dune Simulation Service", Version = "v1" });
});

// Orquestación Multi-Agente con MediatR
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    // Configuramos publicador paralelo para que ambos agentes (Logistics y Strategic) se ejecuten en paralelo simultáneamente
    cfg.NotificationPublisher = new MediatR.NotificationPublishers.TaskWhenAllPublisher();
    cfg.NotificationPublisherType = typeof(MediatR.NotificationPublishers.TaskWhenAllPublisher);
});

builder.Services.AddOptions<CrewAiOptions>()
    .Bind(builder.Configuration.GetSection(CrewAiOptions.SectionName));

builder.Services.AddOptions<DecisionCrewAiOptions>()
    .Bind(builder.Configuration.GetSection(DecisionCrewAiOptions.SectionName));

static void ConfigureCrewAiHttpClient(HttpClient client, string baseUrl, int timeout, string token)
{
    if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;

    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeout));

    if (!string.IsNullOrWhiteSpace(token))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

builder.Services.AddHttpClient<ICrewAiClient, CrewAiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CrewAiOptions>>().Value;
    ConfigureCrewAiHttpClient(client, options.BaseUrl, options.RequestTimeoutSeconds, options.BearerToken);
});

builder.Services.AddSingleton<ISimulationEngine, SimulationEngine>();
builder.Services.AddSingleton<ICrewAiAdvisor, CrewAiAdvisor>();
builder.Services.AddSingleton<ICrewAiWebhookStore, CrewAiWebhookStore>();

builder.Services.AddHttpClient<IDecisionCrewAiClient, DecisionCrewAiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DecisionCrewAiOptions>>().Value;
    ConfigureCrewAiHttpClient(client, options.BaseUrl, options.RequestTimeoutSeconds, options.BearerToken);
});

builder.Services.AddSingleton<IMonthlyDecisionAutomationService, MonthlyDecisionAutomationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
