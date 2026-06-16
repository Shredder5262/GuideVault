# GuideVault Docker container

GuideVault can run as a Docker container for self-hosted use.

The published GuideVault image includes `poppler-utils` in the final runtime image. That provides `pdftoppm` and `pdftocairo`, which GuideVault uses for PDF-to-CBZ conversion. No manual `apt-get install` should be needed after pulling a newly published image.

## Quick start from published package

Create or update `.env` from `.env.example`, then run:

```powershell
docker compose pull
docker compose up -d
```

Open:

```text
http://localhost:5478
```

## Required paths

The container has two important paths:

```text
/data      persistent GuideVault app data
/library   mounted manuals / strategy guides / magazines library
```

The default Compose file maps them from your host like this:

```text
GUIDEVAULT_DATA_PATH=./guidevault-data
GUIDEVAULT_LIBRARY_PATH=./library
```

Edit `.env` to point `GUIDEVAULT_LIBRARY_PATH` at your real library folder.

Example Windows path:

```text
GUIDEVAULT_LIBRARY_PATH=C:/Users/Andrew/Documents/GuidevaultLibrary
```

Inside GuideVault, scan this path:

```text
/library
```

Docker cannot read arbitrary Windows paths from inside the container unless those paths are mounted. Existing Windows paths like `C:\...` or `\\server\share\...` need to be mapped into the container first.

## Persistent data

Keep this mounted between updates:

```text
GUIDEVAULT_DATA_PATH=./guidevault-data
```

It stores:

```text
config
cache
metadata overrides
OPDS keys
device history
system identity
local library settings
```

Do not delete it unless you intentionally want to reset the container.

## Build locally

```powershell
docker build --no-cache -t guidevault:latest .
```

## Run manually without Compose

```powershell
docker run -d `
  --name guidevault `
  -p 5478:5478 `
  -e ASPNETCORE_URLS=http://+:5478 `
  -e GUIDEVAULT_DATA=/data `
  -e GUIDEVAULT_LIBRARY_PATH=/library `
  -v ${PWD}/guidevault-data:/data `
  -v C:/Users/Andrew/Documents/GuidevaultLibrary:/library:ro `
  ghcr.io/shredder5262/guidevault:latest
```

Then open:

```text
http://localhost:5478
```

## Verify PDF rasterizer support

After pulling or building a new image, verify Poppler is available inside the running container:

```powershell
docker exec guidevault sh -lc "command -v pdftoppm; pdftoppm -v; command -v pdftocairo; pdftocairo -v"
```

Expected paths:

```text
/usr/bin/pdftoppm
/usr/bin/pdftocairo
```

If those commands are missing, the running container was created from an older image and needs to be recreated after pulling the newly published package.

## Update to the latest package

```powershell
docker compose pull
docker compose down
docker compose up -d
```

Or without Compose:

```powershell
docker stop guidevault
docker rm guidevault
docker pull ghcr.io/shredder5262/guidevault:latest
```

Then recreate the container using your normal `docker run` command.
