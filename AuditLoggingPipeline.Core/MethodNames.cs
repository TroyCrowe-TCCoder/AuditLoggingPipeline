namespace AuditLoggingPipeline;

/// <summary>Helpers for the naming convention of the methods the plugins observe.</summary>
public static class MethodNames
{
    private const string AsyncSuffix = "Async";

    /// <summary>Removes a trailing <c>Async</c> from <paramref name="methodName"/>, for example <c>RoleUpsertAsync</c> becomes <c>RoleUpsert</c>.</summary>
    public static string TrimAsync(string methodName)
    {
        ArgumentNullException.ThrowIfNull(methodName);

        return methodName.Length > AsyncSuffix.Length && methodName.EndsWith(AsyncSuffix, StringComparison.Ordinal)
            ? methodName[..^AsyncSuffix.Length]
            : methodName;
    }
}
