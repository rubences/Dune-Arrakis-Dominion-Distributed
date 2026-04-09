using System.Globalization;
using System.Text.Json;
using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DuneArrakis.SimulationService.Services;

public interface IMonthlyDecisionAutomationService
{
    Task<MonthlyAutomationResult> GenerateAndApplyActionsAsync(
        GameState gameState,
        bool waitForCompletion,
        bool executeActions,
        bool processMonthAfterActions,
        int maxPollAttempts,
        int pollIntervalSeconds,
        CancellationToken cancellationToken = default);
}

public class MonthlyDecisionAutomationService : IMonthlyDecisionAutomationService
{
    private const decimal SupplyUnitCost = 5m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDecisionCrewAiClient _decisionCrewAiClient;
    private readonly DecisionCrewAiOptions _options;
    private readonly ISimulationEngine _simulationEngine;
    private readonly ILogger<MonthlyDecisionAutomationService> _logger;

    public MonthlyDecisionAutomationService(
        IDecisionCrewAiClient decisionCrewAiClient,
        IOptions<DecisionCrewAiOptions> options,
        ISimulationEngine simulationEngine,
        ILogger<MonthlyDecisionAutomationService> logger)
    {
        _decisionCrewAiClient = decisionCrewAiClient;
        _options = options.Value;
        _simulationEngine = simulationEngine;
        _logger = logger;
    }

    public async Task<MonthlyAutomationResult> GenerateAndApplyActionsAsync(
        GameState gameState,
        bool waitForCompletion,
        bool executeActions,
        bool processMonthAfterActions,
        int maxPollAttempts,
        int pollIntervalSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_decisionCrewAiClient.IsConfigured)
        {
            return new MonthlyAutomationResult
            {
                Configured = false,
                Status = "not-configured",
                Error = "Configure DecisionCrewAi:BaseUrl y DecisionCrewAi:BearerToken antes de usar la automatización mensual.",
                GameState = gameState
            };
        }

        var payload = await BuildPayloadAsync(gameState, cancellationToken);
        var kickoff = await _decisionCrewAiClient.KickoffAsync(payload, cancellationToken);

        if (!waitForCompletion)
        {
            return new MonthlyAutomationResult
            {
                Configured = true,
                KickoffId = kickoff.KickoffId,
                Status = "submitted",
                GameState = gameState
            };
        }

        CrewAiExecutionStatus? latestStatus = null;
        var attempts = Math.Max(1, maxPollAttempts);
        var intervalSeconds = Math.Clamp(pollIntervalSeconds, 1, 30);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            latestStatus = await _decisionCrewAiClient.GetStatusAsync(kickoff.KickoffId, cancellationToken);
            if (latestStatus.IsTerminal)
                break;

