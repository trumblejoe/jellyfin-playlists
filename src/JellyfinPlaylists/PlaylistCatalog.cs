using System.Collections.ObjectModel;

namespace JellyfinPlaylists;

public static class PlaylistCatalog
{
    public static IReadOnlyDictionary<string, PlaylistTarget> TargetsByName =>
        new ReadOnlyDictionary<string, PlaylistTarget>(
            PlaylistTarget.DefaultTargets()
                .ToDictionary(target => target.Name, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyCollection<PlaylistTarget> HighPriorityTargets =>
        PlaylistTarget.DefaultTargets()
            .Where(target => target.Priority >= PlaylistPriority.High)
            .ToArray();
}

public static class LibraryItemFilters
{
    public static bool IsVideoOnly(LibraryItemRecord item)
        => item.ItemType is LibraryItemType.Movie or LibraryItemType.Series or LibraryItemType.Episode;

    public static bool IsMusic(LibraryItemRecord item)
        => item.ItemType is LibraryItemType.Music;

    public static IEnumerable<LibraryItemRecord> FilterVideoLibrary(IEnumerable<LibraryItemRecord> items)
        => items.Where(IsVideoOnly);
}

public static class PlaylistMatchHelper
{
    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Trim()
            .Replace("-", " ")
            .Replace("_", " ")
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray())
            .Replace("  ", " ")
            .Trim();
    }

    public static bool MatchesKeywords(string candidate, IEnumerable<string> keywords)
    {
        var normalizedCandidate = NormalizeName(candidate);
        return keywords
            .Select(NormalizeName)
            .Any(keyword => keyword.Length > 0 && normalizedCandidate.Contains(keyword, StringComparison.Ordinal));
    }
}
