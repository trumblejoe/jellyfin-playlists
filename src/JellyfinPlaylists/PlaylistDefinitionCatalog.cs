using System.Data;
using Microsoft.Data.Sqlite;

namespace JellyfinPlaylists;

public sealed class JellyfinDbLibraryReader : ILibraryDatabaseReader
{
    public async Task<IReadOnlyCollection<LibraryItemRecord>> LoadLibraryAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A Jellyfin library database path is required.", nameof(databasePath));
        }

        var items = new List<LibraryItemRecord>();

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                Id,
                Name,
                SortName,
                Type,
                ParentId,
                SeriesId,
                Path,
                PremiereDate,
                ProductionYear,
                ProviderIds,
                LibraryId,
                Genres
            FROM BaseItems
            WHERE IsFolder = 0
              AND Type IN ('Movie', 'Series', 'Episode');";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemType = MapItemType(GetString(reader, "Type"));
            if (itemType == LibraryItemType.Unknown)
            {
                continue;
            }

            var item = new LibraryItemRecord(
                id: GetString(reader, "Id") ?? Guid.NewGuid().ToString("N"),
                name: GetString(reader, "Name") ?? "Unknown",
                sortName: GetString(reader, "SortName"),
                itemType: itemType,
                parentId: GetString(reader, "ParentId"),
                seriesId: GetString(reader, "SeriesId"),
                path: GetString(reader, "Path"),
                releaseDate: GetString(reader, "PremiereDate") ?? GetString(reader, "ProductionYear"),
                premiereDate: GetString(reader, "PremiereDate"),
                providerId: GetString(reader, "ProviderIds"),
                libraryId: GetString(reader, "LibraryId"),
                genres: ParseGenres(GetString(reader, "Genres")));

            items.Add(item);
        }

        return items;
    }

    private static LibraryItemType MapItemType(string? typeName)
    {
        return typeName?.Trim() switch
        {
            "Movie" => LibraryItemType.Movie,
            "Series" => LibraryItemType.Series,
            "Episode" => LibraryItemType.Episode,
            _ => LibraryItemType.Unknown
        };
    }

    private static string? GetString(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetString(ordinal);
    }

    private static string[] ParseGenres(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class JellyfinPlaylistTrackingReader : IPlaylistTrackingReader
{
    public async Task<IReadOnlyCollection<PlaylistTrackingRecord>> LoadTrackingAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A playlist tracking database path is required.", nameof(databasePath));
        }

        var results = new List<PlaylistTrackingRecord>();

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        var tableNames = await GetTableNamesAsync(connection, cancellationToken);
        if (!tableNames.Contains("PlaylistTracking", StringComparer.OrdinalIgnoreCase) &&
            !tableNames.Contains("PlaylistItems", StringComparer.OrdinalIgnoreCase))
        {
            return results;
        }

        var tableName = tableNames.Contains("PlaylistTracking", StringComparer.OrdinalIgnoreCase)
            ? "PlaylistTracking"
            : "PlaylistItems";

        var sql = $@"
            SELECT
                PlaylistName,
                ItemId,
                ItemName,
                ItemType,
                AddedAt,
                IsPresent,
                ExternalId
            FROM {tableName};";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PlaylistTrackingRecord(
                playlistName: reader.IsDBNull(reader.GetOrdinal("PlaylistName")) ? "Unknown" : reader.GetString(reader.GetOrdinal("PlaylistName")),
                itemId: reader.IsDBNull(reader.GetOrdinal("ItemId")) ? string.Empty : reader.GetString(reader.GetOrdinal("ItemId")),
                itemName: GetNullableString(reader, "ItemName"),
                sourceType: GetNullableString(reader, "ItemType"),
                addedAt: GetNullableDateTimeOffset(reader, "AddedAt"),
                isPresent: !reader.IsDBNull(reader.GetOrdinal("IsPresent")) && reader.GetBoolean(reader.GetOrdinal("IsPresent")),
                externalId: GetNullableString(reader, "ExternalId")));
        }

        return results;
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static string? GetNullableString(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dt => new DateTimeOffset(dt),
            DateTimeOffset dto => dto,
            string str when DateTimeOffset.TryParse(str, out var parsed) => parsed,
            _ => null
        };
    }
}

