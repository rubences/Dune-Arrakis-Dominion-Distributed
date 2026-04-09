using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DuneArrakis.SimulationService.Services;

public interface IDecisionCrewAiClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<string>> GetRequiredInputsAsync(CancellationToken cancellationToken = default);
    Task<CrewAiKickoffResult> KickoffAsync(CrewAiKickoffPayload payload, CancellationToken cancellationToken = default);
    Task<CrewAiExecutionStatus> GetStatusAsync(string kickoffId, CancellationToken cancellationToken = default);
}

public class DecisionCrewAiClient : IDecisionCrewAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DecisionCrewAiOptions _options;
    private readonly ILogger<DecisionCrewAiClient> _logger;

    public DecisionCrewAiClient(HttpClient httpClient, IOptions<DecisionCrewAiOptions> options, ILogger<DecisionCrewAiClient> logger)
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
        return response?.Inputs ?? new List<string>();
    }

    public async Task<CrewAiKickoffResult> KickoffAsync(CrewAiKickoffPayload payload, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var response = await _httpClient.PostAsJsonAsync("/kickoff", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CrewAiKickoffResult>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("El crew de decisiones no devolvió un kickoff_id válido.");
    }

    public async Task<CrewAiExecutionStatus> GetStatusAsync(string kickoffId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        foreach (var path in GetStatusPaths(kickoffId))
        {
            var response = await _httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                continue;

            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;

            return new CrewAiExecutionStatus
            {
                KickoffId = kickoffId,
                Status = FindFirstAvailableString(root, "status", "state") ?? "unknown",
                ResultText = FindFirstAvailableString(root, "result", "output", "raw", "final_output", "response"),
                Error = FindFirstAvailableString(root, "error", "message", "detail"),
                RawJson = rawJson
            };
        }

        throw new InvalidOperationException($"No se encontró un endpoint de estado válido para el kickoff '{kickoffId}'.");
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Se intentó usar el crew de decisiones sin configurar BaseUrl o BearerToken.");
            throw new InvalidOperationException("La integración con el crew de decisiones no está configurada.");
        }
    }

    private static IEnumerable<string> GetStatusPaths(string kickoffId)
    {
        yield return $"/{Uri.EscapeDataString(kickoffId)}/status";
        yield return $"/status/{Uri.EscapeDataString(kickoffId)}";
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