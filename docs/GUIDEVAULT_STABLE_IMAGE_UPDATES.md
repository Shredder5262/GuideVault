# Stable image updates

Guidevault Docker deployments should be able to update with normal Compose commands.

## Update commands

```powershell
docker compose pull
docker compose up -d
```

## Image channel

The stable image is expected to use:

```text
ghcr.io/shredder5262/guidevault:latest
```

The app can display update information using:

```text
GUIDEVAULT__UPDATES__CHANNEL=stable
GUIDEVAULT__UPDATES__CURRENTIMAGE=ghcr.io/shredder5262/guidevault:latest
```

## Persistent data

Keep `/data` mounted to a persistent host folder. Do not store persistent data inside the container filesystem only.
