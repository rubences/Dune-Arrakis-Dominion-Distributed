using System.Collections.Concurrent;
using System.Text.Json;

namespace DuneArrakis.SimulationService.Services;

public interface ICrewAiWebhookStore
{
    void Store(string source, JsonElement payload);
    bool TryGet(string kickoffId, out CrewAiExecutionStatus status);
    Task<CrewAiExecutionStatus?> WaitForStatusAsync(string kickoffId, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public class CrewAiWebhookStore : ICrewAiWebhookStore
{
    private readonly ConcurrentDictionary<string, CrewAiExecutionStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CrewAiExecutionStatus>> _pending = new();

    public void Store(string source, JsonElement payload)
    {
        var kickoffId = FindFirstAvailableString(payload, "kickoff_id", "kickoffId", "id", "execution_id");
        if (string.IsNullOrWhiteSpace(kickoffId))
            return;

        var status = new CrewAiExecutionStatus
        {
            KickoffId = kickoffId,
            Status = FindFirstAvailableString(payload, "status", "state") ?? "unknown",
            ResultText = FindFirstAvailableString(payload, "result", "output", "raw", "final_output", "response"),
            Error = FindFirstAvailableString(payload, "error", "message", "detail"),
            RawJson = payload.GetRawText(),
            Source = source
        };

        _statuses[kickoffId] = status;

        var tcs = _pending.GetOrAdd(kickoffId, _ => new TaskCompletionSource<CrewAiExecutionStatus>(TaskCreationOptions.RunContinuationsAsynchronously));
        tcs.TrySetResult(status);
    }

    public bool TryGet(string kickoffId, out CrewAiExecutionStatus status) => _statuses.TryGetValue(kickoffId, out status!);

    public async Task<CrewAiExecutionStatus?> WaitForStatusAsync(string kickoffId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_statuses.TryGetValue(kickoffId, out var existing))
            return existing;

        var tcs = _pending.GetOrAdd(kickoffId, _ => new TaskCompletionSource<CrewAiExecutionStatus>(TaskCreationOptions.RunContinuationsAsynchronously));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static string? FindFirstAvailableString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = FindString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.NameEquals(propertyName) &&
                !string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = FindString(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }

                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Object => property.Value.ToString(),
                JsonValueKind.Array => property.Value.ToString(),
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        return null;
    }
}