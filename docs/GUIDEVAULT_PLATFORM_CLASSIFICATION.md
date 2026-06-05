# Guidevault platform classification

Guidevault should normalize source platform names to the platform/icon names used by the app.

## Naming rules

- Use the existing Guidevault platform/icon name when one exists.
- Keep platform spelling consistent across imported metadata, details pages, and badges.
- Treat associated platforms as a multi-value field.
- Use preferred platform for the best single platform context when appropriate.

## Examples

| Source value | Guidevault value |
| --- | --- |
| PC (Microsoft Windows) | Windows |
| DOS | MS-DOS |
| MS DOS | MS-DOS |
| Playstation | Sony Playstation |
| Playstation 2 | Sony Playstation 2 |
| Playstation 3 | Sony Playstation 3 |
| Playstation Portable | Sony PSP |
| Dreamcast | Sega Dreamcast |
| Xbox | Microsoft Xbox |

## Associated vs preferred platform

If a game has one platform:

```text
Associated platforms = that platform
Preferred platform = that platform
```

If a game has multiple platforms:

```text
Associated platforms = all applicable platforms
Preferred platform = selected manually or Multi-platform
```

Manual override should remain available because ports, compilations, regional versions, and remakes can make external source data noisy.
