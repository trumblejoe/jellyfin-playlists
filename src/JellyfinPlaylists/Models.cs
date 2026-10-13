namespace JellyfinPlaylists;

public enum LibraryItemType
{
    Unknown = 0,
    Movie,
    Series,
    Episode,
    Music,
    Other
}

public enum PlaylistPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public sealed record LibraryItemRecord(
    string Id,
    string Name,
    string? SortName,
    LibraryItemType ItemType,
    string? ParentId,
    string? SeriesId,
    string? Path,
    string? ReleaseDate,
    string? PremiereDate,
    string? ProviderId,
    string? LibraryId,
    string[] Genres);

public sealed record PlaylistTrackingRecord(
    string PlaylistName,
    string ItemId,
    string? ItemName,
    string? SourceType,
    DateTimeOffset? AddedAt,
    bool IsPresent,
    string? ExternalId);

public sealed record PlaylistDefinition(
    string Name,
    PlaylistPriority Priority,
    IReadOnlyCollection<string> Keywords,
    IReadOnlyCollection<string> ExcludedKeywords,
    bool IncludeMovies,
    bool IncludeSeries,
    bool IncludeEpisodes,
    int? TargetCount = null);

public sealed record PlaylistGap(
    string PlaylistName,
    string ItemId,
    string ItemName,
    string? ParentId,
    bool IsMissing,
    string Reason);

public sealed record PlaylistCompletionReport(
    string PlaylistName,
    IReadOnlyCollection<PlaylistGap> MissingItems,
    IReadOnlyCollection<PlaylistGap> PresentItems,
    double CompletionPercent,
    bool IsComplete);

public sealed class PlaylistTarget
{
    public required string Name { get; init; }
    public required string[] Keywords { get; init; }
    public required string[] ExcludedKeywords { get; init; }
    public PlaylistPriority Priority { get; init; } = PlaylistPriority.High;
    public bool IncludeMovies { get; init; } = true;
    public bool IncludeSeries { get; init; } = true;
    public bool IncludeEpisodes { get; init; } = false;

    public static IReadOnlyCollection<PlaylistTarget> DefaultTargets() =>
    [
        new PlaylistTarget
        {
            Name = "Arrowverse Chronological",
            Keywords = ["arrow", "flash", "supergirl", "legends", "batwoman", "arrowverse"],
            ExcludedKeywords = ["music"],
            Priority = PlaylistPriority.Critical,
            IncludeMovies = false,
            IncludeSeries = true,
            IncludeEpisodes = true
        },
        new PlaylistTarget
        {
            Name = "Marvel MCU Chronological",
            Keywords = ["marvel", "mcu", "avengers", "captain america", "iron man", "thor", "guardians"],
            ExcludedKeywords = ["music"],
            Priority = PlaylistPriority.Critical,
            IncludeMovies = true,
            IncludeSeries = true,
            IncludeEpisodes = true
        }
    ];
}
