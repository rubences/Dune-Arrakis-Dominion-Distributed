using DuneArrakis.Domain.Entities;
using MediatR;

namespace DuneArrakis.SimulationService.Events;

public class SimulationMonthEndedEvent : INotification
{
    public GameState GameState { get; }
    public DateTime ProcessedAtUtc { get; }

    public SimulationMonthEndedEvent(GameState gameState)
    {
        GameState = gameState;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
