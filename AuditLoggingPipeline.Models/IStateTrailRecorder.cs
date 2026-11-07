namespace AuditLoggingPipeline;

/// <summary>
/// Explicit way to record a record-level state trail from application code, where the caller has the
/// real before and after records. Context is captured synchronously at the moment of the call, as
/// with <see cref="IAuditRecorder"/>.
/// </summary>
public interface IStateTrailRecorder
{
    /// <summary>Records a state trail without waiting. Returns false if the entry was not accepted (counted as dropped).</summary>
    bool Record(StateTrailRecord record);

    /// <summary>Records a state trail, waiting for buffer space when the pipeline is configured to wait.</summary>
    ValueTask<bool> RecordAsync(StateTrailRecord record, CancellationToken cancellationToken = default);
}
