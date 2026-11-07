using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// Default <see cref="ITelemetryRecorder"/>. Applies sampling, stamps the application, version and
/// environment, optionally adds a salted user hash, and enqueues the <see cref="UsageEvent"/>. It never
/// blocks and never throws.
/// </summary>
public sealed class TelemetryRecorder : ITelemetryRecorder
{
    private readonly TelemetryChannel _channel;
    private readonly IOptionsMonitor<TelemetryOptions>? _options;
    private readonly IUsageUserKeyProvider? _userKeyProvider;
    private readonly ILogger<TelemetryRecorder>? _logger;

    /// <summary>Creates the recorder. With no options the defaults are used.</summary>
    /// <param name="channel">The channel events are enqueued to.</param>
    /// <param name="options">Supplies the current options.</param>
    /// <param name="userKeyProvider">Optional; only used when <see cref="TelemetryOptions.IncludeUserKey"/> is true.</param>
    /// <param name="logger">Optional logger for failures observed while recording.</param>
    public TelemetryRecorder(
        TelemetryChannel channel,
        IOptionsMonitor<TelemetryOptions>? options = null,
        IUsageUserKeyProvider? userKeyProvider = null,
        ILogger<TelemetryRecorder>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        _channel = channel;
        _options = options;
        _userKeyProvider = userKeyProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool Record(UsageRecord record)
    {
        var usageEvent = Build(record);

        return usageEvent is not null && _channel.TryWrite(usageEvent);
    }

    /// <inheritdoc />
    public ValueTask<bool> RecordAsync(UsageRecord record, CancellationToken cancellationToken = default)
    {
        var usageEvent = Build(record);

        return usageEvent is null ? ValueTask.FromResult(false) : _channel.WriteAsync(usageEvent, cancellationToken);
    }

    private UsageEvent? Build(UsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var options = _options?.CurrentValue ?? new TelemetryOptions();

        if (!TrySample(record, options.SampleRate, out var weight))
        {
            return null;
        }

        return new UsageEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Application = options.ApplicationName,
            Version = options.ApplicationVersion,
            Environment = options.EnvironmentName,
            Name = record.Name,
            Category = record.Category,
            Target = record.Target,
            Duration = record.Duration,
            Outcome = record.Outcome,
            Properties = record.Properties,
            UserKey = ComputeUserKey(options),
            SampleWeight = weight,
        };
    }

    private static bool TrySample(UsageRecord record, double rate, out int weight)
    {
        weight = 1;

        // Problems are rare and the interesting ones; only successful traffic is thinned out.
        if (rate >= 1.0 || record.Outcome != UsageOutcomes.Success)
        {
            return true;
        }

        if (rate <= 0.0 || Random.Shared.NextDouble() >= rate)
        {
            return false;
        }

        weight = (int)Math.Round(1.0 / rate);
        return true;
    }

    private string? ComputeUserKey(TelemetryOptions options)
    {
        if (!options.IncludeUserKey || _userKeyProvider is null || string.IsNullOrWhiteSpace(options.UserKeySalt))
        {
            return null;
        }

        try
        {
            var identifier = _userKeyProvider.GetUserIdentifier();

            return string.IsNullOrWhiteSpace(identifier)
                ? null
                : Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.UserKeySalt), Encoding.UTF8.GetBytes(identifier)));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "The usage user key provider failed; recording the event without a user key.");
            return null;
        }
    }
}
