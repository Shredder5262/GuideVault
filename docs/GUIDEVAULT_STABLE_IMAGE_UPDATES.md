# Guidevault stable image update notifications

Guidevault 0.9.26 can show a notification when a newer stable container image is available.

Configure the app with a stable JSON feed URL. In Docker Compose, use environment variables like these:

```yaml
environment:
  - ASPNETCORE_URLS=http://+:5478
  - PAGEQUEST__UPDATES__STABLEFEEDURL=https://example.com/updates/guidevault-stable.json
  - PAGEQUEST__UPDATES__CHANNEL=stable
  - PAGEQUEST__UPDATES__CURRENTIMAGE=ghcr.io/YOUR-NAME/guidevault:0.9.26
```

The feed can be hosted anywhere static files are served, such as Cloudflare Pages, Cloudflare R2, GitHub Pages, or your own website.

Example `guidevault-stable.json`:

```json
{
  "channel": "stable",
  "version": "0.9.27",
  "image": "ghcr.io/YOUR-NAME/guidevault:0.9.27",
  "publishedAt": "2026-06-02T00:00:00Z",
  "url": "https://github.com/YOUR-NAME/guidevault/releases/tag/v0.9.27",
  "notes": [
    "Reduced idle memory usage.",
    "Improved cover cache behavior.",
    "Added update notification polish."
  ]
}
```

When `version` is newer than the running app version, Guidevault shows a top-bar update icon and the Info → System → Stable Updates panel reports the newer image.

Updating the container is still done from the Docker host:

```powershell
docker compose pull
docker compose up -d
```
