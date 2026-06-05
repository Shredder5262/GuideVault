# Guidevault Library Scope Classification

Guidevault 0.9.30 tightens how files are assigned when more than one configured library can see the same physical file.

## Why this matters

A common layout is to keep multiple literature folders near each other, such as:

- `/Digital Literature/Video Game Manuals`
- `/Digital Literature/Strategy Guides`
- `/Digital Literature/Magazines`

If a broad parent folder and a narrower child folder are both configured as libraries, the same CBZ/CBR/PDF can be discovered twice during one scan. Earlier fast-index behavior could allow the Manual library and Strategy Guide library to fight over the same cached item, which made manuals appear under Strategy Guides or vice versa.

## Current behavior

During discovery, Guidevault now keeps exactly one owner per physical file:

1. The most-specific configured folder wins.
2. If roots are equally specific, an explicitly typed library wins over a Mixed library.
3. If a library is left as Mixed but its name/folder clearly says Manuals, Strategy Guides, or Magazines, Guidevault normalizes the library type accordingly.
4. Fast rescans repair cached items whose kind no longer matches the owning library type.

## Recommended setup

For best results, configure separate library roots that do not overlap:

- Manuals → `/Digital Literature/Video Game Manuals`
- Strategy Guides → `/Digital Literature/Strategy Guides`
- Magazines → `/Digital Literature/Video Game Magazines`

Avoid adding both a broad parent folder and one of its children unless the parent is intended to act as a Mixed catch-all.
