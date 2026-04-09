using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using DuneArrakis.SimulationService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DuneArrakis.SimulationService.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    [HttpPost("process-month")]
    public async Task<ActionResult<SimulationResult>> ProcessMonth([FromBody] GameState gameState)
    {
        if (gameState is null || gameState.ActiveScenario is null)
            return BadRequest("El estado del juego no puede ser nulo.");

        try
        {
            var result = await _simulationEngine.ProcessMonthAsync(gameState);
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

        try
        {
            var scenario = request.GameState.ActiveScenario;
            scenario.PurchaseCreature(request.EnclaveId, request.CreatureType);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("transfer-creature")]
    public ActionResult<GameState> TransferCreature([FromBody] TransferCreatureRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            var scenario = request.GameState.ActiveScenario;
            scenario.TransferCreature(request.SourceEnclaveId, request.TargetEnclaveId, request.CreatureId);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("build-facility")]
    public ActionResult<GameState> BuildFacility([FromBody] BuildFacilityRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            var scenario = request.GameState.ActiveScenario;
            scenario.BuildFacility(request.EnclaveId, request.FacilityType);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("feed-creature")]
    public ActionResult<GameState> FeedCreature([FromBody] FeedCreatureRequest request)
    {
        if (request is null || request.GameState is null)
            return BadRequest("La solicitud no puede ser nula.");

        try
        {
            var scenario = request.GameState.ActiveScenario;
            scenario.FeedCreature(request.CreatureId, request.FoodAmount);
            return Ok(request.GameState);
        }
        catch (DuneArrakis.Domain.Exceptions.DomainException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("health")]
    public IActionResult HealthCheck() => Ok(new { status = "healthy", service = "SimulationService" });

    [HttpGet("ai/health")]
    public async Task<IActionResult> GetCrewAiHealth(CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
        {
            return Ok(new CrewAiHealthResponse(false, "not-configured", Array.Empty<string>(),
                "Configure CrewAi:BaseUrl y CrewAi:BearerToken para habilitar la integración."));
        }

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

    [HttpGet("ai/inputs")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCrewAiInputs(CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return BadRequest("La integración con CrewAI no está configurada.");

        try
        {
            var inputs = await _crewAiClient.GetRequiredInputsAsync(cancellationToken);
            return Ok(inputs);
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

        if (request is null || request.Inputs.Count == 0)
            return BadRequest("Debe proporcionar al menos un input para el crew.");

        try
        {
            var result = await _crewAiClient.KickoffAsync(new CrewAiKickoffPayload
            {
                Inputs = request.Inputs,
                Meta = request.Meta
            }, cancellationToken);

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
        string kickoffId,
        CancellationToken cancellationToken)
    {
        if (!_crewAiClient.IsConfigured)
            return BadRequest("La integración con CrewAI no está configurada.");

        try
        {
            var status = await _crewAiClient.GetStatusAsync(kickoffId, cancellationToken);
            return Ok(status);
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
        if (request is null || request.GameState is null)
            return BadRequest("Debe proporcionar un estado del juego válido.");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest("Debe proporcionar una instrucción para el crew.");

        try
        {
            var result = await _crewAiAdvisor.GetStrategicAdviceAsync(
                request.GameState,
                request.Prompt,
                request.WaitForCompletion,
                request.MaxPollAttempts,
                request.PollIntervalSeconds,
                cancellationToken);

            if (!result.Configured)
                return BadRequest(result);

            return Ok(result);
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
        if (request is null || request.GameState is null)
            return BadRequest("Debe proporcionar un estado del juego válido.");

        try
        {
            var result = await _monthlyDecisionAutomationService.GenerateAndApplyActionsAsync(
                request.GameState,
                request.WaitForCompletion,
                request.ExecuteActions,
                request.ProcessMonthAfterActions,
                request.MaxPollAttempts,
                request.PollIntervalSeconds,
                cancellationToken);

            if (!result.Configured)
                return BadRequest(result);

            return Ok(result);
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

public record BuyCreatureRequest(GameState GameState, Guid EnclaveId, CreatureType CreatureType);
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
