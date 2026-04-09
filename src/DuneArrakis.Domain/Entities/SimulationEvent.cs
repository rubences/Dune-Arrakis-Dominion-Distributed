namespace DuneArrakis.Domain.Entities;

public class SimulationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Month { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? SolarisChange { get; set; }
    public Guid? CreatureId { get; set; }
    public Guid? EnclaveId { get; set; }
}
