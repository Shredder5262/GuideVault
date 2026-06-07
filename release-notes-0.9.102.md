## GuideVault 0.9.102

This update focuses on OPDS pagination fixes, metadata lookup improvements, and cleanup across the details and metadata management screens.

### Added

- Added OPDS catalog pagination with 20, 50, and 100 item page sizes.
- Added OPDS item detail views with Read, Open Details, and Download actions.
- Added /opds/v1 support as an alias for the OPDS catalog route.
- Added IGDB credential testing under metadata source settings.
- Added Metadata Manager controls for loading beyond the first 250 rendered rows.

### Improved

- OPDS pageSize now carries forward when browsing categories such as All Items, Manuals, Strategy Guides, Magazines, and Series.
- Improved Open Library, IGDB, and ESRB individual lookup dialogs.
- Improved IGDB and ESRB result cards with more readable platform/system and metadata details.
- Improved batch metadata lookup behavior so only one source can be active at a time.
- Improved batch lookup preview behavior so the comparison image follows the active source.
- Improved Open Library secondary hint behavior to use ISBN, year, or blank only.

### Fixed

- Fixed /opds/v1 incorrectly falling through to the web login page.
- Fixed pageSize=20 not working correctly on OPDS connections.
- Fixed stale batch lookup results mixing into newer searches.
- Fixed individual lookup fields using the previous guide after Details Previous / Next navigation.
- Fixed styled metadata info icons on detail pages.
- Fixed Strategy Guide identity display by removing Game Title from the identity container.
- Fixed OPDS detail page build issue caused by raw string interpolation braces.
