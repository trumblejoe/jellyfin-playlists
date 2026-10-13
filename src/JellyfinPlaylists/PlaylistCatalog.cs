using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

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

public static partial class PlaylistMatchHelper
{
    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = normalized.Replace("_", " ");
        normalized = normalized.Replace("-", " ");
        normalized = normalized.Replace("’", "'");

        normalized = Regex.Replace(normalized, @"\[[^\]]*\]", " ");
        normalized = Regex.Replace(normalized, @"\([^\)]*\)", " ");
        normalized = Regex.Replace(normalized, @"\b(?:s\d{1,2}e\d{1,2}|e\d{1,2}|season\s*\d+|episode\s*\d+|part\s*\d+)\b", " ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{Nd}]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized.Trim().ToLowerInvariant();
    }

    public static bool MatchesKeywords(string candidate, IEnumerable<string> keywords)
    {
        var normalizedCandidate = NormalizeName(candidate);
        return keywords
            .Select(NormalizeName)
            .Any(keyword => keyword.Length > 0 && normalizedCandidate.Contains(keyword, StringComparison.Ordinal));
    }

    public static bool MatchesDefinition(LibraryItemRecord item, PlaylistDefinition? definition)
    {
        if (definition is null)
        {
            return true;
        }

        var normalizedName = NormalizeName(item.Name);
        var matchesKeyword = definition.Keywords
            .Select(NormalizeName)
            .Any(keyword => keyword.Length > 0 && normalizedName.Contains(keyword, StringComparison.Ordinal));

        if (!matchesKeyword)
        {
            return false;
        }

        var excluded = definition.ExcludedKeywords
            .Select(NormalizeName)
            .Any(keyword => keyword.Length > 0 && normalizedName.Contains(keyword, StringComparison.Ordinal));

        return !excluded;
    }

    public static bool IsLikelyMatch(LibraryItemRecord libraryItem, PlaylistTrackingRecord trackingItem)
        => ScoreLibraryItemMatch(libraryItem, trackingItem) >= 70d;

    public static double ScoreLibraryItemMatch(LibraryItemRecord libraryItem, PlaylistTrackingRecord trackingItem)
    {
        if (libraryItem is null || trackingItem is null)
        {
            return 0d;
        }

        var libraryName = NormalizeName(libraryItem.Name);
        var trackingName = NormalizeName(trackingItem.ItemName ?? trackingItem.ExternalId ?? string.Empty);
        if (string.IsNullOrEmpty(libraryName) || string.IsNullOrEmpty(trackingName))
        {
            return 0d;
        }

        var score = 0d;

        if (string.Equals(libraryName, trackingName, StringComparison.OrdinalIgnoreCase))
        {
            score += 70d;
        }
        else if (libraryName.Contains(trackingName, StringComparison.Ordinal) || trackingName.Contains(libraryName, StringComparison.Ordinal))
        {
            score += 45d;
        }
        else
        {
            score += TitleSimilarity(libraryName, trackingName) * 0.7d;
        }

        var libraryYear = TryParseYear(libraryItem.ReleaseDate) ?? TryParseYear(libraryItem.PremiereDate);
        var trackingYear = TryParseYear(trackingItem.ItemName) ?? TryParseYear(trackingItem.ExternalId);
        if (libraryYear.HasValue && trackingYear.HasValue)
        {
            if (libraryYear.Value == trackingYear.Value)
            {
                score += 10d;
            }
            else if (Math.Abs(libraryYear.Value - trackingYear.Value) <= 1)
            {
                score += 4d;
            }
        }

        var trackingType = NormalizeName(trackingItem.SourceType ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(trackingType))
        {
            var itemTypeMatches =
                (libraryItem.ItemType == LibraryItemType.Movie && trackingType.Contains("movie", StringComparison.Ordinal)) ||
                (libraryItem.ItemType == LibraryItemType.Series && trackingType.Contains("series", StringComparison.Ordinal)) ||
                (libraryItem.ItemType == LibraryItemType.Episode && trackingType.Contains("episode", StringComparison.Ordinal));

            if (itemTypeMatches)
            {
                score += 10d;
            }
        }

        if (!string.IsNullOrWhiteSpace(libraryItem.ParentId)
            && !string.IsNullOrWhiteSpace(trackingItem.ExternalId)
            && string.Equals(libraryItem.ParentId, trackingItem.ExternalId, StringComparison.OrdinalIgnoreCase))
        {
            score += 15d;
        }

        return Math.Clamp(score, 0d, 100d);
    }

    private static double TitleSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0d;
        }

        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0d;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();

        if (union == 0)
        {
            return 0d;
        }

        return (intersection * 100d) / union;
    }

    private static int? TryParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"(19|20)\d{2}");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Value, out var year) ? year : null;
    }

    public static int ParseEpisodeOrder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue;
        }

        var match = Regex.Match(value, @"(?:s\d{1,2})?e(\d{1,3})", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var episodeNumber))
        {
            return episodeNumber;
        }

        match = Regex.Match(value, @"(?:episode|ep)\s*(\d{1,3})", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out episodeNumber))
        {
            return episodeNumber;
        }

        return int.MaxValue;
    }

    public static string BuildTitleSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return NormalizeName(value)
            .Replace("episode", " ")
            .Replace("season", " ")
            .Replace("part", " ")
            .Replace("pt", " ");
    }

    public static bool HasKeywordMatch(string title, IEnumerable<string> keywords)
        => MatchesKeywords(title, keywords);
}

public static class PlaylistCatalogLegacyCompatibility
{
    public static bool IsHighPriority(PlaylistDefinition definition)
        => definition.Priority >= PlaylistPriority.High;
}

public static class PlaylistCatalogEntryPoint
{
    public static IReadOnlyCollection<PlaylistDefinition> AllDefinitions => PlaylistDefinitionCatalog.All;
}







































































































































 




























































































































































































































































































































































































































































































































n

















































































































n






n
































































n



n






n


















