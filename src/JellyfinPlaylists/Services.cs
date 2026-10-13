using System.Data;
using Microsoft.Data.Sqlite;

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
            var typeName = GetString(reader, "Type");
            var itemType = typeName?.Trim() switch
            {
                "Movie" => LibraryItemType.Movie,
                "Series" => LibraryItemType.Series,
                "Episode" => LibraryItemType.Episode,
                _ => LibraryItemType.Unknown
            };

            if (itemType == LibraryItemType.Unknown)
            {
                continue;
            }

            items.Add(new LibraryItemRecord(
                Id: GetString(reader, "Id") ?? Guid.NewGuid().ToString("N"),
                Name: GetString(reader, "Name") ?? "Unknown",
                SortName: GetString(reader, "SortName"),
                ItemType: itemType,
                ParentId: GetString(reader, "ParentId"),
                SeriesId: GetString(reader, "SeriesId"),
                Path: GetString(reader, "Path"),
                ReleaseDate: GetString(reader, "PremiereDate") ?? GetString(reader, "ProductionYear"),
                PremiereDate: GetString(reader, "PremiereDate"),
                ProviderId: GetString(reader, "ProviderIds"),
                LibraryId: GetString(reader, "LibraryId"),
                Genres: ParseGenres(GetString(reader, "Genres"))));
        }

        return items;
    }

    private static string? GetString(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            string s => s,
            long l => l.ToString(),
            int i => i.ToString(),
            _ => reader.GetValue(ordinal)?.ToString()
        };
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

public sealed class PlaylistTrackingReader : IPlaylistTrackingReader
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
        var tableName = tableNames.Contains("PlaylistTracking", StringComparer.OrdinalIgnoreCase)
            ? "PlaylistTracking"
            : tableNames.Contains("PlaylistItems", StringComparer.OrdinalIgnoreCase)
                ? "PlaylistItems"
                : null;

        if (tableName is null)
        {
            return results;
        }

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
                PlaylistName: GetNullableString(reader, "PlaylistName") ?? "Unknown",
                ItemId: GetNullableString(reader, "ItemId") ?? string.Empty,
                ItemName: GetNullableString(reader, "ItemName"),
                SourceType: GetNullableString(reader, "ItemType"),
                AddedAt: GetNullableDateTimeOffset(reader, "AddedAt"),
                IsPresent: !reader.IsDBNull(reader.GetOrdinal("IsPresent")) && reader.GetBoolean(reader.GetOrdinal("IsPresent")),
                ExternalId: GetNullableString(reader, "ExternalId")));
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
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
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

public sealed class PlaylistAnalyzer : IPlaylistAnalyzer
{
    public Task<PlaylistCompletionReport> AnalyzeAsync(
        string playlistName,
        IReadOnlyCollection<LibraryItemRecord> libraryItems,
        IReadOnlyCollection<PlaylistTrackingRecord> trackingItems,
        CancellationToken cancellationToken = default)
    {
        var definition = PlaylistDefinitionCatalog.GetByName(playlistName);
        var filteredLibrary = libraryItems
            .Where(LibraryItemFilters.IsVideoOnly)
            .Where(item => PlaylistMatchHelper.MatchesDefinition(item, definition))
            .ToArray();

        var trackingByPlaylist = trackingItems
            .Where(item => string.Equals(item.PlaylistName, playlistName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var present = new List<PlaylistGap>();
        var missing = new List<PlaylistGap>();

        foreach (var item in filteredLibrary)
        {
            var bestMatch = trackingByPlaylist
                .Select(trackingItem => new
                {
                    TrackingItem = trackingItem,
                    Score = PlaylistMatchHelper.ScoreLibraryItemMatch(item, trackingItem)
                })
                .Where(x => x.Score >= 70d)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            var isPresent = bestMatch is not null;
            var gap = new PlaylistGap(
                PlaylistName: playlistName,
                ItemId: item.Id,
                ItemName: item.Name,
                ParentId: item.ParentId,
                IsMissing: !isPresent,
                Reason: isPresent
                    ? $"Tracked in playlist ({bestMatch.Score:F1})"
                    : "Not found in playlist tracking");

            if (isPresent)
            {
                present.Add(gap);
            }
            else
            {
                missing.Add(gap);
            }
        }

        var completionPercent = filteredLibrary.Length == 0 ? 100d : (present.Count * 100d) / filteredLibrary.Length;

        return Task.FromResult(new PlaylistCompletionReport(
            PlaylistName: playlistName,
            MissingItems: missing,
            PresentItems: present,
            CompletionPercent: completionPercent,
            IsComplete: missing.Count == 0));
    }
}

public sealed class ChronologicalPlaylistBuilder : IChronologicalPlaylistBuilder
{
    public IReadOnlyCollection<LibraryItemRecord> OrderChronologically(
        IReadOnlyCollection<LibraryItemRecord> items,
        string playlistName)
    {
        return items
            .OrderBy(item => GetSortDate(item))
            .ThenBy(item => PlaylistMatchHelper.ParseEpisodeOrder(item.Name))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        _libraryReader = libraryReader ?? new LibraryDatabaseReader();
        _trackingReader = trackingReader ?? new PlaylistTrackingReader();
        _playlistAnalyzer = playlistAnalyzer ?? new PlaylistAnalyzer();
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

    public async Task<JellyfinPlaylistExecutionResult> ExecuteAsync(
        string libraryDatabasePath,
        string trackingDatabasePath,
        CancellationToken cancellationToken = default)
    {
        var libraryItems = await _libraryReader.LoadLibraryAsync(libraryDatabasePath, cancellationToken);
        var trackingItems = await _trackingReader.LoadTrackingAsync(trackingDatabasePath, cancellationToken);

        var reports = new Dictionary<string, PlaylistCompletionReport>(StringComparer.OrdinalIgnoreCase);
        var orderedPlaylists = new Dictionary<string, IReadOnlyCollection<LibraryItemRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in PlaylistDefinitionCatalog.All)
        {
            var filteredItems = libraryItems
                .Where(LibraryItemFilters.IsVideoOnly)
                .Where(item => PlaylistMatchHelper.MatchesDefinition(item, definition))
                .ToArray();

            var report = await _playlistAnalyzer.AnalyzeAsync(definition.Name, filteredItems, trackingItems, cancellationToken);
            reports[definition.Name] = report;
            orderedPlaylists[definition.Name] = _chronologicalBuilder.OrderChronologically(filteredItems, definition.Name);
        }

        return new JellyfinPlaylistExecutionResult(reports, orderedPlaylists);
    }
}

public sealed record JellyfinPlaylistExecutionResult(
    IReadOnlyDictionary<string, PlaylistCompletionReport> Reports,
    IReadOnlyDictionary<string, IReadOnlyCollection<LibraryItemRecord>> OrderedPlaylists)
{
    public int HighPriorityMissingCount =>
        Reports.Where(pair => PlaylistDefinitionCatalog.GetByName(pair.Key)?.Priority >= PlaylistPriority.High)
            .Sum(pair => pair.Value.MissingItems.Count);
}
