using System.Reflection;

namespace AuditLoggingPipeline;

/// <summary>Decides whether a method call is recorded, according to <see cref="InterceptionOptions"/>.</summary>
public static class InterceptionMatcher
{
    /// <summary>Returns true when calls to <paramref name="method"/> should be recorded.</summary>
    /// <param name="options">The prefix, suffix and override settings.</param>
    /// <param name="method">The method being called.</param>
    public static bool IsMatch(InterceptionOptions options, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(method);

        if (options.MethodMatcher is not null)
        {
            return options.MethodMatcher(method);
        }

        var name = MethodNames.TrimAsync(method.Name);

        return options.MethodPrefixes.Any(prefix => HasPrefix(name, prefix))
            || options.MethodSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static bool HasPrefix(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal)
        && (name.Length == prefix.Length || !char.IsLower(name[prefix.Length]));
}
