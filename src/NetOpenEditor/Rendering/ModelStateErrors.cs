using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NetOpenEditor.Rendering;

public static class ModelStateErrors
{
    /// <summary>Returns entries whose key starts with "{prefix}[" (e.g. "Lines[2].DebitAmount", "Lines[2]") with their messages.</summary>
    public static IReadOnlyDictionary<string, string[]> Collect(ModelStateDictionary modelState, string prefix)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var marker = prefix + "[";
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, entry) in modelState)
        {
            if (entry is null || entry.Errors.Count == 0 || !key.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[key] = entry.Errors
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? (e.Exception?.Message ?? "Invalid value.") : e.ErrorMessage)
                .ToArray();
        }

        return result;
    }
}
