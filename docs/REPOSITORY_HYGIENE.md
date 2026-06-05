# Repository hygiene

Guidevault's repository should stay focused on source, documentation, static assets, Docker files, and CI workflow files.

## Keep in the repo

- Source code under `src/`
- Documentation under `docs/`
- Static web assets required by the app
- Docker and Compose examples
- CI workflow files

## Keep out of the repo

- Local runtime data
- Generated cache folders
- Database files
- Personal library files
- Exported source packages
- One-off patch scripts
- Audit folders
- Backup copies of source folders
- Local helper script folders that are not part of the app release

Examples that should not be tracked:

```text
patch.py
scripts/
_audit/
_backup/
_repo/
_packaged/
Guidevault-source-clean/
guidevault-data/
data/
cache/
logs/
library/
libraries/
*.db
*.sqlite
*.cbz
*.cbr
*.pdf
*.zip
*.tar
*.tar.gz
```

## Cleanup command

From the repo root:

```powershell
git rm -r --ignore-unmatch patch.py _audit _backup scripts _repo _packaged Guidevault-source-clean
```

Then commit the cleanup:

```powershell
git add .gitignore .dockerignore README.md DOCKER.md compose.example.yaml Dockerfile docs
git commit -m "Clean repository artifacts and refresh documentation"
```

## Why this matters

Generated audit and backup folders make the repository harder to review and can accidentally preserve outdated code. Runtime data and scanned source files can also make Docker builds slower and risk committing personal collection data.
