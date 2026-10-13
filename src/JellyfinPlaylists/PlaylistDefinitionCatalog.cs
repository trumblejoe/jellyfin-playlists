namespace JellyfinPlaylists;

public static class PlaylistDefinitionCatalog
{
    public static readonly PlaylistDefinition ArrowverseChronological = new(
        Name: "Arrowverse Chronological",
        Priority: PlaylistPriority.Critical,
        Keywords: ["arrow", "flash", "supergirl", "legends", "batwoman", "arrowverse"],
        ExcludedKeywords: ["music"],
        IncludeMovies: false,
        IncludeSeries: true,
        IncludeEpisodes: true,
        TargetCount: null);

    public static readonly PlaylistDefinition MarvelMcuChronological = new(
        Name: "Marvel MCU Chronological",
        Priority: PlaylistPriority.Critical,
        Keywords: ["marvel", "mcu", "captain america", "iron man", "thor", "avengers", "guardians"],
        ExcludedKeywords: ["music"],
        IncludeMovies: true,
        IncludeSeries: true,
        IncludeEpisodes: true,
        TargetCount: null);

    public static IReadOnlyCollection<PlaylistDefinition> All =>
    [
        ArrowverseChronological,
        MarvelMcuChronological
    ];

    public static PlaylistDefinition? GetByName(string name)
    {
        return All.FirstOrDefault(playlist => string.Equals(playlist.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

public static class PlaylistDefinitionCatalogExtensions
{
    public static bool IsHighPriority(this PlaylistDefinition definition)
        => definition.Priority >= PlaylistPriority.High;
}