            if (attempt < attempts - 1)
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
        }

        latestStatus ??= new CrewAiExecutionStatus
        {
            KickoffId = kickoff.KickoffId,
            Status = "unknown"
        };

        var actions = ParseActions(latestStatus.ResultText ?? latestStatus.RawJson);
        var result = new MonthlyAutomationResult
        {
            Configured = true,
            KickoffId = kickoff.KickoffId,
            Status = latestStatus.Status,
            Actions = actions,
            RawResponse = latestStatus.RawJson,
            Error = latestStatus.Error,
            GameState = gameState
        };

        if (executeActions)
            ApplyActions(gameState, result);

        if (processMonthAfterActions)
            result.SimulationResult = _simulationEngine.ProcessMonth(gameState);

        return result;
    }

    private async Task<CrewAiKickoffPayload> BuildPayloadAsync(GameState gameState, CancellationToken cancellationToken)
    {
        var requiredInputs = await _decisionCrewAiClient.GetRequiredInputsAsync(cancellationToken);
        var canonical = BuildCanonicalInputs(gameState);
        var inputs = new Dictionary<string, string>();

        if (requiredInputs.Count == 0)
        {
            foreach (var pair in canonical)
                inputs[pair.Key] = pair.Value;
        }
        else
        {
            foreach (var requiredInput in requiredInputs)
                inputs[requiredInput] = ResolveInputValue(requiredInput, canonical);
        }

        return new CrewAiKickoffPayload
        {
            Inputs = inputs,
            Meta = new Dictionary<string, object?>
            {
                ["source"] = "DuneArrakis.SimulationService.MonthlyDecisionAutomationService",
                ["saveName"] = gameState.SaveName,
                ["month"] = gameState.ActiveScenario.CurrentMonth
            }
        };
    }

    private Dictionary<string, string> BuildCanonicalInputs(GameState gameState)
    {
        var scenario = gameState.ActiveScenario;
        return new Dictionary<string, string>
        {
            ["game_name"] = _options.DefaultGameName,
            ["game_state"] = JsonSerializer.Serialize(gameState, JsonOptions),
            ["month"] = scenario.CurrentMonth.ToString(CultureInfo.InvariantCulture),
            ["instructions"] = "Devuelve exclusivamente JSON con comprar_suministros, trasladar_criaturas y registrar_letargo.",
            ["resource_rules"] = "Cada suministro cuesta 5 solaris. Traslada adultos sanos en Aclimatacion a Exhibicion si salud > 75.",
            ["supplies_available"] = scenario.StoredFoodUnits.ToString(CultureInfo.InvariantCulture),
            ["creatures_summary"] = BuildCreaturesSummary(gameState)
        };
    }

    private string ResolveInputValue(string inputName, IReadOnlyDictionary<string, string> canonical)
    {
        if (canonical.TryGetValue(inputName, out var directValue))
            return directValue;

        var lower = inputName.ToLowerInvariant();
        if (lower.Contains("game") && canonical.TryGetValue("game_name", out var gameName))
            return gameName;
        if (lower.Contains("state") && canonical.TryGetValue("game_state", out var gameState))
            return gameState;
        if (lower.Contains("month") && canonical.TryGetValue("month", out var month))
            return month;
        if ((lower.Contains("rule") || lower.Contains("resource")) && canonical.TryGetValue("resource_rules", out var rules))
            return rules;
        if ((lower.Contains("instruction") || lower.Contains("prompt")) && canonical.TryGetValue("instructions", out var instructions))
            return instructions;

        return canonical["game_state"];
    }

    private static string BuildCreaturesSummary(GameState gameState)
    {
        var lines = new List<string>();
        foreach (var enclave in gameState.ActiveScenario.Enclaves)
        {
            foreach (var creature in enclave.Creatures.OrderByDescending(creature => creature.Health))
            {
                lines.Add(
                    $"id={creature.Id}; nombre={creature.Name}; enclave={enclave.Type}; salud={creature.Health}; " +
                    $"edad_meses={creature.AgeInMonths}; es_adulta={(creature.AgeInMonths >= 12 ? "true" : "false")}; viva={(creature.IsAlive ? "true" : "false")}; alimento_requerido={creature.FoodRequiredPerMonth}");
            }
        }

        return string.Join("\n", lines);
    }

    private void ApplyActions(GameState gameState, MonthlyAutomationResult result)
    {
        if (result.Actions is null)
            return;

        ApplySupplyPurchase(gameState, result);
        ApplyFoodDistribution(gameState, result);
        ApplyTransfers(gameState, result);
        ApplyLethargy(gameState, result);
        result.ActionsApplied = true;
    }

    private static void ApplySupplyPurchase(GameState gameState, MonthlyAutomationResult result)
    {
        var scenario = gameState.ActiveScenario;
        var requestedUnits = Math.Max(0, result.Actions?.ComprarSuministros ?? 0);
        var affordableUnits = (int)Math.Min(requestedUnits, decimal.Truncate(scenario.CurrentSolaris / SupplyUnitCost));

        if (affordableUnits <= 0)
            return;

        scenario.StoredFoodUnits += affordableUnits;
        var totalCost = affordableUnits * SupplyUnitCost;
        scenario.CurrentSolaris -= totalCost;
        result.PurchasedSupplyUnits = affordableUnits;

        scenario.EventLog.Add(new SimulationEvent
        {
            Month = scenario.CurrentMonth,
            EventType = "Suministros",
            Description = $"Compra automática de {affordableUnits} suministros para el próximo mes.",
            SolarisChange = -totalCost
        });
    }

    private static void ApplyFoodDistribution(GameState gameState, MonthlyAutomationResult result)
    {
        var scenario = gameState.ActiveScenario;
        var aliveCreatures = scenario.Enclaves
            .SelectMany(enclave => enclave.Creatures)
            .Where(creature => creature.IsAlive)
            .OrderBy(creature => creature.Health)
            .ToList();

        var allocatedUnits = 0;
        foreach (var creature in aliveCreatures)
        {
            if (scenario.StoredFoodUnits <= 0)
                break;

            var missingUnits = Math.Max(0, creature.FoodRequiredPerMonth - creature.FoodConsumedThisMonth);
            if (missingUnits <= 0)
                continue;

            var units = Math.Min(missingUnits, scenario.StoredFoodUnits);
            creature.FoodConsumedThisMonth += units;
            scenario.StoredFoodUnits -= units;
            allocatedUnits += units;
        }

        result.AllocatedFoodUnits = allocatedUnits;

        if (allocatedUnits > 0)
        {
            scenario.EventLog.Add(new SimulationEvent
            {
                Month = scenario.CurrentMonth,
                EventType = "Abastecimiento",
                Description = $"Distribución automática de {allocatedUnits} unidades de alimento entre criaturas vivas."
            });
        }
    }

    private static void ApplyTransfers(GameState gameState, MonthlyAutomationResult result)
    {
        var actions = result.Actions;
        if (actions is null || actions.TrasladarCriaturas.Count == 0)
            return;

        var scenario = gameState.ActiveScenario;
        var exhibitionEnclaves = scenario.Enclaves.Where(enclave => enclave.Type == EnclaveType.Exhibicion).ToList();
        if (exhibitionEnclaves.Count == 0)
            return;

        foreach (var creatureIdText in actions.TrasladarCriaturas)
        {
            if (!Guid.TryParse(creatureIdText, out var creatureId))
                continue;

            var sourceEnclave = scenario.Enclaves.FirstOrDefault(enclave => enclave.Creatures.Any(creature => creature.Id == creatureId));
            var creature = sourceEnclave?.Creatures.FirstOrDefault(creature => creature.Id == creatureId);
            if (sourceEnclave is null || creature is null || !creature.IsAlive)
                continue;

            var targetEnclave = exhibitionEnclaves.FirstOrDefault(enclave => enclave.Creatures.Count(c => c.IsAlive) < enclave.MaxCreatureCapacity);
            if (targetEnclave is null)
                continue;

            if (sourceEnclave.Type != EnclaveType.Aclimatacion || creature.Health <= 75 || creature.AgeInMonths < 12)
                continue;

            sourceEnclave.Creatures.Remove(creature);
            creature.EnclaveId = targetEnclave.Id;
            targetEnclave.Creatures.Add(creature);
            result.ExecutedTransfers.Add(creature.Id);

            scenario.EventLog.Add(new SimulationEvent
            {
                Month = scenario.CurrentMonth,
                EventType = "TrasladoAutomatico",
                Description = $"{creature.Name} trasladado automáticamente a {targetEnclave.Name} por el crew de decisiones.",
                CreatureId = creature.Id,
                EnclaveId = targetEnclave.Id
            });
        }
    }

    private static void ApplyLethargy(GameState gameState, MonthlyAutomationResult result)
    {
        var actions = result.Actions;
        if (actions is null || actions.RegistrarLetargo.Count == 0)
            return;

        var scenario = gameState.ActiveScenario;
        foreach (var creatureIdText in actions.RegistrarLetargo)
        {
            if (!Guid.TryParse(creatureIdText, out var creatureId))
                continue;

            var enclave = scenario.Enclaves.FirstOrDefault(item => item.Creatures.Any(creature => creature.Id == creatureId));
            var creature = enclave?.Creatures.FirstOrDefault(item => item.Id == creatureId);
            if (creature is null || creature.Health > 0)
                continue;

            creature.IsAlive = false;
            result.RegisteredLethargy.Add(creature.Id);
            scenario.EventLog.Add(new SimulationEvent
            {
                Month = scenario.CurrentMonth,
                EventType = "Letargo",
                Description = $"{creature.Name} ha sido registrado automáticamente en letargo.",
                CreatureId = creature.Id,
                EnclaveId = enclave?.Id
            });
        }
    }

    private MonthlyAutomationActions ParseActions(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new MonthlyAutomationActions();

        var normalized = ExtractJson(rawText);
        if (normalized is null)
        {
            _logger.LogWarning("No se pudo extraer JSON de acciones del crew de decisiones. Se devuelve salida vacía.");
            return new MonthlyAutomationActions { RawOutput = rawText };
        }

        try
        {
            return JsonSerializer.Deserialize<MonthlyAutomationActions>(normalized, JsonOptions)
                   ?? new MonthlyAutomationActions { RawOutput = rawText };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "El JSON devuelto por el crew de decisiones no es válido. Se conserva la salida cruda.");
            return new MonthlyAutomationActions { RawOutput = rawText };
        }
    }

    private static string? ExtractJson(string rawText)
    {
        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                return trimmed[firstBrace..(lastBrace + 1)];
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return null;
    }
}

public class MonthlyAutomationActions
{
    public int ComprarSuministros { get; set; }
    public List<string> TrasladarCriaturas { get; set; } = [];
    public List<string> RegistrarLetargo { get; set; } = [];
    public string? RawOutput { get; set; }
}

public class MonthlyAutomationResult
{
    public bool Configured { get; set; }
    public string KickoffId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? RawResponse { get; set; }
    public bool ActionsApplied { get; set; }
    public int PurchasedSupplyUnits { get; set; }
    public int AllocatedFoodUnits { get; set; }
    public List<Guid> ExecutedTransfers { get; set; } = [];
    public List<Guid> RegisteredLethargy { get; set; } = [];
    public MonthlyAutomationActions? Actions { get; set; }
    public SimulationResult? SimulationResult { get; set; }
    public GameState? GameState { get; set; }
}