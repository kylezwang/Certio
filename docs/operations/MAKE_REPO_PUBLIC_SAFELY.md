# Making the Repo Public Safely (Remove Secrets from Git History)

## Status (2026-09)

**History cleanup for committed secrets and local DB artifacts is done** on the remotes that were
force-pushed after `git filter-repo` (`main`, `production`, `mac-development`, `desktop-development`).

Completed:

- Rotated / revoked keys that had lived in committed env files (treat any pre-rewrite clone as hostile).
- Removed from git history:
  - `.env`, `ai_agents/.env`
  - `Certio.Web/appsettings.json`, `Certio.Web/appsettings.Development.json`
  - tracked `**/bin/**/appsettings*.json` copies
  - `Certio.Web/app.db`, `Certio.Web/bin/Debug/net8.0/app.db`, `Certio.Web/bin/Debug/net9.0/app.db`
  - `.vs/slnx.sqlite`
- Force-pushed rewritten branches; re-added `origin` after each `filter-repo` run (it removes remotes).

Still optional before / after going public:

- Confirm GitHub code search shows no `.env` / `app.db` hits; old commit SHAs may 404 after GC.
- Enable GitHub secret scanning / add CI secret scanning if not already on.
- Flip visibility: Settings → Danger Zone → Public (only after verification below).

The sections below remain the runbook if you need to repeat a strip or clean another path.

---

## Why it matters

- **Making the repo public is valuable:** contributions become visible on your profile. A strong green
  graph + a serious project is a real differentiator.
- **Never make it public with secrets in history.** Old commits stay in the repo forever; anyone can
  clone and read them. Leaked API keys and Identity hashes in SQLite can be abused and look unprofessional.

**Bottom line:** Remove secrets from history first, then make the repo public. You keep the contribution
graph and avoid risk. Rotation revokes access; history rewrite only stops *new* clones from seeing old
values.

---

## Option A: Rewrite history to remove secrets (recommended)

This keeps all your commits and contribution graph but removes the sensitive files from every commit.

### 1. Rotate any keys that were ever committed

Assume anything that was in an old commit is compromised. Rotate or revoke:

- OpenAI project keys (`sk-proj-...`) if unused — revoke entirely
- Azure OpenAI / Foundry keys (Portal or AI Foundry → resource → Keys and endpoint)
- OAuth client secrets (Google Cloud Console, Entra app registrations)
- Maps, Document Intelligence, SMTP, and any other secrets from `.env`
- Replace `AI_API_KEY` with a random shared secret (it is Certio.Web ↔ `ai_agents` auth, not an OpenAI key)

Do not leave old values working “because the new ones are different.”

### 2. Install `git-filter-repo`

```powershell
pip install git-filter-repo
```

On Windows, `git filter-repo` often fails with `git: 'filter-repo' is not a git command` because the
Python Scripts folder is not on PATH. Prefer:

```powershell
python -m git_filter_repo --help
```

Use underscores: `git_filter_repo`, not `git_filter-repo`.

Or download from: https://github.com/newren/git-filter-repo

**BFG Repo-Cleaner** (alternative): https://rtyley.github.io/bfg-repo-cleaner/

### 3. Backup and run the rewrite

**Backup first:**

```powershell
cd C:\Projects\Certio
git clone . ../Certio-backup
```

**Remove sensitive files from all history** (repeat per path; each run may remove `origin` again):

```powershell
python -m git_filter_repo --path .env --invert-paths --force
python -m git_filter_repo --path ai_agents/.env --invert-paths --force
python -m git_filter_repo --path Certio.Web/appsettings.json --invert-paths --force
python -m git_filter_repo --path Certio.Web/appsettings.Development.json --invert-paths --force
python -m git_filter_repo --path-glob "**/bin/**/appsettings*.json" --invert-paths --force
python -m git_filter_repo --path Certio.Web/app.db --invert-paths --force
python -m git_filter_repo --path Certio.Web/bin/Debug/net8.0/app.db --invert-paths --force
python -m git_filter_repo --path Certio.Web/bin/Debug/net9.0/app.db --invert-paths --force
python -m git_filter_repo --path .vs/slnx.sqlite --invert-paths --force
```

If you had different filenames (e.g. `secrets.txt`):

```powershell
python -m git_filter_repo --path path/to/secretfile --invert-paths --force
```

**Replace specific strings** (only if a key was hardcoded in source):

```powershell
# Write a file replacements.txt with lines: OLD_SECRET==>REPLACED
python -m git_filter_repo --replace-text replacements.txt
```

### 4. Re-add remote and force-push every cleaned branch

`git filter-repo` removes remotes by design:

```powershell
git remote add origin https://github.com/kylezwang/Certio.git
# if origin already exists:
# git remote set-url origin https://github.com/kylezwang/Certio.git

git push --force origin main
git push --force origin production
git push --force origin mac-development
git push --force origin desktop-development
# push any other branches that still had the old history
```

**Warning:** Force-pushing rewrites history. Anyone else with a clone must re-clone.

### 5. Verify, then make the repo public

**Local:**

```powershell
git ls-files .env
git ls-files ai_agents/.env
git ls-files Certio.Web/appsettings.json Certio.Web/appsettings.Development.json
git ls-files | Select-String "\.(db|sqlite)$"
git log --all --oneline -- .env
git log --all --oneline -- ai_agents/.env
git log --all --oneline -- Certio.Web/app.db
```

Expect no output from those commands.

**GitHub:** On each branch, confirm `.env` / `app.db` / secret appsettings are gone. Code search, e.g.
`repo:kylezwang/Certio filename:.env` or `filename:app.db`. Old pre-rewrite commit URLs should
eventually 404.

Then: Settings → Danger Zone → Change repository visibility → Public.

---

## Option B: Fresh repo (no history, no green graph from this repo)

If you don’t care about preserving this repo’s history:

1. Create a **new** GitHub repo (e.g. `notal-public`).
2. Copy only the **current** tree (no `.git`):

   ```powershell
   cd C:\Projects\Certio
   git archive main | tar -x -C ../notal-clean
   cd ../notal-clean
   git init
   git add .
   git commit -m "Initial public release"
   git remote add origin https://github.com/YOUR_USERNAME/notal-public.git
   git push -u origin main
   ```

3. Make that new repo public.

**Downside:** Profile contributions from this project stay tied to the old repo. Option A is better if
you want the graph.

---

## After going public

- Keep a **README** with setup that never pastes real secrets (use placeholders / App Service / Key Vault).
- Ensure **`.env`**, secret `appsettings*`, and `*.db` / `.vs/` stay **untracked** (listed in
  `.gitignore`; `.gitignore` does not untrack files already in the index).
- Prefer environment variables or a secrets manager in production; never commit keys or local databases.

---

## Quick reference: files to strip from history (Option A)

| Path | Notes |
|------|--------|
| `.env` | Root secrets |
| `ai_agents/.env` | Was tracked despite `.gitignore` |
| `Certio.Web/appsettings.json` | Prefer env placeholders only; strip if ever held real secrets |
| `Certio.Web/appsettings.Development.json` | Same |
| `**/bin/**/appsettings*.json` | Build output copies; should never be tracked |
| `Certio.Web/app.db` (+ `bin/**/app.db`) | May contain Identity password hashes |
| `.vs/slnx.sqlite` | IDE junk; should never be tracked |
| `appsettings.Production.json`, `secrets.json` | If ever committed |

Run `python -m git_filter_repo --path <path> --invert-paths --force` for each that was ever committed.
