# Jellyfin Playlists

A Jellyfin plugin for generating and maintaining curated movie and TV playlists from the Jellyfin library database and playlist-tracking metadata.

This project is intentionally focused on:
- Movies and TV shows only
- Ignoring music and other media types
- Comparing what exists in the Jellyfin library against tracking data for each playlist
- Prioritizing chronological watch-order playlists, especially:
  - Arrowverse
  - Marvel MCU

## Goals

- Read the Jellyfin library database and enumerate movie/series entries that are actually present
- Read playlist-tracking data to understand which items are already accounted for or missing
- Identify missing titles per playlist by normalized metadata match
- Produce a watch-order plan in chronological order for the highest-priority collections
- Keep the implementation plugin-friendly for Jellyfin, but keep the core logic isolated and testable

## High-priority playlists

1. Arrowverse chronological
2. Marvel MCU chronological

These are the first playlists to be implemented and validated against the library + tracking data.

## Architectural direction

The project is structured into a few layers:

- Core domain models for library items and playlist tracking records
- Database readers that query the Jellyfin library data source
- Playlist gap analysis that compares library items against tracking metadata
- Chronological ordering logic for the curated collections
- Optional Jellyfin plugin entry points for exposing functionality to the app

## Repository status

This is the initial skeleton for the project so the repo is ready for implementation work.

Planned next steps:
- Add the Jellyfin-native plugin entry point
- Implement SQLite readers against the Jellyfin library database
- Add playlist definitions and matching rules
- Build missing-item calculations for each priority playlist
- Add tests for Arrowverse and Marvel MCU ordering & completeness

## Notes

- Music is explicitly excluded from the library scan and playlist generation logic.
- Item matching should normalize names and release metadata to reduce false negatives.
- Playlist generation should prefer deterministic chronological ordering over fuzzy or user-edited ordering.
