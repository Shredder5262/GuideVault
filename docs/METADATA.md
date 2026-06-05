# Metadata

Guidevault supports editable metadata designed around game manuals, strategy guides, and gaming magazines.

## Common fields

- Title
- Sort title
- Series
- Publisher
- Year
- Region
- Language
- Summary
- Notes
- Tags

## Strategy guide fields

- Strategy guide title
- Game title
- Author / writer
- Publisher
- Publish year
- ISBN-10
- ISBN-13
- ASIN
- Page count
- Guide type
- Edition type
- Associated platforms
- Preferred platform

## Game metadata fields

- Game title
- Developer
- Publisher
- Release year
- Franchise / series
- Genre
- Associated platforms
- Preferred platform
- ESRB rating

## Magazine fields

- Issue number
- Month
- Year
- Volume
- Number
- Region
- Language
- Publisher
- Featured games
- Featured platforms
- Cover story
- Special features
- Staff
- ISSN / barcode

## Guidevault-native metadata

The preferred long-term metadata format is Guidevault-native metadata using `guidevault.json`.

`ComicInfo.xml` remains supported as a legacy import source, but it was not designed around game manuals, strategy guides, or gaming magazines.

## Import behavior

Metadata imports should be reviewed before applying.

Preferred flow:

1. Search a source.
2. Select a result.
3. Compare existing Guidevault values with source values.
4. Import all fields, empty fields only, or selected fields.

Existing metadata should not be overwritten automatically.
