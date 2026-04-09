using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using DuneArrakis.SimulationService.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuneArrakis.Tests;

public class SimulationEngineTests
{
    private static SimulationEngine CreateEngine() =>
        new(NullLogger<SimulationEngine>.Instance, new DummyPublisher());

    private class DummyPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private static GameState CreateTestGameState()
    {
        var scenario = Scenario.CreateArrakeen();
        var enclave = Enclave.CreateExhibicion("Test Exhibicion");
        scenario.Enclaves.Add(enclave);
        return GameState.NewGame(scenario, "TestGame");
    }

    [Fact]
    public async Task ProcessMonth_AdvancesMonthCounter()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var initialMonth = gameState.ActiveScenario.CurrentMonth;

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(initialMonth + 1, gameState.ActiveScenario.CurrentMonth);
    }

    [Fact]
    public async Task ProcessMonth_WithOptimalFeedingCreature_HealthIncreases()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First();

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 80;
        // Feed fully: consume all required food
        creature.FoodConsumedThisMonth = creature.FoodRequiredPerMonth;
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(85, creature.Health); // +5 for optimal feeding
    }

    [Fact]
    public async Task ProcessMonth_WithNoFeeding_HealthDecreasesByThirty()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First();

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 80;
        creature.FoodConsumedThisMonth = 0; // no food at all
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(50, creature.Health); // -30 for starvation
    }

    [Fact]
    public async Task ProcessMonth_WithPartialFeeding25To75_HealthDecreasesByTwenty()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First();

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 80;
        // Feed 50% (between 25% and 75%)
        creature.FoodConsumedThisMonth = creature.FoodRequiredPerMonth / 2;
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(60, creature.Health); // -20
    }

    [Fact]
    public async Task ProcessMonth_WithPartialFeeding75To100_HealthDecreasesByTen()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First();

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 80;
        // Feed 90% (between 75% and 100%)
        creature.FoodConsumedThisMonth = (int)(creature.FoodRequiredPerMonth * 0.9);
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(70, creature.Health); // -10
    }

    [Fact]
    public async Task ProcessMonth_WhenHealthReachesZero_CreatureDies()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First();

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 25; // will lose 30, dropping to 0
        creature.FoodConsumedThisMonth = 0;
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.False(creature.IsAlive);
        Assert.Equal(0, creature.Health);
    }

    [Fact]
    public async Task ProcessMonth_ExhibicionEnclave_GeneratesVisitorRevenue()
    {
        var engine = CreateEngine();
        var gameState = CreateTestGameState();
        var scenario = gameState.ActiveScenario;
        var enclave = scenario.Enclaves.First(e => e.Type == EnclaveType.Exhibicion);
        var initialSolaris = scenario.CurrentSolaris;

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.Health = 100;
        creature.FoodConsumedThisMonth = creature.FoodRequiredPerMonth; // optimal feeding
        creature.EnclaveId = enclave.Id;
        enclave.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        // Visitors should generate revenue
        Assert.True(enclave.TotalVisitorsThisMonth > 0);
    }

    [Fact]
    public async Task ProcessMonth_AclimatacionEnclave_NoVisitors()
    {
        var engine = CreateEngine();
        var scenario = Scenario.CreateArrakeen();
        var aclimatacion = Enclave.CreateAclimatacion("Test Aclimatacion");
        scenario.Enclaves.Add(aclimatacion);
        var gameState = GameState.NewGame(scenario, "TestAclimatacion");

        var creature = Creature.Create(CreatureType.MuadDib);
        creature.EnclaveId = aclimatacion.Id;
        aclimatacion.Creatures.Add(creature);

        await engine.ProcessMonthAsync(gameState);

        Assert.Equal(0, aclimatacion.TotalVisitorsThisMonth);
    }

    [Fact]
    public async Task ProcessMonth_AppliesMaintenanceCosts()
    {
        var engine = CreateEngine();
        var scenario = Scenario.CreateArrakeen();
        var enclave = Enclave.CreateExhibicion("Test");
        var facility = Facility.Create(FacilityType.ZonaDeHabitat);
        enclave.Facilities.Add(facility);
        scenario.Enclaves.Add(enclave);
        var gameState = GameState.NewGame(scenario, "TestMaintenance");
        var initialSolaris = scenario.CurrentSolaris;

        await engine.ProcessMonthAsync(gameState);

        // Maintenance cost should be deducted
        Assert.True(scenario.CurrentSolaris < initialSolaris);
    }

    [Fact]
    public void CreatureTemplates_AllTypesHaveValidData()
    {
        foreach (var type in Enum.GetValues<CreatureType>())
        {
            Assert.True(Creature.Templates.ContainsKey(type), $"Missing template for {type}");
            var template = Creature.Templates[type];
            Assert.NotEmpty(template.Name);
            Assert.True(template.AcquisitionCost > 0);
            Assert.True(template.FoodRequiredPerMonth > 0);
        }
    }

    [Fact]
    public void Scenario_CreateArrakeen_HasCorrectInitialSolaris()
    {
        var scenario = Scenario.CreateArrakeen();
        Assert.Equal(50_000m, scenario.CurrentSolaris);
        Assert.Equal(ScenarioType.Arrakeen, scenario.Type);
    }

    [Fact]
    public void Scenario_CreateGiediPrime_HasCorrectInitialSolaris()
    {
        var scenario = Scenario.CreateGiediPrime();
        Assert.Equal(75_000m, scenario.CurrentSolaris);
        Assert.Equal(ScenarioType.GiediPrime, scenario.Type);
    }

    [Fact]
    public void Scenario_CreateCaladan_HasCorrectInitialSolaris()
    {
        var scenario = Scenario.CreateCaladan();
        Assert.Equal(40_000m, scenario.CurrentSolaris);
        Assert.Equal(ScenarioType.Caladan, scenario.Type);
    }

    [Fact]
    public void Enclave_CreateAclimatacion_HasCorrectType()
    {
        var enclave = Enclave.CreateAclimatacion("Test");
        Assert.Equal(EnclaveType.Aclimatacion, enclave.Type);
        Assert.True(enclave.Hectareas > 0);
        Assert.True(enclave.MaxCreatureCapacity > 0);
    }

    [Fact]
    public void Enclave_CreateExhibicion_HasHigherNivelAdquisitivo()
    {
        var aclimatacion = Enclave.CreateAclimatacion("Test");
        var exhibicion = Enclave.CreateExhibicion("Test");
        Assert.True(exhibicion.NivelAdquisitivo > aclimatacion.NivelAdquisitivo);
    }

    [Fact]
    public void FacilityCatalog_ContainsAllFacilityTypes()
    {
        foreach (var type in Enum.GetValues<FacilityType>())
        {
            Assert.True(Facility.Catalog.ContainsKey(type), $"Missing catalog entry for {type}");
            var (name, cost, maintenance) = Facility.Catalog[type];
            Assert.NotEmpty(name);
            Assert.True(cost > 0);
            Assert.True(maintenance > 0);
        }
    [Fact]
    public void PurchaseCreature_WithInsufficientFunds_ThrowsDomainException()
    {
        var scenario = Scenario.CreateArrakeen();
        var enclave = Enclave.CreateExhibicion("Test");
        scenario.Enclaves.Add(enclave);
        // Vaciar fondos
        scenario.CurrentSolaris = 0; 

        Assert.Throws<DuneArrakis.Domain.Exceptions.InsufficientFundsException>(() =>
        {
            scenario.PurchaseCreature(enclave.Id, CreatureType.GusanoDeArenaJuvenil);
        });
    }
}
