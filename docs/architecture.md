# Jellyfin Playlists

This solution is the initial skeleton for the Jellyfin playlists plugin. It is structured for incremental implementation without requiring a live Jellyfin server during early development.

## Projects

- `src/JellyfinPlaylists` — domain logic and plugin orchestration code
- `tests/JellyfinPlaylists.Tests` — unit tests for playlist matching and ordering logic

## Current status

The code in this repo currently covers the foundational models and service interfaces needed to implement:
- movie/TV filtering
- library scans
- playlist tracking inputs
- missing-item analyses
- chronological playlist ordering

## Next implementation milestones

1. Add SQLite reading against the Jellyfin library database
2. Add playlist tracking readers
3. Add matching logic for Arrowverse and Marvel MCU
4. Add chronology rules and ordering
5. Integrate plugin entry points for Jellyfin
6. Validate against real Jellyfin data
