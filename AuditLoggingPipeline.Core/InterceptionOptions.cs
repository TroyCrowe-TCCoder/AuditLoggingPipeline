using System.Reflection;

namespace AuditLoggingPipeline;

/// <summary>
/// Options for plugins that observe method calls through a dispatch proxy. Decides which methods
/// are recorded; <see cref="InterceptionMatcher"/> applies them.
/// </summary>
public abstract class InterceptionOptions : EntryPipelineOptions
{
	/// <summary>The verbs recognised by default as a prefix or a suffix of a method name.</summary>
	internal static readonly string[] DefaultVerbs =
		["Save", "Add", "Update", "Upsert", "Insert", "Archive", "Restore", "Delete", "Remove"];

	/// <summary>
	/// Method names that start with one of these words are recorded, for example <c>SaveWidgetAsync</c>.
	/// A prefix must end at a word boundary, so <c>Add</c> matches <c>AddUser</c> but not <c>AddressGet</c>.
	/// </summary>
	public IList<string> MethodPrefixes { get; } = new List<string>(DefaultVerbs);

	/// <summary>
	/// Method names that end with one of these words (ignoring a trailing <c>Async</c>) are recorded,
	/// for example <c>RoleUpsertAsync</c>. Matches the <c>NounVerbAsync</c> naming convention.
	/// </summary>
	public IList<string> MethodSuffixes { get; } = new List<string>(DefaultVerbs);

	/// <summary>
	/// Optional full override. When set, it alone decides whether a method is recorded and the
	/// prefix and suffix lists are ignored.
	/// </summary>
	public Func<MethodInfo, bool>? MethodMatcher { get; set; }
}
