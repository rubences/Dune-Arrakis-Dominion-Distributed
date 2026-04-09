using DuneArrakis.SimulationService.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DuneArrakis.SimulationService.Agents;

public class StrategicAdvisorAgent : INotificationHandler<SimulationMonthEndedEvent>
{
    private readonly Services.ICrewAiAdvisor _advisor;
    private readonly ILogger<StrategicAdvisorAgent> _logger;

    public StrategicAdvisorAgent(Services.ICrewAiAdvisor advisor, ILogger<StrategicAdvisorAgent> logger)
    {
        _advisor = advisor;
        _logger = logger;
    }

    public async Task Handle(SimulationMonthEndedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("StrategicAdvisorAgent: Analizando el final del mes {Month}...", notification.GameState.ActiveScenario.CurrentMonth);

        try
        {
            var prompt = "Revisa el estado del escenario en el mes actual y proporciona recomendaciones estratégicas a largo plazo.";
            var result = await _advisor.GetStrategicAdviceAsync(
                notification.GameState, 
                prompt, 
                waitForCompletion: true, 
                maxPollAttempts: 10, 
                pollIntervalSeconds: 3, 
                cancellationToken);

            _logger.LogInformation("StrategicAdvisorAgent Advice Finalizado: {Status}", result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "El StrategicAdvisorAgent falló al intentar generar el análisis del mes.");
        }
    }
}
