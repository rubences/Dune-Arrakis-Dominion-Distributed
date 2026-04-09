namespace DuneArrakis.Domain.Entities;

public class GameState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SaveName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;
    public Scenario ActiveScenario { get; set; } = null!;

    public static GameState NewGame(Scenario scenario, string saveName = "Nueva Partida") => new()
    {
        SaveName = saveName,
        ActiveScenario = scenario,
        CreatedAt = DateTime.UtcNow,
        LastSavedAt = DateTime.UtcNow
    };
}
