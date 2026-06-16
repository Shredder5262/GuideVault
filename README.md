# GuideVault

Self-hosted web reader and library manager for video game manuals, strategy guides, and gaming magazines.

GuideVault is built for people who keep local collections of scanned game literature and want a clean browser-based way to organize, browse, read, and preserve them.

## Current Release

**Version:** v1.0.0

GuideVault is now in its first public release stage. The application is intended for local and self-hosted use with user-managed library files.

## What GuideVault Does

GuideVault helps organize and read video game literature collections, including:

* Game manuals
* Strategy guides
* Gaming magazines
* Reference books
* Inserts, maps, posters, and related scanned material

## Core Features

* Self-hosted web interface
* Browser-based reader
* Manual, strategy guide, and magazine library sections
* CBZ and CBR support
* Local folder scanning
* Metadata editing and cleanup tools
* Details pages for library items
* Associated platform metadata
* Single-page, two-page, and adaptive reader layouts
* Book-style reader visuals
* Page transitions, fullscreen reading, zoom, and magnifier controls
* User accounts and reading profiles
* Customizable home shelves and navigation
* OPDS support for compatible readers
* Docker deployment support
* PDF-to-CBZ conversion with Poppler included in the published Docker image

## Supported File Types

GuideVault is currently focused on scanned reading formats:

```text
.cbz
.cbr
.pdf
```

The published Docker image includes Poppler so PDF-to-CBZ conversion works without manually installing `pdftoppm` or `pdftocairo` inside the container.

## Suggested Library Layout

GuideVault can scan a configured library folder. A structure like this keeps larger collections easier to manage:

```text
GuideVault Library/
  Manuals/
    Nintendo Entertainment System/
    Super Nintendo Entertainment System/
    Sega Genesis/

  Strategy Guides/
    Nintendo/
    Sega/
    Sony PlayStation/

  Magazines/
    Nintendo Power/
    Electronic Gaming Monthly/
    GamePro/
```

## Quick Start with Docker

Create a folder for GuideVault data and mount your existing library folder into the container.

```yaml
services:
  guidevault:
    image: ghcr.io/shredder5262/guidevault:latest
    container_name: guidevault
    restart: unless-stopped
    ports:
      - "5478:5478"
    environment:
      ASPNETCORE_URLS: "http://+:5478"
      GUIDEVAULT_DATA: "/data"
      GUIDEVAULT_LIBRARY_PATH: "/library"
    volumes:
      - ./guidevault-data:/data
      - D:/GameLibrary/Guides:/library:ro
```

Start the container:

```bash
docker compose pull
docker compose up -d
```

Then open:

```text
http://localhost:5478
```

GuideVault uses port `5478` by default so it can coexist with other self-hosted readers that commonly use port `5000`.

## First Run

On first launch, GuideVault creates the initial local user account.

First-run setup asks for:

* Username
* Email address
* Password

After the first account exists, users can sign in with either their username or email address.

## Library Scanning

GuideVault scans and indexes local files. Your original manuals, guides, and magazines remain in the library folder you choose.

GuideVault stores app data, generated cache, metadata, profile data, and settings separately under the configured data path.

## Metadata

GuideVault includes editable metadata designed around video game literature.

Metadata may include:

* Title
* Series
* Preferred platform
* Associated platforms
* Publisher
* Year
* Region
* Language
* Issue number
* Volume / number
* Guide type
* Edition type
* ISBN / ASIN
* Summary
* Tags
* Notes

Legacy `ComicInfo.xml` import support is included where applicable. GuideVault-native metadata is the preferred direction for richer manual, strategy guide, and magazine data.

## Reader

The reader supports book-focused reading behavior, including:

* Cover-first reading
* Single-page mode
* Two-page spreads
* Adaptive two-page handling
* Fullscreen reading
* Page transitions
* Bookmark support
* Zoom and magnifier controls
* Background and shading preferences
* Per-item and per-series reading profiles

## OPDS

GuideVault includes OPDS support for compatible reading clients.

Default local address:

```text
http://localhost:5478
```

## Releases

Releases are published through the GitHub Releases page.

The current public release is:

```text
v1.0.0
```

## Contributing and Feature Requests

Use GitHub Issues for bug reports, feature requests, and enhancement discussions.

Planned or future work should be tracked through GitHub Issues and Milestones instead of being listed directly in this README.

## License

See `LICENSE`.
