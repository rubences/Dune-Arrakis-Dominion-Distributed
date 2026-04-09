using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using DuneArrakis.SimulationService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DuneArrakis.Tests;

public class MonthlyDecisionAutomationServiceTests
{
    [Fact]
    public async Task GenerateAndApplyActionsAsync_BuysSuppliesWithoutGoingBankrupt()
    {
        var gameState = CreateGameState();
        gameState.ActiveScenario.CurrentSolaris = 27m;
        gameState.ActiveScenario.StoredFoodUnits = 0;

        var client = new FakeDecisionCrewAiClient(
                        requiredInputs: new List<string>(),
                        status: CreateSuccessStatus(@"{
    ""comprar_suministros"": 10,
    ""trasladar_criaturas"": [],
    ""registrar_letargo"": []
}"));

        var service = CreateService(client);

        var result = await service.GenerateAndApplyActionsAsync(gameState, true, true, false, 1, 1);

        Assert.True(result.ActionsApplied);
        Assert.Equal(5, result.PurchasedSupplyUnits);
        Assert.Equal(2m, gameState.ActiveScenario.CurrentSolaris);
        Assert.True(gameState.ActiveScenario.StoredFoodUnits >= 0);
        Assert.Contains(gameState.ActiveScenario.EventLog, evt => evt.EventType == "Suministros");
    }

    [Fact]
    public async Task GenerateAndApplyActionsAsync_DistributesFoodToLowestHealthFirst()
    {
        var gameState = CreateGameState();
        var creatures = gameState.ActiveScenario.Enclaves.SelectMany(enclave => enclave.Creatures).ToList();
        creatures[0].Health = 20;
        creatures[0].FoodConsumedThisMonth = 0;
        creatures[0].FoodRequiredPerMonth = 50;
        creatures[1].Health = 90;
        creatures[1].FoodConsumedThisMonth = 0;
        creatures[1].FoodRequiredPerMonth = 50;
        gameState.ActiveScenario.StoredFoodUnits = 50;

        var client = new FakeDecisionCrewAiClient(
                        requiredInputs: new List<string>(),
                        status: CreateSuccessStatus(@"{
    ""comprar_suministros"": 0,
    ""trasladar_criaturas"": [],
    ""registrar_letargo"": []
}"));

        var service = CreateService(client);

        var result = await service.GenerateAndApplyActionsAsync(gameState, true, true, false, 1, 1);

        Assert.Equal(50, result.AllocatedFoodUnits);
        Assert.Equal(50, creatures[0].FoodConsumedThisMonth);
        Assert.Equal(0, creatures[1].FoodConsumedThisMonth);
        Assert.Contains(gameState.ActiveScenario.EventLog, evt => evt.EventType == "Abastecimiento");
    }

    [Fact]
    public async Task GenerateAndApplyActionsAsync_TransfersHealthyAdultCreatureToExhibition()
    {
        var gameState = CreateGameState();
        var acclimation = gameState.ActiveScenario.Enclaves.First(enclave => enclave.Type == EnclaveType.Aclimatacion);
        var exhibition = gameState.ActiveScenario.Enclaves.First(enclave => enclave.Type == EnclaveType.Exhibicion);
        var creature = acclimation.Creatures.Single();

        creature.Health = 88;
        creature.AgeInMonths = 14;

        var client = new FakeDecisionCrewAiClient(
                        requiredInputs: new List<string>(),
                        status: CreateSuccessStatus("{\n" +
                                                                             "  \"comprar_suministros\": 0,\n" +
                                                                             $"  \"trasladar_criaturas\": [\"{creature.Id}\"],\n" +
                                                                             "  \"registrar_letargo\": []\n" +
                                                                             "}"));

        var service = CreateService(client);

        var result = await service.GenerateAndApplyActionsAsync(gameState, true, true, false, 1, 1);

        Assert.Contains(creature, exhibition.Creatures);
        Assert.DoesNotContain(creature, acclimation.Creatures);
        Assert.Contains(creature.Id, result.ExecutedTransfers);
        Assert.Contains(result.GeneratedEvents, evt => evt.EventType == "TrasladoAutomatico");
    }

    private static MonthlyDecisionAutomationService CreateService(FakeDecisionCrewAiClient client)
    {
        var options = Options.Create(new DecisionCrewAiOptions
        {
            BaseUrl = "https://example.crewai.com",
            BearerToken = "token",
            DefaultGameName = "Dune: Arrakis Dominion"
        });

        return new MonthlyDecisionAutomationService(
            client,
            new CrewAiWebhookStore(),
            options,
            new FakeSimulationEngine(),
            NullLogger<MonthlyDecisionAutomationService>.Instance);
    }

    private static GameState CreateGameState()
    {
        var scenario = Scenario.CreateArrakeen();
        var acclimation = Enclave.CreateAclimatacion("Aclimatacion");
        var exhibition = Enclave.CreateExhibicion("Exhibicion");

        var creatureOne = Creature.Create(CreatureType.TigreLaza);
        creatureOne.EnclaveId = acclimation.Id;
        acclimation.Creatures.Add(creatureOne);

        var creatureTwo = Creature.Create(CreatureType.MuadDib);
        creatureTwo.EnclaveId = exhibition.Id;
        exhibition.Creatures.Add(creatureTwo);

        scenario.Enclaves.Add(acclimation);
        scenario.Enclaves.Add(exhibition);
        return GameState.NewGame(scenario, "MonthlyAutomationTest");
    }

    private static CrewAiExecutionStatus CreateSuccessStatus(string json) => new()
    {
        KickoffId = Guid.NewGuid().ToString(),
        Status = "SUCCESS",
        ResultText = json,
        RawJson = json
    };

    private sealed class FakeDecisionCrewAiClient : IDecisionCrewAiClient
    {
        private readonly IReadOnlyList<string> _requiredInputs;
        private readonly CrewAiExecutionStatus _status;

        public FakeDecisionCrewAiClient(IReadOnlyList<string> requiredInputs, CrewAiExecutionStatus status)
        {
            _requiredInputs = requiredInputs;
            _status = status;
        }

        public bool IsConfigured => true;

        public Task<IReadOnlyList<string>> GetRequiredInputsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_requiredInputs);

        public Task<CrewAiKickoffResult> KickoffAsync(CrewAiKickoffPayload payload, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CrewAiKickoffResult { KickoffId = _status.KickoffId });

        public Task<CrewAiExecutionStatus> GetStatusAsync(string kickoffId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_status);
    }

    private sealed class FakeSimulationEngine : ISimulationEngine
    {
        public SimulationResult ProcessMonth(GameState gameState) => new()
        {
            Month = gameState.ActiveScenario.CurrentMonth,
            CurrentSolaris = gameState.ActiveScenario.CurrentSolaris,
            Events = new List<SimulationEvent>()
        };
    }
}