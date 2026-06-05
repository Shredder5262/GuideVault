# Guidevault Docker deployment

Guidevault is intended to run cleanly as a self-hosted Docker container.

The default application port is **5478**.

## Quick start

Create a folder for Guidevault and a default library folder:

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

Start Guidevault:

```powershell
docker compose up -d
```

Open:

```text
http://localhost:5478
```

Place manuals, strategy guides, and magazines in:

```text
./guidevault-data/library
```

Inside Guidevault, scan this library path:

```text
/data/library
```

That is the simplest install path. The container already defaults to port `5478`, app data at `/data`, and the default library folder at `/data/library`, so no environment variables are needed for a normal install.

## Use an existing library folder

Use this layout when your collection already lives somewhere else. The example below keeps Guidevault settings/cache in `./guidevault-data` and mounts your existing library directly as `/data/library`.

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
      - "D:/Digital Literature:/data/library:ro"
```

Inside Guidevault, scan:

```text
/data/library
```

On Windows, use Docker-style paths such as `D:/Digital Literature`. Docker cannot read arbitrary host folders unless they are mounted into the container.

## Update

```powershell
docker compose pull
docker compose up -d
```

## Logs

```powershell
docker logs -f guidevault
```

## Backup

Back up the persistent data folder:

```text
./guidevault-data
```

This folder contains app settings, generated cache, metadata, reading profiles, OPDS keys, and other persistent Guidevault data.

## Reset

To reset the app completely, stop the container and delete the persistent data folder:

```powershell
docker compose down
Remove-Item -Recurse -Force .\guidevault-data
```

Only do this if you intentionally want to remove local Guidevault settings, generated cache, metadata, profiles, OPDS keys, and other app data.

## Build locally

```powershell
docker build -t guidevault:local .
docker run -d --name guidevault -p 5478:5478 -v ${PWD}/guidevault-data:/data guidevault:local
```

Open:

```text
http://localhost:5478
```
