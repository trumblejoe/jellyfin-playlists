using Xunit;

namespace JellyfinPlaylists.Tests;

public sealed class OrderedPlaylistTests
{
    [Fact]
    public void ChronologicalPlaylistBuilder_OrdersByReleaseDate()
    {
        var items = new[]
        {
            new LibraryItemRecord(
                "b",
                "Second",
                "Second",
                LibraryItemType.Movie,
                null,
                null,
                "/media/second.mkv",
                "2016-01-01",
                null,
                null,
                null,
                []),
            new LibraryItemRecord(
                "a",
                "First",
                "First",
                LibraryItemType.Movie,
                null,
                null,
                "/media/first.mkv",
                "2012-01-01",
                null,
                null,
                null,
                [])
        };

        var ordered = new ChronologicalPlaylistBuilder().OrderChronologically(items, "Example Playlist");

        Assert.Equal("First", ordered.First().Name);
        Assert.Equal("Second", ordered.Last().Name);
    }
}
