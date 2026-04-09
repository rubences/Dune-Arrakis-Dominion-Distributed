using DuneArrakis.Domain.Enums;

namespace DuneArrakis.Domain.Entities;

public class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScenarioType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal InitialSolaris { get; set; }
    public decimal CurrentSolaris { get; set; }
    public int StoredFoodUnits { get; set; }
    public List<Enclave> Enclaves { get; set; } = [];
    public List<SimulationEvent> EventLog { get; set; } = [];
    public int CurrentMonth { get; set; } = 1;

    public static Scenario CreateArrakeen() => new()
    {
        Type = ScenarioType.Arrakeen,
        Name = "Arrakeen",
        Description = "La capital de Arrakis, corazón del Imperio en el planeta de la especia.",
        InitialSolaris = 50_000m,
        CurrentSolaris = 50_000m,
        StoredFoodUnits = 200
    };

    public static Scenario CreateGiediPrime() => new()
    {
        Type = ScenarioType.GiediPrime,
        Name = "Giedi Prime",
        Description = "Mundo industrial de la Casa Harkonnen, dominado por fábricas y contaminación.",
        InitialSolaris = 75_000m,
        CurrentSolaris = 75_000m,
        StoredFoodUnits = 250
    };

    public static Scenario CreateCaladan() => new()
    {
        Type = ScenarioType.Caladan,
        Name = "Caladan",
        Description = "Mundo oceánico de la Casa Atreides, con vastos océanos y naturaleza exuberante.",
        InitialSolaris = 40_000m,
        CurrentSolaris = 40_000m,
        StoredFoodUnits = 180
    };
}
