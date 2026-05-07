using System.Security.Cryptography;
using System.Text;

namespace DuneArrakis.SimulationService.Services;

public class SecurityOptions
{
    public const string SectionName = "Security";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSigningSecret { get; set; } = string.Empty;
}

public interface IWebhookSignatureVerifier
{
    bool IsValid(string payload, string? signatureHeader);
}

public class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly SecurityOptions _options;
    public WebhookSignatureVerifier(Microsoft.Extensions.Options.IOptions<SecurityOptions> options) => _options = options.Value;

    public bool IsValid(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret) || string.IsNullOrWhiteSpace(signatureHeader)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        var provided = signatureHeader.Trim().ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }
}
