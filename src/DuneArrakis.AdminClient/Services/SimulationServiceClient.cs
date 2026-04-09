using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DuneArrakis.AdminClient.Services;

public class SimulationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SimulationServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SimulationServiceClient(HttpClient httpClient, ILogger<SimulationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SimulationResultDto?> ProcessMonthAsync(GameState gameState)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/simulation/process-month", gameState, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SimulationResultDto>(JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con el Servicio de Simulación.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al procesar el mes.");
            return null;
        }
    }

    public async Task<GameState?> BuyCreatureAsync(GameState gameState, Guid enclaveId, CreatureType creatureType)
    {
        try
        {
            var request = new { gameState, enclaveId, creatureType };
            var response = await _httpClient.PostAsJsonAsync("api/simulation/buy-creature", request, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("No se pudo comprar la criatura: {Error}", error);
                throw new InvalidOperationException(error.Trim('"'));
            }
            return await response.Content.ReadFromJsonAsync<GameState>(JsonOptions);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al comprar criatura.");
            throw new InvalidOperationException("Error de conexión con el Servicio de Simulación.");
        }
    }

    public async Task<GameState?> TransferCreatureAsync(GameState gameState, Guid sourceEnclaveId, Guid targetEnclaveId, Guid creatureId)
    {
        try
        {
            var request = new { gameState, sourceEnclaveId, targetEnclaveId, creatureId };
            var response = await _httpClient.PostAsJsonAsync("api/simulation/transfer-creature", request, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("No se pudo trasladar la criatura: {Error}", error);
                throw new InvalidOperationException(error.Trim('"'));
            }
            return await response.Content.ReadFromJsonAsync<GameState>(JsonOptions);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al trasladar criatura.");
            throw new InvalidOperationException("Error de conexión con el Servicio de Simulación.");
        }
    }

    public async Task<GameState?> BuildFacilityAsync(GameState gameState, Guid enclaveId, FacilityType facilityType)
    {
        try
        {
            var request = new { gameState, enclaveId, facilityType };
            var response = await _httpClient.PostAsJsonAsync("api/simulation/build-facility", request, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error.Trim('"'));
            }
            return await response.Content.ReadFromJsonAsync<GameState>(JsonOptions);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al construir instalación.");
            throw new InvalidOperationException("Error de conexión con el Servicio de Simulación.");
        }
    }

    public async Task<GameState?> FeedCreatureAsync(GameState gameState, Guid creatureId, int foodAmount)
    {
        try
        {
            var request = new { gameState, creatureId, foodAmount };
            var response = await _httpClient.PostAsJsonAsync("api/simulation/feed-creature", request, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error.Trim('"'));
            }
            return await response.Content.ReadFromJsonAsync<GameState>(JsonOptions);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al alimentar criatura.");
            throw new InvalidOperationException("Error de conexión con el Servicio de Simulación.");
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/simulation/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<CrewAiHealthDto?> GetCrewAiHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/simulation/ai/health");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CrewAiHealthDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando el estado de CrewAI.");
            return null;
        }
    }

    public async Task<CrewAiStrategicAdviceDto?> GetStrategicAdviceAsync(
        GameState gameState,
        string prompt,
        bool waitForCompletion = true,
        int maxPollAttempts = 10,
        int pollIntervalSeconds = 3)
    {
        try
        {
            var request = new
            {
                gameState,
                prompt,
                waitForCompletion,
                maxPollAttempts,
                pollIntervalSeconds
            };

            var response = await _httpClient.PostAsJsonAsync("api/simulation/ai/strategic-advice", request, JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error.Trim('"'));
            }

            return await response.Content.ReadFromJsonAsync<CrewAiStrategicAdviceDto>(JsonOptions);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando asesoría estratégica en CrewAI.");
            throw new InvalidOperationException("Error de conexión con la integración de CrewAI.");
        }
    }
}

public class SimulationResultDto
{
    public int Month { get; set; }
    public List<SimulationEventDto> Events { get; set; } = [];
    public decimal CurrentSolaris { get; set; }
}

public class SimulationEventDto
{
    public int Month { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? SolarisChange { get; set; }
}

public class CrewAiHealthDto
{
    public bool Configured { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> RequiredInputs { get; set; } = [];
    public string? Error { get; set; }
}

public class CrewAiStrategicAdviceDto
{
    public bool Configured { get; set; }
    public string KickoffId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Advice { get; set; }
    public string? Error { get; set; }

    [JsonPropertyName("rawResponse")]
    public string? RawResponse { get; set; }
}
