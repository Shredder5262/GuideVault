# Guidevault

Self-hosted web reader and library manager for video game manuals, strategy guides, gaming magazines, maps, inserts, and related scanned game literature.

Guidevault is built for local collections. It scans files from folders you control, keeps the original files in place, and stores application data separately from your library.

![Guidevault main interface](docs/images/guidevault-main.png)

## Status

Guidevault is in active pre-release development.

Current working version: **0.9.75**

## Features

- Browser-based reader for `.cbz`, `.cbr`, and `.pdf` files.
- Library sections for manuals, strategy guides, magazines, favorites, and collections.
- Folder scanning for local or mounted libraries without uploading source files into the app.
- Details pages with editable metadata, notes, library data, and file information.
- Single-page, two-page, and adaptive two-page reader modes.
- Reader controls for fullscreen, bookmarks, zoom, magnifier, page transitions, backgrounds, and book-style shading.
- Reading profiles for per-series and per-item reader preferences.
- Local account/profile support.
- OPDS access for compatible readers.
- Review-first metadata lookups from Open Library, IGDB, and ESRB.org.

## Metadata lookups

Guidevault separates metadata sources by purpose:

| Source | Used for |
| --- | --- |
| Open Library | Guide/book metadata: title, author/writer, publisher, publish year, ISBN, language, summary, and page count |
| IGDB | Game metadata: game title, developer, publisher, release year, franchise/series, genre, and platforms |
| ESRB.org | Game rating lookup |

Metadata imports are review-first. Guidevault shows existing values beside source values, then lets you import all fields, empty fields only, or selected fields.

Open Library cover art is only shown as a visual comparison aid. It is not imported and does not replace the cover generated from the local file.

## Quick start with Docker

Create a folder and a default library folder:

```powershell
mkdir guidevault
cd guidevault
mkdir guidevault-data
mkdir guidevault-data\library
```

Create `compose.yaml`:

```yaml
services:
  guidevault:
    image: ghcr.io/shredder5262/guidevault:latest
    container_name: guidevault
    restart: unless-stopped
    ports:
      - "5478:5478"
    volumes:
      - "./guidevault-data:/data"
```

Start the container:

```powershell
docker compose up -d
```

Open:

```text
http://localhost:5478
```

Place manuals, strategy guides, and magazines in `./guidevault-data/library`, then scan `/data/library` inside Guidevault.

For using an existing library folder, see [Docker deployment](DOCKER.md).

## First run

On first launch, Guidevault creates the initial local profile. After the first profile exists, sign in with the username or email address configured during setup.

## Library setup

Guidevault scans your configured library folders and indexes supported files. It does not upload or move the original source files.

Suggested layout:

```text
Guidevault Library/
  Manuals/
    Nintendo Entertainment System/
    Super Nintendo Entertainment System/
    Sega Genesis/
  Strategy Guides/
    Nintendo/
    Sega/
    Sony Playstation/
  Magazines/
    Nintendo Power/
    Electronic Gaming Monthly/
    GamePro/
```

## Documentation

- [Docker deployment](DOCKER.md)
- [Documentation index](docs/README.md)
- [Library setup](docs/LIBRARY_SETUP.md)
- [Metadata](docs/METADATA.md)
- [Metadata sources](docs/METADATA_SOURCES.md)
- [Reader](docs/READER.md)
- [OPDS](docs/OPDS.md)
- [Release notes](docs/releases/README.md)

## Local development

From a local checkout:

```powershell
dotnet restore .\src\Guidevault.Web\Guidevault.Web.csproj
dotnet run --project .\src\Guidevault.Web\Guidevault.Web.csproj
```

Then open the localhost URL shown by `dotnet run`.

## Repository cleanup

Do not commit local runtime data, personal library files, one-off patch scripts, generated audit folders, or backup copies of the source tree.

Common cleanup command:

```powershell
git rm -r --ignore-unmatch patch.py _audit _backup scripts _repo _packaged Guidevault-source-clean
```

More detail is in [Repository hygiene](docs/REPOSITORY_HYGIENE.md).

## License

See [LICENSE](LICENSE).
