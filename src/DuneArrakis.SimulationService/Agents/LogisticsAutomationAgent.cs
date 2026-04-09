using DuneArrakis.SimulationService.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DuneArrakis.SimulationService.Agents;

public class LogisticsAutomationAgent : INotificationHandler<SimulationMonthEndedEvent>
{
    private readonly Services.IMonthlyDecisionAutomationService _logisticsAutomation;
    private readonly ILogger<LogisticsAutomationAgent> _logger;

    public LogisticsAutomationAgent(
        Services.IMonthlyDecisionAutomationService logisticsAutomation, 
        ILogger<LogisticsAutomationAgent> logger)
    {
        _logisticsAutomation = logisticsAutomation;
        _logger = logger;
    }

    public async Task Handle(SimulationMonthEndedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogisticsAutomationAgent: Evaluando acciones logísticas automáticas para el mes {Month}...", notification.GameState.ActiveScenario.CurrentMonth);

        try
        {
            var automatedResult = await _logisticsAutomation.GenerateAndApplyActionsAsync(
                notification.GameState,
                waitForCompletion: true,
                executeActions: true,
                processMonthAfterActions: false,
                maxPollAttempts: 10,
                pollIntervalSeconds: 3,
                cancellationToken);

            _logger.LogInformation("LogisticsAutomationAgent Ejecutado: Aplicadas {Purchased} compras de suministro y {Transfers} traslados.", 
                automatedResult.PurchasedSupplyUnits, 
                automatedResult.ExecutedTransfers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "El LogisticsAutomationAgent falló al intentar aplicar la logística del mes.");
        }
    }
}
