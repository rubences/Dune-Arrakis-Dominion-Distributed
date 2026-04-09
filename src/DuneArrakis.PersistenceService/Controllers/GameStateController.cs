using DuneArrakis.Domain.Entities;
using DuneArrakis.PersistenceService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DuneArrakis.PersistenceService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameStateController : ControllerBase
{
    private readonly IJsonPersistenceService _persistenceService;
    private readonly ILogger<GameStateController> _logger;

    public GameStateController(IJsonPersistenceService persistenceService, ILogger<GameStateController> logger)
    {
        _persistenceService = persistenceService;
        _logger = logger;
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveGame([FromBody] GameState gameState)
    {
        if (gameState is null || gameState.ActiveScenario is null)
            return BadRequest("El estado del juego no puede ser nulo.");

        var success = await _persistenceService.SaveGameAsync(gameState);
        if (!success)
            return StatusCode(500, "Error al guardar la partida.");

        return Ok(new { message = $"Partida '{gameState.SaveName}' guardada correctamente." });
    }

    [HttpGet("load/{saveName}")]
    public async Task<ActionResult<GameState>> LoadGame(string saveName)
    {
        if (string.IsNullOrWhiteSpace(saveName))
            return BadRequest("El nombre de la partida no puede estar vacío.");

        var gameState = await _persistenceService.LoadGameAsync(saveName);
        if (gameState is null)
            return NotFound($"No se encontró la partida '{saveName}'.");

        return Ok(gameState);
    }

    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<string>>> ListSaves()
    {
        var saves = await _persistenceService.ListSavesAsync();
        return Ok(saves);
    }

    [HttpDelete("delete/{saveName}")]
    public async Task<IActionResult> DeleteSave(string saveName)
    {
        if (string.IsNullOrWhiteSpace(saveName))
            return BadRequest("El nombre de la partida no puede estar vacío.");

        var success = await _persistenceService.DeleteSaveAsync(saveName);
        if (!success)
            return NotFound($"No se encontró la partida '{saveName}'.");

        return Ok(new { message = $"Partida '{saveName}' eliminada correctamente." });
    }

    [HttpGet("health")]
    public IActionResult HealthCheck() => Ok(new { status = "healthy", service = "PersistenceService" });
}
