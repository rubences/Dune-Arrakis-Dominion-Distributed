namespace DuneArrakis.SimulationService.Services;

public class DecisionCrewAiOptions
{
    public const string SectionName = "DecisionCrewAi";

    public string BaseUrl { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string WebhookBaseUrl { get; set; } = string.Empty;
    public string GameNameInput { get; set; } = "game_name";
    public string DefaultGameName { get; set; } = "Dune: Arrakis Dominion";

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(BearerToken);

    public bool HasWebhookBaseUrl => Uri.TryCreate(WebhookBaseUrl, UriKind.Absolute, out _);
}