# Jellyfin Playlists Requirements

## Scope

This plugin is only concerned with:
- Movies
- TV shows
- Episode/series metadata needed for ordering and tracking

The plugin intentionally excludes:
- Music
- Audio books
- Photo libraries
- Other non-video media

## Read model

The plugin should read from the Jellyfin library database to determine:
- which movies are present
- which series are present
- which episodes are present, where needed for ordering
- the metadata necessary to map to playlist tracking data

The database read should be focused on the minimal set of fields required for matching and ordering:
- Item ID
- Name / sort title
- Item type (movie, series, episode)
- Parent/series ID
- Release date / premiere date
- Provider metadata / external IDs if available
- Path / library ID for source validation

## Playlist tracking model

The plugin should read playlist-tracking data to determine:
- which items are already assigned to a playlist
- which items are missing from a playlist
- whether a playlist is complete or needs items added

Tracking data should be normalized per playlist so the plugin can calculate gaps without needing to inspect the UI state directly.

## Priority playlists

### Arrowverse chronology
The plugin should support generating a chronological watch order across Arrowverse entries, and prioritize items that exist in the Jellyfin library but are missing from the tracking list.

### Marvel MCU chronology
The plugin should support generating a chronological order across the MCU timeline and identify missing items in the same way.

## Expected outputs

For each target playlist, the plugin should produce:
- the full ordered watch list
- the set of items present in Jellyfin
- the set of missing items based on playlist tracking
- a summary of completion gaps

## Implementation constraints

- Keep data access isolated from the playlist business logic
- Use deterministic ordering for chronology
- Prefer clear matching on normalized names and release metadata over brittle string comparisons
- Keep plugin logic testable without a live Jellyfin server
