using System.Globalization;
using System.Text;
using System.Text.Json;
using DuneArrakis.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DuneArrakis.SimulationService.Services;

public interface ICrewAiAdvisor
{
    Task<CrewAiStrategicAdviceResult> GetStrategicAdviceAsync(
        GameState gameState,
        string prompt,
        bool waitForCompletion,
        int maxPollAttempts,
        int pollIntervalSeconds,
        CancellationToken cancellationToken = default);
}

public class CrewAiAdvisor : ICrewAiAdvisor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICrewAiClient _crewAiClient;
    private readonly CrewAiOptions _options;

    public CrewAiAdvisor(ICrewAiClient crewAiClient, IOptions<CrewAiOptions> options)
    {
        _crewAiClient = crewAiClient;
        _options = options.Value;
    }

    public async Task<CrewAiStrategicAdviceResult> GetStrategicAdviceAsync(
        GameState gameState,
        string prompt,
        bool waitForCompletion,
        int maxPollAttempts,
        int pollIntervalSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_crewAiClient.IsConfigured)
        {
            return new CrewAiStrategicAdviceResult
            {
                Configured = false,
                Status = "not-configured",
                Error = "Configure CrewAi:BaseUrl y CrewAi:BearerToken antes de usar el asesor estratégico."
            };
        }

        var kickoff = await _crewAiClient.KickoffAsync(BuildPayload(gameState, prompt), cancellationToken);

        if (!waitForCompletion)
        {
            return new CrewAiStrategicAdviceResult
            {
                Configured = true,
                KickoffId = kickoff.KickoffId,
                Status = "submitted"
            };
        }

        var attempts = Math.Max(1, maxPollAttempts);
        var intervalSeconds = Math.Clamp(pollIntervalSeconds, 1, 30);
        CrewAiExecutionStatus? latestStatus = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            latestStatus = await _crewAiClient.GetStatusAsync(kickoff.KickoffId, cancellationToken);
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

        return new CrewAiStrategicAdviceResult
        {
            Configured = true,
            KickoffId = kickoff.KickoffId,
            Status = latestStatus.Status,
            Advice = latestStatus.ResultText,
            Error = latestStatus.Error,
            RawResponse = latestStatus.RawJson
        };
    }

    private CrewAiKickoffPayload BuildPayload(GameState gameState, string prompt)
    {
        var scenario = gameState.ActiveScenario;
        var mapping = _options.InputMapping;

        return new CrewAiKickoffPayload
        {
            Inputs = new Dictionary<string, string>
            {
                [mapping.Prompt] = prompt,
                [mapping.GameState] = JsonSerializer.Serialize(gameState, JsonOptions),
                [mapping.Month] = scenario.CurrentMonth.ToString(CultureInfo.InvariantCulture),
                [mapping.Solaris] = scenario.CurrentSolaris.ToString(CultureInfo.InvariantCulture),
                [mapping.EnclavesSummary] = BuildEnclavesSummary(gameState)
            },
            Meta = new Dictionary<string, object?>
            {
                ["saveName"] = gameState.SaveName,
                ["scenario"] = scenario.Name,
                ["timestampUtc"] = DateTime.UtcNow,
                ["source"] = "DuneArrakis.SimulationService"
            }
        };
    }

    private static string BuildEnclavesSummary(GameState gameState)
    {
        var builder = new StringBuilder();
        var scenario = gameState.ActiveScenario;

        builder.AppendLine($"Escenario: {scenario.Name}");
        builder.AppendLine($"Mes actual: {scenario.CurrentMonth}");
        builder.AppendLine($"Solaris actuales: {scenario.CurrentSolaris:N0}");

        foreach (var enclave in scenario.Enclaves)
        {
            var aliveCreatures = enclave.Creatures.Where(creature => creature.IsAlive).ToList();
            var averageHealth = aliveCreatures.Count == 0 ? 0 : aliveCreatures.Average(creature => creature.Health);

            builder.AppendLine(
                $"- {enclave.Name} [{enclave.Type}] | criaturas vivas: {aliveCreatures.Count}/{enclave.MaxCreatureCapacity} | " +
                $"salud media: {averageHealth:F1} | instalaciones: {enclave.Facilities.Count} | visitantes mes: {enclave.TotalVisitorsThisMonth:N0}");

            foreach (var creature in aliveCreatures.OrderByDescending(creature => creature.Health).Take(5))
            {
                builder.AppendLine(
                    $"  * {creature.Name} | salud {creature.Health} | edad {creature.AgeInMonths} meses | dieta {creature.Diet} | hábitat {creature.Habitat}");
            }
        }

        return builder.ToString().Trim();
    }
}

public class CrewAiStrategicAdviceResult
{
    public bool Configured { get; set; }
    public string KickoffId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Advice { get; set; }
    public string? Error { get; set; }
    public string? RawResponse { get; set; }
}