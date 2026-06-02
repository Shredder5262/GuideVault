# Guidevault

Guidevault is a self-hosted web reader and library manager for video game manuals, strategy guides, and gaming magazines.

It is designed for local/self-hosted collections where the source files stay in your own folders. Guidevault indexes and reads your files in place; it does not require uploading, copying, or moving your manuals into the app.

Current release: **v0.9.30**

## What Guidevault does

Guidevault provides a browser-based library and reader for:

* Video game manuals
* Strategy guides
* Gaming magazines

Supported source formats:

* `.cbz`
* `.cbr`
* `.pdf`

## Core features

* Self-hosted web interface
* Docker-ready deployment
* Local file/folder library scanning
* Separate library areas for Manuals, Strategy Guides, and Magazines
* Reader with single-page, two-page, and adaptive page display modes
* Open-book style reader presentation with spine/gutter shading
* Page-turn/curl-style reader transitions
* Metadata editing per item
* Notes editing per item
* Favorites
* Search
* OPDS support for compatible external readers
* Local user profile/login support
* Reading profiles and reader preferences
* System/platform classification for retro gaming libraries
* Strategy guide support across multiple associated platforms
* Update history and system/performance diagnostics

## Current behavior

Guidevault scans files from configured library folders and builds an index.

It does **not** upload, copy, move, or delete your source files.

Removing an item from Guidevault removes it from the Guidevault library index only. It does not delete the original file from disk.

## Library types

Guidevault supports three main content types:

### Manuals

Manuals are organized primarily by detected/preferred platform.

Examples:

* Nintendo Entertainment System
* Super Nintendo Entertainment System
* Sega Genesis
* Sony PlayStation

### Strategy Guides

Strategy guides can be associated with one or more platforms.

If a strategy guide has multiple associated platforms, it can appear under each applicable platform library instead of being placed into a generic multi-platform bucket.

### Magazines

Magazines support issue-style metadata such as:

* Magazine title
* Issue number
* Volume
* Publication date
* Publisher
* Region
* Featured games/platforms

## Metadata

Guidevault can use a mix of:

* Folder path inference
* Filename inference
* Manual metadata edits
* `ComicInfo.xml` metadata
* Future Guidevault-native metadata

### Fast Rescan

Fast Rescan is the normal scan path. It prioritizes speed and uses folder/file information to quickly populate the library.

### Enrich Metadata

Enrich Metadata is a slower optional pass that opens supported archives and attempts to read embedded metadata such as `ComicInfo.xml`.

Use this when you want Guidevault to pull deeper metadata from files after the library has already been indexed.

## Suggested folder layout

```text
Guidevault Library/
  Manuals/
    Nintendo Entertainment System/
    Super Nintendo Entertainment System/
    Sega Genesis/

  Strategy Guides/
    Nintendo Entertainment System/
    Super Nintendo Entertainment System/
    Sony PlayStation/

  Magazines/
    Nintendo Power/
    Electronic Gaming Monthly/
```

The exact folder layout is flexible, but using clear platform and content-type folders improves automatic classification.

## Run locally

From the web project folder:

```powershell
cd C:\Users\Andrew\Documents\VSCode\PageQuest\src\PageQuest.Web
dotnet restore
dotnet run
```

Then open the local URL shown by `dotnet run`.

The default Guidevault port is:

```text
http://localhost:5478
```

Guidevault uses port `5478` by default to avoid conflicting with other self-hosted apps such as Kavita on port `5000`.

## Docker

The project includes Docker support.

Example:

```powershell
docker compose up -d --build
```

A safe example compose file is included as:

```text
compose.example.yaml
```

For real usage, mount your persistent Guidevault data folder and your read-only library folders as volumes.

Example concept:

```yaml
services:
  guidevault:
    image: ghcr.io/YOUR-GITHUB-NAME/guidevault:latest
    container_name: guidevault
    restart: unless-stopped
    ports:
      - "5478:5478"
    environment:
      - ASPNETCORE_URLS=http://+:5478
      - PAGEQUEST__DATA__ROOT=/data
    volumes:
      - ./data:/data
      - /path/to/manuals:/books:ro
```

Do not bake user data, scanned libraries, databases, OPDS keys, covers, cache files, or local settings into the Docker image.

## OPDS

Guidevault includes OPDS support for compatible external readers.

Use the OPDS settings screen inside Guidevault to generate or copy the OPDS URL and access key.

When connecting from another device on your network, use the host machine’s LAN IP address, not `localhost`.

Example:

```text
http://192.168.1.50:5478/opds/all
```

## Runtime data

Guidevault runtime data should stay outside the source repo and outside the Docker image.

Do not commit:

* `data/`
* database files
* cover cache
* thumbnail cache
* logs
* local app settings
* OPDS keys
* `.env` files
* personal library files
* manuals, magazines, strategy guides, PDFs, CBZs, or CBRs

## Development status

Guidevault is an early self-hosted release. The current focus is:

* Stable Docker deployment
* Faster library indexing
* Better metadata classification
* Lower idle memory usage
* OPDS compatibility
* Reader polish
* Safer repo/release packaging

## Recent release highlights

### v0.9.30

* Improved library ownership/classification behavior when Manual and Strategy Guide libraries are both configured.
* Added deterministic one-owner-per-file scan behavior.
* Improved repair behavior for cached items whose type no longer matches the owning library.

### v0.9.29

* Expanded retro platform classification aliases.
* Improved classification for additional systems such as Atari, Amstrad, ColecoVision, Intellivision, Vectrex, Neo Geo, MSX, Commodore, and others.
* Fast rescans can repair previously unsorted items when improved inference can classify them.

### v0.9.28

* Fixed false-positive strategy guide classification caused by broad substring matching.
* Improved whole-word metadata classification behavior.

### v0.9.27

* Added Fast Rescan behavior for quicker indexing.
* Added optional Enrich Metadata flow for slower embedded metadata parsing.
* Added indexing performance documentation.

### v0.9.26

* Reduced memory pressure from cover/image caching.
* Added system performance diagnostics.
* Added stable Docker image update notification support.

## License

License information has not been finalized yet.
