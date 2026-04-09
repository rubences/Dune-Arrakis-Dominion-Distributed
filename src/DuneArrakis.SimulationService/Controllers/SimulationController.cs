using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using DuneArrakis.SimulationService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DuneArrakis.SimulationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationEngine _simulationEngine;
    private readonly ICrewAiAdvisor _crewAiAdvisor;
    private readonly ICrewAiClient _crewAiClient;
    private readonly IMonthlyDecisionAutomationService _monthlyDecisionAutomationService;
    private readonly ICrewAiWebhookStore _webhookStore;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(
        ISimulationEngine simulationEngine,
        ICrewAiAdvisor crewAiAdvisor,
        ICrewAiClient crewAiClient,
        IMonthlyDecisionAutomationService monthlyDecisionAutomationService,
        ICrewAiWebhookStore webhookStore,
        ILogger<SimulationController> logger)
    {
        _simulationEngine = simulationEngine;
        _crewAiAdvisor = crewAiAdvisor;
        _crewAiClient = crewAiClient;
        _monthlyDecisionAutomationService = monthlyDecisionAutomationService;
        _webhookStore = webhookStore;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GAME STATE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Crea una nueva partida con un Escenario inicial y dos Enclaves.</summary>
    [HttpPost("new-game")]
    public ActionResult<GameState> NewGame([FromQuery] int scenarioType = 0, [FromQuery] string saveName = "Partida")
    {
        var scenario = scenarioType switch
        {
            1 => Scenario.CreateGiediPrime(),
            2 => Scenario.CreateCaladan(),
            _ => Scenario.CreateArrakeen()
        };

        // Añadir enclave de aclimatación y exhibición por defecto
        scenario.Enclaves.Add(Enclave.CreateAclimatacion("Zona de Aclimatación I"));
        scenario.Enclaves.Add(Enclave.CreateExhibicion("Gran Exhibición de Arrakis"));

        var state = GameState.NewGame(scenario, saveName);
        _logger.LogInformation("Nueva partida creada: {SaveName} — Escenario: {Scenario}", saveName, scenario.Name);
        return Ok(state);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PLAYER ACTIONS (Stateless — Backend valida y devuelve el estado mutado)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Procesa el turno mensual y dispara los Agentes IA en paralelo.</summary>
    [HttpPost("process-month")]
    public async Task<ActionResult<SimulationResult>> ProcessMonth(
        [FromBody] GameState gameState,
        CancellationToken cancellationToken)
    {
        if (gameState?.ActiveScenario is null)
            return BadRequest("El estado del juego no puede ser nulo.");

        try
        {
            var result = await _simulationEngine.ProcessMonthAsync(gameState, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el mes de simulación");
            return StatusCode(500, new { error = "Error al procesar el mes de simulación.", detail = ex.Message });
        }
    }

    /// <summary>Compra una criatura y la añade al enclave especificado.</summary>
    [HttpPost("purchase-creature")]
    public ActionResult<GameState> PurchaseCreature([FromBody] PurchaseCreatureRequest request)
    {
        if (request?.GameState?.ActiveScenario is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            request.GameState.ActiveScenario.PurchaseCreature(request.EnclaveId, request.CreatureType);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Alias legacy para compatibilidad.</summary>
    [HttpPost("buy-creature")]
    public ActionResult<GameState> BuyCreature([FromBody] PurchaseCreatureRequest request)
        => PurchaseCreature(request);

    /// <summary>Transfiere una criatura entre enclaves.</summary>
    [HttpPost("transfer-creature")]
    public ActionResult<GameState> TransferCreature([FromBody] TransferCreatureRequest request)
    {
        if (request?.GameState?.ActiveScenario is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            request.GameState.ActiveScenario.TransferCreature(
                request.SourceEnclaveId, request.TargetEnclaveId, request.CreatureId);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Construye una instalación en el enclave especificado.</summary>
    [HttpPost("build-facility")]
    public ActionResult<GameState> BuildFacility([FromBody] BuildFacilityRequest request)
    {
        if (request?.GameState?.ActiveScenario is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            request.GameState.ActiveScenario.BuildFacility(request.EnclaveId, request.FacilityType);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Alimenta una criatura con la cantidad especificada de unidades de comida.</summary>
    [HttpPost("feed-creature")]
    public ActionResult<GameState> FeedCreature([FromBody] FeedCreatureRequest request)
    {
        if (request?.GameState?.ActiveScenario is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            request.GameState.ActiveScenario.FeedCreature(request.CreatureId, request.FoodAmount);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HEALTH & DIAGNOSTICS
    // ═══════════════════════════════════════════════════════════════════════════

    [HttpGet("health")]
    public IActionResult HealthCheck() => Ok(new
    {
        status          = "healthy",
        service         = "DuneArrakis.SimulationService",
        version         = "2026.1",
        agentsEnabled   = true,
        parallelMode    = "TaskWhenAllPublisher",
        timestamp       = DateTime.UtcNow
    });

    [HttpGet("ai/health")]
    public async Task<IActionResult> GetCrewAiHealth(CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return Ok(new CrewAiHealthResponse(false, "not-configured", Array.Empty<string>(),
                "Configure CrewAi:BaseUrl y CrewAi:BearerToken para habilitar la integración."));

        try
        {
            var inputs = await _crewAiClient.GetRequiredInputsAsync(cancellationToken);
            return Ok(new CrewAiHealthResponse(true, "online", inputs, null));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el estado de CrewAI.");
            return StatusCode(502, new CrewAiHealthResponse(true, "unreachable", Array.Empty<string>(), ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AI AGENT ENDPOINTS (Direct invocation, outside the MediatR event flow)
    // ═══════════════════════════════════════════════════════════════════════════

    [HttpGet("ai/inputs")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCrewAiInputs(CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return BadRequest("La integración con CrewAI no está configurada.");
        try
        {
            return Ok(await _crewAiClient.GetRequiredInputsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo inputs requeridos de CrewAI.");
            return StatusCode(502, "No se pudieron consultar los inputs requeridos del crew.");
        }
    }

    [HttpPost("ai/kickoff")]
    public async Task<ActionResult<CrewAiKickoffResult>> StartCrewAiKickoff(
        [FromBody] CrewAiKickoffApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return BadRequest("La integración con CrewAI no está configurada.");
        if (request?.Inputs.Count == 0)
            return BadRequest("Debe proporcionar al menos un input para el crew.");
        try
        {
            var result = await _crewAiClient.KickoffAsync(
                new CrewAiKickoffPayload { Inputs = request.Inputs, Meta = request.Meta }, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error iniciando ejecución en CrewAI.");
            return StatusCode(502, "No se pudo iniciar la ejecución del crew.");
        }
    }

    [HttpGet("ai/status/{kickoffId}")]
    public async Task<ActionResult<CrewAiExecutionStatus>> GetCrewAiStatus(
        string kickoffId, CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return BadRequest("La integración con CrewAI no está configurada.");
        try
        {
            return Ok(await _crewAiClient.GetStatusAsync(kickoffId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando estado de CrewAI para kickoff {KickoffId}.", kickoffId);
            return StatusCode(502, "No se pudo consultar el estado del crew.");
        }
    }

    [HttpPost("ai/strategic-advice")]
    public async Task<ActionResult<CrewAiStrategicAdviceResult>> GetStrategicAdvice(
        [FromBody] CrewAiStrategicAdviceRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.GameState is null)
            return BadRequest("Debe proporcionar un estado del juego válido.");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest("Debe proporcionar una instrucción para el crew.");
        try
        {
            var result = await _crewAiAdvisor.GetStrategicAdviceAsync(
                request.GameState, request.Prompt,
                request.WaitForCompletion, request.MaxPollAttempts,
                request.PollIntervalSeconds, cancellationToken);

            return result.Configured ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo asesoría estratégica de CrewAI.");
            return StatusCode(502, "No se pudo completar la consulta al crew.");
        }
    }

    [HttpPost("ai/monthly-automation")]
    public async Task<ActionResult<MonthlyAutomationResult>> ExecuteMonthlyAutomation(
        [FromBody] MonthlyAutomationRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.GameState is null)
            return BadRequest("Debe proporcionar un estado del juego válido.");
        try
        {
            var result = await _monthlyDecisionAutomationService.GenerateAndApplyActionsAsync(
                request.GameState, request.WaitForCompletion, request.ExecuteActions,
                request.ProcessMonthAfterActions, request.MaxPollAttempts,
                request.PollIntervalSeconds, cancellationToken);

            return result.Configured ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando la automatización mensual impulsada por CrewAI.");
            return StatusCode(502, "No se pudo completar la automatización mensual del crew.");
        }
    }

    [HttpPost("ai/webhooks/{source}")]
    public IActionResult ReceiveCrewAiWebhook(string source, [FromBody] JsonElement payload)
    {
        _webhookStore.Store(source, payload);
        _logger.LogInformation("Webhook de CrewAI recibido para source {Source}.", source);
        return Accepted();
    }
}

// ── Request Records ───────────────────────────────────────────────────────────
public record PurchaseCreatureRequest(GameState GameState, Guid EnclaveId, CreatureType CreatureType);
public record TransferCreatureRequest(GameState GameState, Guid SourceEnclaveId, Guid TargetEnclaveId, Guid CreatureId);
public record BuildFacilityRequest(GameState GameState, Guid EnclaveId, FacilityType FacilityType);
public record FeedCreatureRequest(GameState GameState, Guid CreatureId, int FoodAmount);
public record CrewAiKickoffApiRequest(Dictionary<string, string> Inputs, Dictionary<string, object?>? Meta);
public record CrewAiStrategicAdviceRequest(
    GameState GameState,
    string Prompt,
    bool WaitForCompletion = true,
    int MaxPollAttempts = 10,
    int PollIntervalSeconds = 3);
public record MonthlyAutomationRequest(
    GameState GameState,
    bool WaitForCompletion = true,
    bool ExecuteActions = true,
    bool ProcessMonthAfterActions = false,
    int MaxPollAttempts = 10,
    int PollIntervalSeconds = 3);
public record CrewAiHealthResponse(bool Configured, string Status, IReadOnlyList<string> RequiredInputs, string? Error);
