# Metadata sources

Guidevault uses separate lookup lanes for book/guide metadata, game metadata, and rating metadata.

## Open Library

Use Open Library for guide/book metadata:

- Title
- Author / writer
- Publisher
- Publish year
- ISBN-10
- ISBN-13
- Language
- Description / summary
- Page count

Open Library search is title-first. ISBN can be used when known, but most strategy guide lookups should start with the strategy guide title or item title.

Open Library covers are preview-only. They help confirm the selected result but are never imported and never replace the cover generated from the local file.

## IGDB

Use IGDB for game metadata:

- Game title
- Game developer
- Game publisher
- Game release year
- Game franchise / series
- Genre
- Associated platforms
- Preferred platform

IGDB lookup should search by game title first, then use platform and year as hints.

IGDB credentials are configured from Guidevault settings.

## ESRB.org

Use ESRB.org for game rating lookup:

- ESRB rating

ESRB lookup should use the game title, not the strategy guide title.

## Platform normalization

Imported platform names should be normalized to Guidevault platform/icon names when possible.

| Source value | Guidevault value |
| --- | --- |
| PC (Microsoft Windows) | Windows |
| DOS | MS-DOS |
| MS DOS | MS-DOS |
| Playstation | Sony Playstation |
| Playstation 2 | Sony Playstation 2 |
| Playstation 3 | Sony Playstation 3 |
| Playstation Portable | Sony PSP |
| Dreamcast | Sega Dreamcast |
| Xbox | Microsoft Xbox |

If a source returns multiple platforms, Guidevault stores them as associated platforms. Preferred platform can remain a specific platform or be set to multi-platform depending on the item.
