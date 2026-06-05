# Docker install and update guide

Guidevault runs as a self-hosted Docker container on port **5478**.

## Default install

Create `compose.yaml`:

```yaml
services:
  guidevault:
    image: ghcr.io/shredder5262/guidevault:latest
    container_name: guidevault
    restart: unless-stopped
    ports:
      - "5478:5478"
    environment:
      ASPNETCORE_URLS: "http://+:5478"
      ASPNETCORE_HTTP_PORTS: "5478"
      GUIDEVAULT_DATA: "/data"
      GUIDEVAULT_LIBRARY_PATH: "/data/library"
      GUIDEVAULT__UPDATES__CHANNEL: "stable"
      GUIDEVAULT__UPDATES__CURRENTIMAGE: "ghcr.io/shredder5262/guidevault:latest"
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

This creates one host folder:

```text
./guidevault-data
```

The default library folder is:

```text
./guidevault-data/library
```

Inside the container, scan:

```text
/data/library
```

## Mount an existing library

Use this layout when your collection already lives somewhere else:

```yaml
services:
  guidevault:
    image: ghcr.io/shredder5262/guidevault:latest
    container_name: guidevault
    restart: unless-stopped
    ports:
      - "5478:5478"
    environment:
      ASPNETCORE_URLS: "http://+:5478"
      ASPNETCORE_HTTP_PORTS: "5478"
      GUIDEVAULT_DATA: "/data"
      GUIDEVAULT_LIBRARY_PATH: "/library"
      GUIDEVAULT__UPDATES__CHANNEL: "stable"
      GUIDEVAULT__UPDATES__CURRENTIMAGE: "ghcr.io/shredder5262/guidevault:latest"
    volumes:
      - "./guidevault-data:/data"
      - "D:/Digital Literature:/library:ro"
```

Inside Guidevault, scan:

```text
/library
```

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
