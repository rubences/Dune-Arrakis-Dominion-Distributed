using System.Net.Http.Json;
using System.Text.Json;
using DuneArrakis.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DuneArrakis.AdminClient.Services;

public class PersistenceServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PersistenceServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PersistenceServiceClient(HttpClient httpClient, ILogger<PersistenceServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SaveGameAsync(GameState gameState)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/gamestate/save", gameState, JsonOptions);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con el Servicio de Persistencia al guardar.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al guardar la partida.");
            return false;
        }
    }

    public async Task<GameState?> LoadGameAsync(string saveName)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/gamestate/load/{Uri.EscapeDataString(saveName)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GameState>(JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con el Servicio de Persistencia al cargar '{SaveName}'.", saveName);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error de serialización al cargar la partida '{SaveName}'.", saveName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al cargar la partida '{SaveName}'.", saveName);
            return null;
        }
    }

    public async Task<IEnumerable<string>> ListSavesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/gamestate/list");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IEnumerable<string>>(JsonOptions)
                   ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar las partidas guardadas.");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/gamestate/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
