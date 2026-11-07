using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AuditLoggingPipeline;

/// <summary>
/// Turns record states into JSON text. Output is always complete, valid JSON; it is never cut to a
/// length, because a truncated JSON document cannot be read back.
/// </summary>
public static class JsonPayload
{
	/// <summary>
	/// Serializes <paramref name="value"/>. A string that is already valid JSON is returned
	/// unchanged; any other string becomes a JSON string. Returns null for null input or when the
	/// value cannot be serialized.
	/// </summary>
	/// <param name="value">The state to serialize.</param>
	/// <param name="logger">Optional logger that receives serialization failures.</param>
	public static string? Serialize(object? value, ILogger? logger = null)
	{
		if (value is null)
		{
			return null;
		}

		try
		{
			if (value is string text)
			{
				return IsValidJson(text, logger) ? text : JsonSerializer.Serialize(text);
			}

			return JsonSerializer.Serialize(value, value.GetType());
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
		{
			logger?.LogWarning(ex, "Could not serialize a {StateType} to JSON; the entry will omit that state.", value.GetType().Name);
			return null;
		}
	}

	private static bool IsValidJson(string text, ILogger? logger)
	{
		try
		{
			using var document = JsonDocument.Parse(text);
			return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
		}
		catch (JsonException ex)
		{
			logger?.LogDebug(ex, "A string state is not JSON and will be stored as a JSON string.");
			return false;
		}
	}
}
