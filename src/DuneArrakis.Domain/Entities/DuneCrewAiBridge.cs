using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dune.Models;

namespace Dune.AI.Bridge;

/// <summary>
/// Cliente puente que envía snapshots a CrewAI y consulta decisiones del Mentat de forma asíncrona.
/// </summary>
public sealed class CrewAiBridgeClient
{
    private readonly HttpClient _httpClient;

    public CrewAiBridgeClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Envía estado serializado del juego al Cerebro CrewAI para su análisis.
    /// </summary>
    public async Task<string> SendSnapshotAsync(ArrakisStateDto snapshot, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/mentat/snapshot", snapshot, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Consulta la decisión de CrewAI usando el id de decisión previamente generado.
    /// </summary>
    public async Task<CrewAiDecisionEnvelope?> GetDecisionAsync(string decisionId, CancellationToken ct = default)
        => await _httpClient.GetFromJsonAsync<CrewAiDecisionEnvelope>($"/api/mentat/decision/{decisionId}", ct);
}

namespace Dune.Distributed.Core;

/// <summary>
/// Estados de ejecución del bucle autoritario respecto al Mentat.
/// </summary>
public enum MentatDecisionState
{
    Idle,
    WaitingMentat,
    ApplyingDecision
}

/// <summary>
/// Bucle de servidor autoritario para Unity que nunca bloquea el hilo principal durante espera de CrewAI.
/// </summary>
public sealed class ServerGameLoop
{
    private readonly Random _random = new();
    private readonly List<Territorio> _territorios;
    private readonly Queue<CrewAiOrder> _actionQueue = new();
    private readonly CrewAiBridgeClient _bridge;

    public ServerGameLoop(CrewAiBridgeClient bridge)
    {
        _bridge = bridge;
        _territorios = [new Caladan(), new GiediPrime(), new SalusaSecundus()];
    }

    public int CurrentCycle { get; private set; }
    public MentatDecisionState DecisionState { get; private set; } = MentatDecisionState.Idle;
    public string? PendingDecisionId { get; private set; }

    /// <summary>
    /// Ejecuta un ciclo de simulación distribuida y dispara consulta asíncrona al Mentat cuando corresponda.
    /// </summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        CurrentCycle++;
        SimularEconomiaYCosecha();
        SimularEventosAleatorios();
        if (CurrentCycle % 5 == 0 && DecisionState == MentatDecisionState.Idle)
        {
            DecisionState = MentatDecisionState.WaitingMentat;
            PendingDecisionId = await _bridge.SendSnapshotAsync(BuildSnapshot(), ct);
        }

        if (DecisionState == MentatDecisionState.WaitingMentat && !string.IsNullOrWhiteSpace(PendingDecisionId))
        {
            var decision = await _bridge.GetDecisionAsync(PendingDecisionId, ct);
            if (decision is not null)
            {
                foreach (var order in decision.Orders) _actionQueue.Enqueue(order);
                DecisionState = MentatDecisionState.ApplyingDecision;
            }
        }

        if (DecisionState == MentatDecisionState.ApplyingDecision)
        {
            AplicarOrdenesValidadas();
            DecisionState = MentatDecisionState.Idle;
            PendingDecisionId = null;
        }
    }

    /// <summary>
    /// Receptor de acciones: valida recursos y ejecuta órdenes propuestas por CrewAI.
    /// </summary>
    public void AplicarOrdenesValidadas()
    {
        while (_actionQueue.Count > 0)
        {
            var order = _actionQueue.Dequeue();
            if (order.Action.Equals("transfer", StringComparison.OrdinalIgnoreCase))
            {
                EjecutarTraslado(order);
            }
            else if (order.Action.Equals("build", StringComparison.OrdinalIgnoreCase))
            {
                EjecutarConstruccion(order);
            }
        }
    }

    private void EjecutarTraslado(CrewAiOrder order)
    {
        var from = _territorios.FirstOrDefault(t => t.Id == order.FromTerritoryId);
        var to = _territorios.FirstOrDefault(t => t.Id == order.ToTerritoryId);
        if (from is null || to is null) return;

        var movers = from.Subditos.Where(s => s.Rol == order.UnitRole).Take(order.Amount).ToList();
        foreach (var unit in movers)
        {
            if (!unit.IntentarTraslado()) continue;
            _ = from.ExtraerSubdito(unit.Id);
            to.RecibirSubdito(unit);
        }
    }

    private void EjecutarConstruccion(CrewAiOrder order)
    {
        var territory = _territorios.FirstOrDefault(t => t.Id == order.FromTerritoryId);
        if (territory is null) return;

        var alpha = order.Alpha ?? 1m;
        var beta = order.Beta ?? 1m;
        var surface = order.Surface ?? 10m;
        _ = territory.IntentarConstruccion(alpha, beta, surface);
    }

    private void SimularEconomiaYCosecha()
    {
        foreach (var t in _territorios)
        {
            _ = t.PagarSalariosYSupervivencia();
            foreach (var s in t.Subditos) s.AvanzarCiclo();
            _ = t.CosecharMelange(1.15m);
        }
    }

    private void SimularEventosAleatorios()
    {
        foreach (var t in _territorios)
        {
            var vibracion = t.Subditos.OfType<Recolector>().Count() * 0.08;
            if (_random.NextDouble() < vibracion) t.AjustarEnergia(-5m);
            if (_random.NextDouble() < 0.15) t.AjustarEnergia(-7m);
        }
    }

    /// <summary>
    /// Fórmula de combate distribuido: media_exp * (100/(100-media_energia)) * alpha.
    /// </summary>
    public decimal CalcularPotenciaAtaque(IReadOnlyCollection<Subdito> fuerza, decimal alpha)
    {
        if (fuerza.Count == 0) return 0m;
        var mediaExp = fuerza.Average(x => x.Experiencia);
        var mediaEnergia = fuerza.Average(x => x.Energia);
        var divisor = Math.Max(1m, 100m - (decimal)mediaEnergia);
        return (decimal)mediaExp * (100m / divisor) * alpha;
    }

    public ArrakisStateDto BuildSnapshot() => new()
    {
        Cycle = CurrentCycle,
        Territories = _territorios.Select(t => new TerritoryDto
        {
            Id = t.Id,
            Name = t.Nombre,
            House = t.Casa,
            Extension = t.Extension,
            Melange = t.Melange,
            Energy = t.Energia,
            BuiltSurface = t.SuperficieConstruida,
            Units = t.Subditos.Select(s => new UnitDto
            {
                Id = s.Id,
                Name = s.Nombre,
                Role = s.Rol,
                Energy = s.Energia,
                Experience = s.Experiencia
            }).ToList()
        }).ToList()
    };
}