public sealed class PlaylistGapAnalyzer : IPlaylistAnalyzer
{
    public Task<PlaylistCompletionReport> AnalyzeAsync(
        string playlistName,
        IReadOnlyCollection<LibraryItemRecord> libraryItems,
        IReadOnlyCollection<PlaylistTrackingRecord> trackingItems,
        CancellationToken cancellationToken = default)
    {
        var target = PlaylistDefinitionCatalog.GetByName(playlistName);
        var videoItems = libraryItems
            .Where(item => item.ItemType is LibraryItemType.Movie or LibraryItemType.Series or LibraryItemType.Episode)
            .Where(item => MatchesTarget(item, target))
            .ToArray();

        var trackingByPlaylist = trackingItems
            .Where(item => string.Equals(item.PlaylistName, playlistName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var trackedIds = new HashSet<string>(trackingByPlaylist
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemId))
            .Select(item => item.ItemId), StringComparer.OrdinalIgnoreCase);

        var trackedNames = trackingByPlaylist
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemName))
            .Select(item => PlaylistMatchHelper.NormalizeName(item.ItemName!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var present = new List<PlaylistGap>();
        var missing = new List<PlaylistGap>();

        foreach (var item in videoItems)
        {
            var trackedById = trackedIds.Contains(item.Id);
            var trackedByName = trackedNames.Contains(PlaylistMatchHelper.NormalizeName(item.Name));
            var isPresent = trackedById || trackedByName;

            var gap = new PlaylistGap(
                playlistName,
                item.Id,
                item.Name,
                item.ParentId,
                !isPresent,
                isPresent ? "Tracked in playlist" : "Not found in playlist tracking");

            if (isPresent)
            {
                present.Add(gap);
            }
            else
            {
                missing.Add(gap);
            }
        }

        var completionPercent = videoItems.Count == 0 ? 100d : (present.Count * 100d) / videoItems.Count;
        var isComplete = missing.Count == 0;

        return Task.FromResult(new PlaylistCompletionReport(
            playlistName,
            missing,
            present,
            completionPercent,
            isComplete));
    }

    private static bool MatchesTarget(LibraryItemRecord item, PlaylistDefinition? definition)
    {
        if (definition is null)
        {
            return true;
        }

        var normalizedName = PlaylistMatchHelper.NormalizeName(item.Name);

        var matchesKeyword = definition.Keywords.Any(keyword =>
            PlaylistMatchHelper.NormalizeName(keyword).Length > 0 &&
            normalizedName.Contains(PlaylistMatchHelper.NormalizeName(keyword), StringComparison.Ordinal));

        if (!matchesKeyword)
        {
            return false;
        }

        var isExcluded = definition.ExcludedKeywords.Any(keyword =>
            PlaylistMatchHelper.NormalizeName(keyword).Length > 0 &&
            normalizedName.Contains(PlaylistMatchHelper.NormalizeName(keyword), StringComparison.Ordinal));

        return !isExcluded;
    }
}

public sealed class ChronologicalPlaylistBuilder : IChronologicalPlaylistBuilder
{
    public IReadOnlyCollection<LibraryItemRecord> OrderChronologically(
        IReadOnlyCollection<LibraryItemRecord> items,
        string playlistName)
    {
        var ordered = items
            .OrderBy(item => GetSortDate(item))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ordered;
    }

    private static DateTimeOffset GetSortDate(LibraryItemRecord item)
    {
        foreach (var candidate in new[] { item.ReleaseDate, item.PremiereDate })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (DateTimeOffset.TryParse(candidate, out var parsed))
            {
                return parsed;
            }

            if (int.TryParse(candidate, out var year))
            {
                return new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
        }

        return DateTimeOffset.MaxValue;
    }
}

public sealed class JellyfinPlaylistEngine
{
    private readonly ILibraryDatabaseReader _libraryReader;
    private readonly IPlaylistTrackingReader _trackingReader;
    private readonly IPlaylistAnalyzer _playlistAnalyzer;
    private readonly IChronologicalPlaylistBuilder _chronologicalBuilder;

    public JellyfinPlaylistEngine(
        ILibraryDatabaseReader? libraryReader = null,
        IPlaylistTrackingReader? trackingReader = null,
        IPlaylistAnalyzer? playlistAnalyzer = null,
        IChronologicalPlaylistBuilder? chronologicalBuilder = null)
    {
        _libraryReader = libraryReader ?? new JellyfinDbLibraryReader();
        _trackingReader = trackingReader ?? new JellyfinPlaylistTrackingReader();
        _playlistAnalyzer = playlistAnalyzer ?? new PlaylistGapAnalyzer();
        _chronologicalBuilder = chronologicalBuilder ?? new ChronologicalPlaylistBuilder();
    }

    public async Task<IReadOnlyDictionary<string, PlaylistCompletionReport>> AnalyzePlaylistsAsync(
        string libraryDatabasePath,
        string trackingDatabasePath,
        CancellationToken cancellationToken = default)
    {
        var libraryItems = await _libraryReader.LoadLibraryAsync(libraryDatabasePath, cancellationToken);
        var trackingItems = await _trackingReader.LoadTrackingAsync(trackingDatabasePath, cancellationToken);

        var results = new Dictionary<string, PlaylistCompletionReport>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in PlaylistDefinitionCatalog.All)
        {
            var report = await _playlistAnalyzer.AnalyzeAsync(definition.Name, libraryItems, trackingItems, cancellationToken);
            results[definition.Name] = report;
        }

        return results;
    }

    public IReadOnlyCollection<LibraryItemRecord> BuildOrderedPlaylist(
        string playlistName,
        IReadOnlyCollection<LibraryItemRecord> items)
    {
        return _chronologicalBuilder.OrderChronologically(items, playlistName);
    }
}
