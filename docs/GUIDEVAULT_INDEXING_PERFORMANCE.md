# Guidevault Indexing Performance

Guidevault 0.9.27 splits library work into two paths so large libraries become usable sooner.

## Fast Rescan

Fast Rescan is the default scan path. It discovers supported files, compares each file against the persisted index using path, size, and modified time, and reuses unchanged items without reopening their archives.

For new or changed CBZ files, Fast Rescan now infers basic metadata from the file and folder path first. It intentionally defers ComicInfo.xml parsing because opening every archive can be slow on large local collections and very slow on network-mounted libraries.

## Enrich Metadata

The Enrich Metadata action is the slower, optional pass. It opens CBZ archives at low concurrency and imports ComicInfo.xml metadata after the library is already indexed and usable.

Use this after a first scan when you want deeper metadata such as publication fields, tags, issue details, guide details, or richer platform information from ComicInfo.xml.

## Cleanup

Cleanup remains a safe reconciliation action. It removes stale/untracked entries and refreshes changed files without doing deep validation of every archive.

## Diagnostics

Info -> System -> Performance now includes the last scan summary, including candidate count, reused items, parsed items, deferred metadata imports, enriched metadata count, skipped unreadable files, and elapsed time.
