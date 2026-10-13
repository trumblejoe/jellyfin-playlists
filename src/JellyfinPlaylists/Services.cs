namespace JellyfinPlaylists;

public interface ILibraryDatabaseReader
{
    Task<IReadOnlyCollection<LibraryItemRecord>> LoadLibraryAsync(string databasePath, CancellationToken cancellationToken = default);
}

public interface IPlaylistTrackingReader
{
    Task<IReadOnlyCollection<PlaylistTrackingRecord>> LoadTrackingAsync(string databasePath, CancellationToken cancellationToken = default);
}

public interface IPlaylistAnalyzer
{
    Task<PlaylistCompletionReport> AnalyzeAsync(
        string playlistName,
        IReadOnlyCollection<LibraryItemRecord> libraryItems,
        IReadOnlyCollection<PlaylistTrackingRecord> trackingItems,
        CancellationToken cancellationToken = default);
}

public interface IChronologicalPlaylistBuilder
{
    IReadOnlyCollection<LibraryItemRecord> OrderChronologically(
        IReadOnlyCollection<LibraryItemRecord> items,
        string playlistName);
}

public sealed class LibraryDatabaseReader : ILibraryDatabaseReader
{
    public Task<IReadOnlyCollection<LibraryItemRecord>> LoadLibraryAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        // Placeholder: this will be implemented against the Jellyfin SQLite database.
        // Only movies and TV content should be returned; music and other media types are ignored.
        return Task.FromResult<IReadOnlyCollection<LibraryItemRecord>>([]);
    }
}

public sealed class PlaylistTrackingReader : IPlaylistTrackingReader
{
    public Task<IReadOnlyCollection<PlaylistTrackingRecord>> LoadTrackingAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        // Placeholder: this will read tracking metadata and playlist assignments from the configured data source.
        return Task.FromResult<IReadOnlyCollection<PlaylistTrackingRecord>>([]);
    }
}

public sealed class PlaylistAnalyzer : IPlaylistAnalyzer
{
    public Task<PlaylistCompletionReport> AnalyzeAsync(
        string playlistName,
        IReadOnlyCollection<LibraryItemRecord> libraryItems,
        IReadOnlyCollection<PlaylistTrackingRecord> trackingItems,
        CancellationToken cancellationToken = default)
    {
        var filteredLibrary = libraryItems
            .Where(item => item.ItemType is LibraryItemType.Movie or LibraryItemType.Series or LibraryItemType.Episode)
            .ToArray();

        var missing = filteredLibrary
            .Select(item => new PlaylistGap(
                playlistName,
                item.Id,
                item.Name,
                item.ParentId,
                true,
                "Not found in playlist tracking"))
            .ToArray();

        var present = Array.Empty<PlaylistGap>();
        var completionPercent = missing.Length == 0 && filteredLibrary.Count == 0 ? 100d : 0d;

        return Task.FromResult(new PlaylistCompletionReport(
            playlistName,
            missing,
            present,
            completionPercent,
            false));
    }
}

public sealed class ChronologicalPlaylistBuilder : IChronologicalPlaylistBuilder
{
    public IReadOnlyCollection<LibraryItemRecord> OrderChronologically(IReadOnlyCollection<LibraryItemRecord> items, string playlistName)
    {
        return items
            .OrderBy(item => ParseYear(item.ReleaseDate ?? item.PremiereDate ?? "0000-01-01"))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ParseYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return int.MaxValue;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.Year;
        }

        return int.MaxValue;
    }
}
