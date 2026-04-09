using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using DuneArrakis.SimulationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DuneArrakis.SimulationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationEngine _simulationEngine;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(ISimulationEngine simulationEngine, ILogger<SimulationController> logger)
    {
        _simulationEngine = simulationEngine;
        _logger = logger;
    }

    [HttpPost("process-month")]
    public ActionResult<SimulationResult> ProcessMonth([FromBody] GameState gameState)
    {
        if (gameState is null || gameState.ActiveScenario is null)
            return BadRequest("El estado del juego no puede ser nulo.");

        try
        {
            var result = _simulationEngine.ProcessMonth(gameState);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el mes de simulación");
            return StatusCode(500, "Error al procesar el mes de simulación.");
        }
    }

    [HttpPost("buy-creature")]
    public ActionResult<GameState> BuyCreature([FromBody] BuyCreatureRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        var scenario = request.GameState.ActiveScenario;
        var enclave = scenario.Enclaves.FirstOrDefault(e => e.Id == request.EnclaveId);
        if (enclave is null)
            return NotFound($"No se encontró el enclave '{request.EnclaveId}'.");

        if (!Creature.Templates.TryGetValue(request.CreatureType, out var template))
            return BadRequest($"Tipo de criatura no válido: {request.CreatureType}.");

        if (scenario.CurrentSolaris < template.AcquisitionCost)
            return BadRequest($"Saldo insuficiente. Necesita {template.AcquisitionCost:N0} Solaris pero solo tiene {scenario.CurrentSolaris:N0}.");

        if (enclave.Creatures.Count(c => c.IsAlive) >= enclave.MaxCreatureCapacity)
            return BadRequest($"El enclave '{enclave.Name}' está lleno (capacidad máxima: {enclave.MaxCreatureCapacity}).");

        var creature = Creature.Create(request.CreatureType);
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);
        scenario.CurrentSolaris -= template.AcquisitionCost;

        scenario.EventLog.Add(new SimulationEvent
        {
            Month = scenario.CurrentMonth,
            EventType = "Compra",
            Description = $"Adquirido {creature.Name} para {enclave.Name}. Coste: {template.AcquisitionCost:N0} Solaris.",
            SolarisChange = -template.AcquisitionCost,
            CreatureId = creature.Id,
            EnclaveId = enclave.Id
        });

        return Ok(request.GameState);
    }

    [HttpPost("transfer-creature")]
    public ActionResult<GameState> TransferCreature([FromBody] TransferCreatureRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        var scenario = request.GameState.ActiveScenario;
        var sourceEnclave = scenario.Enclaves.FirstOrDefault(e => e.Id == request.SourceEnclaveId);
        var targetEnclave = scenario.Enclaves.FirstOrDefault(e => e.Id == request.TargetEnclaveId);

        if (sourceEnclave is null)
            return NotFound($"No se encontró el enclave origen '{request.SourceEnclaveId}'.");
        if (targetEnclave is null)
            return NotFound($"No se encontró el enclave destino '{request.TargetEnclaveId}'.");

        var creature = sourceEnclave.Creatures.FirstOrDefault(c => c.Id == request.CreatureId);
        if (creature is null)
            return NotFound($"No se encontró la criatura '{request.CreatureId}'.");

        if (!creature.IsAlive)
            return BadRequest("No se puede trasladar una criatura que no está viva.");

        if (creature.Health < 75)
            return BadRequest($"No se puede trasladar '{creature.Name}'. La criatura necesita al menos 75 de salud (actual: {creature.Health}).");

        if (targetEnclave.Creatures.Count(c => c.IsAlive) >= targetEnclave.MaxCreatureCapacity)
            return BadRequest($"El enclave destino '{targetEnclave.Name}' está lleno.");

        sourceEnclave.Creatures.Remove(creature);
        creature.EnclaveId = targetEnclave.Id;
        targetEnclave.Creatures.Add(creature);

        scenario.EventLog.Add(new SimulationEvent
        {
            Month = scenario.CurrentMonth,
            EventType = "Traslado",
            Description = $"{creature.Name} trasladado de '{sourceEnclave.Name}' a '{targetEnclave.Name}'.",
            CreatureId = creature.Id,
            EnclaveId = targetEnclave.Id
        });

        return Ok(request.GameState);
    }

    [HttpPost("build-facility")]
    public ActionResult<GameState> BuildFacility([FromBody] BuildFacilityRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        var scenario = request.GameState.ActiveScenario;
        var enclave = scenario.Enclaves.FirstOrDefault(e => e.Id == request.EnclaveId);
        if (enclave is null)
            return NotFound($"No se encontró el enclave '{request.EnclaveId}'.");

        if (!Facility.Catalog.TryGetValue(request.FacilityType, out var catalogEntry))
            return BadRequest($"Tipo de instalación no válido: {request.FacilityType}.");

        var (name, cost, _) = catalogEntry;
        if (scenario.CurrentSolaris < cost)
            return BadRequest($"Saldo insuficiente. Necesita {cost:N0} Solaris pero solo tiene {scenario.CurrentSolaris:N0}.");

        var facility = Facility.Create(request.FacilityType);
        enclave.Facilities.Add(facility);
        scenario.CurrentSolaris -= cost;

        scenario.EventLog.Add(new SimulationEvent
        {
            Month = scenario.CurrentMonth,
            EventType = "Construccion",
            Description = $"Construida instalación '{name}' en {enclave.Name}. Coste: {cost:N0} Solaris.",
            SolarisChange = -cost,
            EnclaveId = enclave.Id
        });

        return Ok(request.GameState);
    }

    [HttpPost("feed-creature")]
    public ActionResult<GameState> FeedCreature([FromBody] FeedCreatureRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        var scenario = request.GameState.ActiveScenario;
        var enclave = scenario.Enclaves
            .FirstOrDefault(e => e.Creatures.Any(c => c.Id == request.CreatureId));

        if (enclave is null)
            return NotFound("No se encontró la criatura en ningún enclave.");

        var creature = enclave.Creatures.FirstOrDefault(c => c.Id == request.CreatureId);
        if (creature is null)
            return NotFound($"Criatura '{request.CreatureId}' no encontrada.");

        if (!creature.IsAlive)
            return BadRequest("No se puede alimentar una criatura que no está viva.");

        var foodCost = creature.MonthlyFoodCost * (decimal)request.FoodAmount / creature.FoodRequiredPerMonth;
        if (scenario.CurrentSolaris < foodCost)
            return BadRequest($"Saldo insuficiente para alimentar a {creature.Name}.");

        creature.FoodConsumedThisMonth = Math.Min(
            creature.FoodConsumedThisMonth + request.FoodAmount,
            creature.FoodRequiredPerMonth);

        scenario.CurrentSolaris -= foodCost;

        return Ok(request.GameState);
    }

    [HttpGet("health")]
    public IActionResult HealthCheck() => Ok(new { status = "healthy", service = "SimulationService" });
}

public record BuyCreatureRequest(GameState GameState, Guid EnclaveId, CreatureType CreatureType);
public record TransferCreatureRequest(GameState GameState, Guid SourceEnclaveId, Guid TargetEnclaveId, Guid CreatureId);
public record BuildFacilityRequest(GameState GameState, Guid EnclaveId, FacilityType FacilityType);
public record FeedCreatureRequest(GameState GameState, Guid CreatureId, int FoodAmount);
