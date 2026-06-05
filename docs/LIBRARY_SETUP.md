# Library setup

Guidevault is designed to scan and index local files. It does not upload, move, or rewrite your original source files during normal library scanning.

## Supported content

Guidevault is focused on video game literature:

- Game manuals
- Strategy guides
- Gaming magazines
- Maps and inserts
- Reference material
- Related scanned game documents

## Supported file types

```text
.cbz
.cbr
.pdf
```

## Suggested layout

Guidevault can scan any configured folder, but a predictable structure makes browsing and cleanup easier:

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

## Docker library paths

Default Docker setup:

```text
Host:      ./guidevault-data/library
Container: /data/library
```

Existing library mount example:

```text
Host:      D:/Digital Literature
Container: /library
```

Inside Guidevault, always scan the container path, not the host path.

## Scan behavior

A library scan indexes files and generates app-side data such as cache and metadata records. The original files remain in the library folder.

Recommended workflow:

1. Put files in the library folder.
2. Add or confirm the folder path in Guidevault.
3. Run a scan.
4. Review item type, platform, and metadata.
5. Use metadata lookups or manual editing to improve records.

## Content classification

Guidevault separates content into three main types:

- Manual
- Strategy Guide
- Magazine

Some mixed or unusual items may need manual correction after scan.
