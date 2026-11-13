namespace AuditLoggingPipeline.Documentation.Tests;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Keeps each package's CONFIGURATION.md in step with its options classes. A library may carry defaults only
/// when they can be found without reading code, so a listed default that differs from the code fails the build.
/// </summary>
public sealed class ConfigurationReferenceTests
{
    private const string ReferenceFileName = "CONFIGURATION.md";
    private const string NoValue = "none";

    private static readonly Assembly[] PackageAssemblies =
    [
        typeof(AuditPipelineOptions).Assembly,
        typeof(EntryPipelineOptions).Assembly,
        typeof(StateTrailOptions).Assembly,
        typeof(SqlAuditSinkOptions).Assembly,
        typeof(TelemetryOptions).Assembly,
        typeof(SqlTelemetrySinkOptions).Assembly,
    ];

    private static readonly Regex SecretName = new("ConnectionString|Salt|Secret|Password|Token|ApiKey|AccessKey", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SecretValue = new(@"(Password|Pwd|AccountKey|SharedAccessKey|Server|Data Source)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredValues = [ReferenceConstants.Yes, ReferenceConstants.No, ReferenceConstants.Conditional];

    private static readonly string[] SecretValues = [ReferenceConstants.Yes, ReferenceConstants.No];

    private sealed record OptionRow(string Default, string Required, string Secret);

    public static TheoryData<string> OptionsTypeNames()
    {
        var typeNames = new TheoryData<string>();
        foreach (var type in FindOptionsTypes())
        {
            typeNames.Add(type.Name);
        }

        return typeNames;
    }

    [Fact]
    public void FindOptionsTypes_AllPackages_FindsOptionsInEachOfSix()
    {
        var packages = FindOptionsTypes().Select(type => type.Assembly.GetName().Name).Distinct().ToList();

        Assert.Equal(6, packages.Count);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void Reference_OptionsDifferFromCode_ReportsEveryMismatch(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);

        var inCode = ReadProperties(type).Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        problems.AddRange(inCode.Except(documented.Keys).Select(name => $"{typeName}.{name} is in the code but not in {ReferenceFileName}."));
        problems.AddRange(documented.Keys.Except(inCode).Select(name => $"{typeName}.{name} is in {ReferenceFileName} but not in the code."));

        Assert.Empty(problems);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void Reference_DefaultDiffersFromCode_ReportsEveryMismatch(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);
        var instance = CreateInstance(type);

        var problems = new List<string>();
        foreach (var property in ReadProperties(type))
        {
            if (!documented.TryGetValue(property.Name, out var row))
            {
                continue;
            }

            var actual = FormatDefault(property.GetValue(instance));
            if (!string.Equals(row.Default, actual, StringComparison.Ordinal))
            {
                problems.Add($"{typeName}.{property.Name}: {ReferenceFileName} says '{row.Default}' but the code default is '{actual}'.");
            }
        }

        Assert.Empty(problems);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void Reference_RequiredOrSecretMarkingInvalid_ReportsEveryProblem(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);

        var problems = new List<string>();
        foreach (var (name, row) in documented)
        {
            if (!RequiredValues.Contains(row.Required))
            {
                problems.Add($"{typeName}.{name}: Required must be Yes, No or Conditional.");
            }
            else if (row.Required != ReferenceConstants.No && row.Default != NoValue)
            {
                problems.Add($"{typeName}.{name}: a required option cannot have a default.");
            }

            if (!SecretValues.Contains(row.Secret))
            {
                problems.Add($"{typeName}.{name}: Secret must be Yes or No.");
            }
            else if (SecretName.IsMatch(name) && row.Secret != ReferenceConstants.Yes)
            {
                problems.Add($"{typeName}.{name}: the name suggests a secret, so Secret must be Yes.");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void PackageProject_ReferenceNotPacked_ReportsPackage()
    {
        var problems = new List<string>();
        foreach (var package in FindOptionsTypes().Select(type => type.Assembly.GetName().Name!).Distinct())
        {
            var project = File.ReadAllText(Path.Combine(RepositoryRoot(), package, package + ".csproj"));
            if (!project.Contains(ReferenceFileName, StringComparison.Ordinal))
            {
                problems.Add($"{package}.csproj does not pack {ReferenceFileName}.");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Reference_ContainsCredentialText_ReportsPackage()
    {
        var problems = new List<string>();
        foreach (var package in FindOptionsTypes().Select(type => type.Assembly.GetName().Name!).Distinct())
        {
            var text = File.ReadAllText(ReferencePath(package));
            var match = SecretValue.Match(text);
            if (match.Success)
            {
                problems.Add($"{package}/{ReferenceFileName} contains '{match.Value}', which looks like a connection string or credential.");
            }
        }

        Assert.Empty(problems);
    }

    private static List<Type> FindOptionsTypes() => PackageAssemblies
        .SelectMany(assembly => assembly.GetExportedTypes())
        .Where(type => type.IsClass && type.Name.EndsWith("Options", StringComparison.Ordinal))
        .Distinct()
        .ToList();

    private static Type FindOptionsType(string typeName) => FindOptionsTypes().Single(type => type.Name == typeName);

    private static List<PropertyInfo> ReadProperties(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(property => property.GetMethod is not null)
        .ToList();

    private static object CreateInstance(Type type)
    {
        var concrete = type.IsAbstract
            ? PackageAssemblies.SelectMany(assembly => assembly.GetExportedTypes()).First(candidate => !candidate.IsAbstract && type.IsAssignableFrom(candidate))
            : type;

        return Activator.CreateInstance(concrete)!;
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => NoValue,
        string text => text.Length == 0 ? NoValue : text,
        bool flag => flag ? "true" : "false",
        TimeSpan span => FormatSpan(span),
        Delegate => throw new NotSupportedException("A default that is a function has no documented format."),
        IDictionary dictionary => dictionary.Count == 0 ? NoValue : throw new NotSupportedException("A non-empty default dictionary has no documented format."),
        IEnumerable<string> items => items.Any() ? string.Join(", ", items) : NoValue,
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? NoValue,
    };

    private static string FormatSpan(TimeSpan span)
    {
        if (span == TimeSpan.Zero)
        {
            return "0 s";
        }

        return span < TimeSpan.FromSeconds(1)
            ? span.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms"
            : span.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s";
    }

    private static Dictionary<string, OptionRow> ReadSection(Type type)
    {
        var path = ReferencePath(type.Assembly.GetName().Name!);
        Assert.True(File.Exists(path), $"{path} does not exist.");

        var heading = $"## `{type.Name}`";
        var rows = new Dictionary<string, OptionRow>(StringComparer.Ordinal);
        var inSection = false;
        var found = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line.Trim(), heading, StringComparison.Ordinal);
                found |= inSection;
                continue;
            }

            if (!inSection || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells[0] == "Option" || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(cells.Length == 5, $"{ReferenceFileName} row '{line}' must have 5 columns: Option, Default, Required, Secret, Meaning.");
            rows[cells[0].Trim('`')] = new OptionRow(cells[1].Trim('`'), cells[2], cells[3]);
        }

        Assert.True(found, $"{path} has no section {heading}.");
        return rows;
    }

    private static string ReferencePath(string package) => Path.Combine(RepositoryRoot(), package, ReferenceFileName);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AuditLoggingPipeline.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("AuditLoggingPipeline.slnx was not found above the test output folder.");
    }
}
