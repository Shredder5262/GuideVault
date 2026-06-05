# Guidevault Platform Classification

Guidevault 0.9.29 expands the fast-index platform detector for retro video-game literature collections.

The fast scan path intentionally avoids opening every CBZ/CBR archive during normal rescans. Because of that, platform detection relies primarily on folder names, file names, and lightweight path inference. This update adds aliases for more retro systems so valid manuals are less likely to land in **Unsorted Manuals**.

Added/expanded platform families include Atari, Amstrad, ColecoVision, Daphne/LaserDisc arcade, Intellivision, Odyssey 2/Videopac, Vectrex, Philips CD-i, Neo Geo, Commodore, MSX, ZX Spectrum, Sharp X68000, FM Towns, and Apple II.

If a platform still appears as unsorted, add the platform name to the folder path or update the detector aliases in `MetadataInferer.DetectSystemsFromText`.


## Cached unsorted items

Fast rescans normally reuse unchanged files from the persisted index. In 0.9.29, cached items whose system/category is still Unsorted are re-run through the lightweight filename/folder inference path so classification improvements can repair existing records without opening every archive.
