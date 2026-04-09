using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;

namespace DuneArrakis.SimulationService.Services;

public interface ISimulationEngine
{
    SimulationResult ProcessMonth(GameState gameState);
}

public class SimulationEngine : ISimulationEngine
{
    private readonly ILogger<SimulationEngine> _logger;
    private static readonly Random Rng = new();

    private const int HealthLossHighStarvation = 30;
    private const int HealthLossModerateStarvation = 20;
    private const int HealthLossLowStarvation = 10;
    private const int HealthGainOptimalFeeding = 5;
    private const double ReproductionSuccessRate = 0.20;
    private const int MinHealthForTransfer = 75;

    public SimulationEngine(ILogger<SimulationEngine> logger)
    {
        _logger = logger;
    }

    public SimulationResult ProcessMonth(GameState gameState)
    {
        var scenario = gameState.ActiveScenario;
        var events = new List<SimulationEvent>();
        var month = scenario.CurrentMonth;

        foreach (var enclave in scenario.Enclaves)
        {
            ProcessEnclaveFeeding(enclave, month, events);
            ProcessEnclaveVisitors(enclave, scenario, month, events);
            ProcessEnclaveReproduction(enclave, scenario, month, events);
            ApplyMaintenanceCosts(enclave, scenario, month, events);
        }

        scenario.CurrentMonth++;
        scenario.EventLog.AddRange(events);

        return new SimulationResult
        {
            Month = month,
            Events = events,
            CurrentSolaris = scenario.CurrentSolaris
        };
    }

    private void ProcessEnclaveFeeding(Enclave enclave, int month, List<SimulationEvent> events)
    {
        foreach (var creature in enclave.Creatures.Where(c => c.IsAlive))
        {
            creature.AgeInMonths++;
            if (creature.AgeInMonths % 12 == 0)
                creature.Age++;

            var feedingRatio = creature.FoodRequiredPerMonth > 0
                ? (double)creature.FoodConsumedThisMonth / creature.FoodRequiredPerMonth
                : 1.0;

            int healthChange;
            string healthEvent;

            if (feedingRatio < 0.25)
            {
                healthChange = -HealthLossHighStarvation;
                healthEvent = $"Hambruna severa (ingesta {feedingRatio:P0}): -{HealthLossHighStarvation} salud";
            }
            else if (feedingRatio < 0.75)
            {
                healthChange = -HealthLossModerateStarvation;
                healthEvent = $"Alimentación insuficiente (ingesta {feedingRatio:P0}): -{HealthLossModerateStarvation} salud";
            }
            else if (feedingRatio < 1.0)
            {
                healthChange = -HealthLossLowStarvation;
                healthEvent = $"Alimentación adecuada (ingesta {feedingRatio:P0}): -{HealthLossLowStarvation} salud";
            }
            else
            {
                healthChange = HealthGainOptimalFeeding;
                healthEvent = $"Alimentación óptima: +{HealthGainOptimalFeeding} salud";
            }

            creature.Health = Math.Clamp(creature.Health + healthChange, 0, 100);
            creature.FoodConsumedThisMonth = 0;

            events.Add(new SimulationEvent
            {
                Month = month,
                EventType = "Salud",
                Description = $"[{enclave.Name}] {creature.Name}: {healthEvent}. Salud: {creature.Health}",
                CreatureId = creature.Id,
                EnclaveId = enclave.Id
            });

            if (creature.Health <= 0)
            {
                creature.IsAlive = false;
                events.Add(new SimulationEvent
                {
                    Month = month,
                    EventType = "Muerte",
                    Description = $"[{enclave.Name}] {creature.Name} ha muerto por inanición.",
                    CreatureId = creature.Id,
                    EnclaveId = enclave.Id
                });
                _logger.LogWarning("Criatura {CreatureName} ha muerto en {EnclaveName}", creature.Name, enclave.Name);
            }
        }
    }

