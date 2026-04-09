namespace DuneArrakis.SimulationService.Services;

public class CrewAiOptions
{
    public const string SectionName = "CrewAi";

    public string BaseUrl { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public CrewAiInputMappingOptions InputMapping { get; set; } = new();

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(BearerToken);
}

public class CrewAiInputMappingOptions
{
    public string Prompt { get; set; } = "prompt";
    public string GameState { get; set; } = "game_state";
    public string Month { get; set; } = "current_month";
    public string Solaris { get; set; } = "current_solaris";
    public string EnclavesSummary { get; set; } = "enclaves_summary";
}