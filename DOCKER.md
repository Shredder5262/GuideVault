# Guidevault Docker container

Guidevault can run as a Docker container for self-hosted use.

## Quick start

From the repo root:

```powershell
copy .env.example .env
notepad .env
docker compose up -d --build
```

Open:

```text
http://localhost:5000
```

## Required paths

The container has two important paths:

```text
/app/data   persistent Guidevault app data
/library    mounted manuals / strategy guides / magazines library
```

The default Compose file maps them from your host like this:

```text
GUIDEVAULT_DATA_PATH=./data
GUIDEVAULT_LIBRARY_PATH=./library
```

Edit `.env` to point `GUIDEVAULT_LIBRARY_PATH` at your real library folder.

Example Windows path:

```text
GUIDEVAULT_LIBRARY_PATH=C:/Users/Andrew/Documents/GuidevaultLibrary
```

Inside Guidevault, scan this path:

```text
/library
```

Docker cannot read arbitrary Windows paths from inside the container unless those paths are mounted. Existing Windows paths like `C:\...` or `\\server\share\...` need to be mapped into the container first.

## Persistent data

Keep this mounted between updates:

```text
GUIDEVAULT_DATA_PATH=./data
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

## Build manually

```powershell
docker build -t guidevault:0.9.22 -t guidevault:latest .
```

## Run manually without Compose

```powershell
docker run -d `
  --name guidevault `
  -p 5000:8080 `
  -e GUIDEVAULT_LIBRARY_PATH=/library `
  -v ${PWD}/data:/app/data `
  -v C:/Users/Andrew/Documents/GuidevaultLibrary:/library:ro `
  guidevault:0.9.22
```

Then open:

```text
http://localhost:5000
```

## Build release helper files

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\Build-GuidevaultDocker.ps1
```

To export a Docker image tar for another machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\Build-GuidevaultDocker.ps1 -SaveImageArchive
```

Load it elsewhere:

```powershell
docker load -i Guidevault-0.9.22-docker-image.tar
docker compose up -d
```
