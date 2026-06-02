# Guidevault v0.9.7

Guidevault is a first-version self-hosted web reader for video game manuals, strategy guides, and gaming magazines.

## v0.7 reader update

- Adds a stronger open-book spine/gutter shader between two-page spreads.
- Fullscreen spreads now render as one joined book surface instead of two disconnected panels.
- Page curl animation was softened and shaded to feel more like paper turning across the center.

## Run locally

```powershell
cd C:\Users\Andrew\Documents\VSCode\PageQuest\src\PageQuest.Web
dotnet restore
dotnet run
```

Open the localhost URL shown by `dotnet run`. The default local Guidevault port is `http://localhost:5478` so it does not conflict with Kavita on port 5000.

## What v0.5 adds

- Library root path is no longer shown on the main library screen.
- Library root is now managed from **Settings**.
- Drill-in details page tabs now work:
  - Overview
  - Metadata
  - Notes
- Metadata can be edited and saved per item.
- Notes can be edited and saved per item.
- Items can be removed from the library index without touching, uploading, copying, or deleting the source files.
- CBZ/CBR imports read `ComicInfo.xml` metadata:
  - Title
  - Series
  - Number / issue number
  - Writer
  - Publisher
  - Year
  - Summary / Notes
  - Genre
  - Tags
- Magazines use `ComicInfo.xml` issue numbers for category sorting when available.
- Reader now treats page 1 as the cover, then switches to two-page book spreads for interior pages.
- Reader page-turn animation is adjusted to behave more like a page curl across an open book.

## Suggested folder layout

```text
Guidevault Library\
  Manuals\
    Super Nintendo (SNES)\
    Sega Genesis\

  Strategy Guides\
    Super Nintendo (SNES)\
    PlayStation (PS1)\

  Magazines\
    Nintendo Power\
    Electronic Gaming Monthly\
```

Supported files: `.pdf`, `.cbz`, `.cbr`.

## Docker preparation

The project includes a `Dockerfile` and `docker-compose.yml`. The current local test path is still the recommended path while the reader/import UI is being shaped.


## v0.7.1

- Left sidebar now starts collapsed/icon-only by default. Use the hamburger button to expand it.


## v0.7.3

- The old right-side details drawer has been removed. Clicking an item opens a dedicated details page.
- Left sidebar remains open by default.

## v0.9.1

- Visible branding now uses Guidevault with a small GV cartridge-vault icon.
- The details overview is content-focused: detected system, ESRB rating, optional web link, issue/publisher/year/writer/pages, summary, and tags.
- ComicInfo.xml `Series` is treated as the detected system/category for scanned guide/manual items.
- Cover rendering now uses contain-style sizing so rectangular/wide manuals are not cropped.
- The visible ODSP placeholder card was removed from the settings UI.


## v0.9.7

- Strategy guides now appear in every platform library listed in Associated Platforms.
- The old multi-platform bucket is no longer used for library placement when associated platforms are available.
- Visible System / Category labels were renamed to Preferred Platform.
