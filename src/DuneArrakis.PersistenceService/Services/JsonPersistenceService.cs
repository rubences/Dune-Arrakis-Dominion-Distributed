using System.Text.Json;
using DuneArrakis.Domain.Entities;

namespace DuneArrakis.PersistenceService.Services;

public interface IJsonPersistenceService
{
    Task<bool> SaveGameAsync(GameState gameState);
    Task<GameState?> LoadGameAsync(string saveName);
    Task<IEnumerable<string>> ListSavesAsync();
    Task<bool> DeleteSaveAsync(string saveName);
}

public class JsonPersistenceService : IJsonPersistenceService
{
    private readonly string _saveDirectory;
    private readonly ILogger<JsonPersistenceService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonPersistenceService(IConfiguration configuration, ILogger<JsonPersistenceService> logger)
    {
        _saveDirectory = configuration["SaveDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "saves");
        _logger = logger;
        Directory.CreateDirectory(_saveDirectory);
    }

    public async Task<bool> SaveGameAsync(GameState gameState)
    {
        try
        {
            gameState.LastSavedAt = DateTime.UtcNow;
            var sanitizedName = SanitizeSaveName(gameState.SaveName);
            var filePath = Path.Combine(_saveDirectory, $"{sanitizedName}.json");
            var json = JsonSerializer.Serialize(gameState, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Partida guardada: {SaveName}", gameState.SaveName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la partida: {SaveName}", gameState.SaveName);
            return false;
        }
    }

    public async Task<GameState?> LoadGameAsync(string saveName)
    {
        try
        {
            var sanitizedName = SanitizeSaveName(saveName);
            var filePath = Path.Combine(_saveDirectory, $"{sanitizedName}.json");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("No se encontró la partida guardada: {SaveName}", saveName);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var gameState = JsonSerializer.Deserialize<GameState>(json, JsonOptions);
            _logger.LogInformation("Partida cargada: {SaveName}", saveName);
            return gameState;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error de serialización al cargar la partida: {SaveName}", saveName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar la partida: {SaveName}", saveName);
            return null;
        }
    }

    public Task<IEnumerable<string>> ListSavesAsync()
    {
        try
        {
            var saves = Directory.GetFiles(_saveDirectory, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)!)
                .OrderByDescending(n => n)
                .AsEnumerable();
            return Task.FromResult(saves);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar las partidas guardadas");
            return Task.FromResult(Enumerable.Empty<string>());
        }
    }

    public Task<bool> DeleteSaveAsync(string saveName)
    {
        try
        {
            var sanitizedName = SanitizeSaveName(saveName);
            var filePath = Path.Combine(_saveDirectory, $"{sanitizedName}.json");
            if (!File.Exists(filePath)) return Task.FromResult(false);
            File.Delete(filePath);
            _logger.LogInformation("Partida eliminada: {SaveName}", saveName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar la partida: {SaveName}", saveName);
            return Task.FromResult(false);
        }
    }

    private static string SanitizeSaveName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
