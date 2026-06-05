# Docker install and update guide

Guidevault runs as a self-hosted Docker container on port **5478**.

## Simple install

Create a working folder and the default library folder:

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

Start:

```powershell
docker compose up -d
```

Open:

```text
http://localhost:5478
```

Place library files in:

```text
./guidevault-data/library
```

Inside Guidevault, scan:

```text
/data/library
```

No environment variables are needed for the standard install. The Docker image defaults to:

```text
Port: 5478
App data: /data
Default library: /data/library
```

## Use an existing library folder

Use this layout when your collection already lives somewhere else:

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

The `:ro` means the library is mounted read-only. Guidevault still stores its own settings, cache, profiles, OPDS keys, and metadata in `./guidevault-data`.

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

This folder contains app settings, generated cache, metadata, profiles, OPDS keys, and other persistent Guidevault data.
