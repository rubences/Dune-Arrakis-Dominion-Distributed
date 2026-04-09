using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DuneArrakis.SimulationService.Services;

public interface ICrewAiClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<string>> GetRequiredInputsAsync(CancellationToken cancellationToken = default);
    Task<CrewAiKickoffResult> KickoffAsync(CrewAiKickoffPayload payload, CancellationToken cancellationToken = default);
    Task<CrewAiExecutionStatus> GetStatusAsync(string kickoffId, CancellationToken cancellationToken = default);
}

public class CrewAiClient : ICrewAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly CrewAiOptions _options;
    private readonly ILogger<CrewAiClient> _logger;

    public CrewAiClient(HttpClient httpClient, IOptions<CrewAiOptions> options, ILogger<CrewAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<string>> GetRequiredInputsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var response = await _httpClient.GetFromJsonAsync<CrewAiInputsEnvelope>("/inputs", JsonOptions, cancellationToken);
        return response?.Inputs ?? Array.Empty<string>();
    }

    public async Task<CrewAiKickoffResult> KickoffAsync(CrewAiKickoffPayload payload, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var response = await _httpClient.PostAsJsonAsync("/kickoff", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CrewAiKickoffResult>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("CrewAI no devolvió un kickoff_id válido.");
    }

    public async Task<CrewAiExecutionStatus> GetStatusAsync(string kickoffId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var response = await _httpClient.GetAsync($"/{Uri.EscapeDataString(kickoffId)}/status", cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        return new CrewAiExecutionStatus
        {
            KickoffId = kickoffId,
            Status = FindString(root, "status") ?? "unknown",
            ResultText = FindFirstAvailableString(root, "result", "output", "raw", "final_output", "response"),
            Error = FindFirstAvailableString(root, "error", "message"),
            RawJson = rawJson
        };
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Se intentó usar CrewAI sin configurar BaseUrl o BearerToken.");
            throw new InvalidOperationException("La integración con CrewAI no está configurada.");
        }
    }

    private static string? FindFirstAvailableString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = FindString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.NameEquals(propertyName) &&
                !string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Object => property.Value.ToString(),
                JsonValueKind.Array => property.Value.ToString(),
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
                continue;

            var nested = FindString(property.Value, propertyName);
            if (!string.IsNullOrWhiteSpace(nested))
                return nested;
        }

        return null;
    }
}

public class CrewAiInputsEnvelope
{
    public List<string> Inputs { get; set; } = [];
}

public class CrewAiKickoffPayload
{
    public Dictionary<string, string> Inputs { get; set; } = [];
    public Dictionary<string, object?>? Meta { get; set; }
    public string? TaskWebhookUrl { get; set; }
    public string? StepWebhookUrl { get; set; }
    public string? CrewWebhookUrl { get; set; }
}

public class CrewAiKickoffResult
{
    public string KickoffId { get; set; } = string.Empty;
}

public class CrewAiExecutionStatus
{
    public string KickoffId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResultText { get; set; }
    public string? Error { get; set; }
    public string RawJson { get; set; } = string.Empty;

    public bool IsTerminal =>
        string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);
}