    private static void ProcessEnclaveVisitors(Enclave enclave, Scenario scenario, int month, List<SimulationEvent> events)
    {
        if (enclave.Type == EnclaveType.Aclimatacion)
        {
            enclave.CurrentVisitors = 0;
            enclave.TotalVisitorsThisMonth = 0;
            return;
        }

        var aliveCreatures = enclave.Creatures.Where(c => c.IsAlive).ToList();
        if (aliveCreatures.Count == 0)
        {
            enclave.TotalVisitorsThisMonth = 0;
            return;
        }

        var avgHealth = aliveCreatures.Average(c => c.Health);
        var avgAge = aliveCreatures.Average(c => c.AgeInMonths);
        var nivelAdquisitivo = enclave.NivelAdquisitivo;

        var baseVisitors = (int)(aliveCreatures.Count * 150 * (avgHealth / 100.0));
        var ageBonus = avgAge > 24 ? 1.3 : avgAge > 12 ? 1.15 : 1.0;
        var totalVisitors = (int)(baseVisitors * ageBonus);

        enclave.CurrentVisitors = totalVisitors;
        enclave.TotalVisitorsThisMonth = totalVisitors;

        var donationsPerVisitor = nivelAdquisitivo * 5m * (decimal)(avgHealth / 100.0);
        var totalDonations = totalVisitors * donationsPerVisitor;
        scenario.CurrentSolaris += totalDonations;

        events.Add(new SimulationEvent
        {
            Month = month,
            EventType = "Visitantes",
            Description = $"[{enclave.Name}] {totalVisitors} visitantes. Donaciones: {totalDonations:N0} Solaris.",
            SolarisChange = totalDonations,
            EnclaveId = enclave.Id
        });
    }

    private void ProcessEnclaveReproduction(Enclave enclave, Scenario scenario, int month, List<SimulationEvent> events)
    {
        if (enclave.Type != EnclaveType.Aclimatacion) return;

        var hasCloneLab = enclave.Facilities.Any(f =>
            f.Type == Domain.Enums.FacilityType.LaboratorioDeClonacion && f.IsOperational);

        if (!hasCloneLab) return;

        var eligibleCreatures = enclave.Creatures
            .Where(c => c.IsAlive && c.Health >= MinHealthForTransfer && c.AgeInMonths >= 6)
            .ToList();

        foreach (var creature in eligibleCreatures)
        {
            if (enclave.Creatures.Count(c => c.IsAlive) >= enclave.MaxCreatureCapacity) break;

            var roll = Rng.NextDouble();
            if (roll < ReproductionSuccessRate)
            {
                var offspring = Creature.Create(creature.Type);
                offspring.EnclaveId = enclave.Id;
                enclave.Creatures.Add(offspring);

                events.Add(new SimulationEvent
                {
                    Month = month,
                    EventType = "Reproduccion",
                    Description = $"[{enclave.Name}] ¡Reproducción exitosa! Nuevo {offspring.Name} nacido.",
                    CreatureId = offspring.Id,
                    EnclaveId = enclave.Id
                });
                _logger.LogInformation("Nueva criatura {CreatureName} en {EnclaveName}", offspring.Name, enclave.Name);
            }
        }
    }

    private static void ApplyMaintenanceCosts(Enclave enclave, Scenario scenario, int month, List<SimulationEvent> events)
    {
        var totalMaintenance = enclave.Facilities
            .Where(f => f.IsOperational)
            .Sum(f => f.MaintenanceCostPerMonth);

        var foodCosts = enclave.Creatures
            .Where(c => c.IsAlive)
            .Sum(c => c.MonthlyFoodCost);

        var totalCost = totalMaintenance + foodCosts;
        if (totalCost > 0)
        {
            scenario.CurrentSolaris -= totalCost;
            events.Add(new SimulationEvent
            {
                Month = month,
                EventType = "Gastos",
                Description = $"[{enclave.Name}] Gastos mensuales: {totalMaintenance:N0} mantenimiento + {foodCosts:N0} alimentación = {totalCost:N0} Solaris.",
                SolarisChange = -totalCost,
                EnclaveId = enclave.Id
            });
        }
    }
}

public class SimulationResult
{
    public int Month { get; set; }
    public List<SimulationEvent> Events { get; set; } = [];
    public decimal CurrentSolaris { get; set; }
}
