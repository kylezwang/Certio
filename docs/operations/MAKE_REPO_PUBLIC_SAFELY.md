# Making the Repo Public Safely (Remove Secrets from Git History)

## Why it matters

- **Making the repo public is valuable:** ~571 contributions since September would become visible on your profile. Right now (private) recruiters see none of that. A strong green graph + a serious project is a real differentiator.
- **Never make it public with secrets in history.** Old commits stay in the repo forever; anyone can clone and read them. Leaked API keys can be abused and look unprofessional.

**Bottom line:** Remove the secrets from history first, then make the repo public. You keep the contribution graph and avoid risk.

---

## Option A: Rewrite history to remove secrets (recommended)

This keeps all your commits and contribution graph but removes the sensitive files from every commit.

### 1. Rotate any keys that were ever committed

Assume anything that was in an old commit is compromised. Rotate:

- Azure OpenAI API keys (Azure Portal → Azure OpenAI → Keys)
- Any other API keys or secrets that were in `.env` or config files

### 2. Install `git-filter-repo` (recommended) or BFG

**git-filter-repo** (fast, modern):

```bash
# Windows (PowerShell) - install via pip
pip install git-filter-repo
```

Or download from: https://github.com/newren/git-filter-repo

**BFG Repo-Cleaner** (alternative): https://rtyley.github.io/bfg-repo-cleaner/

### 3. Backup and run the rewrite

**Backup first:**

```bash
cd C:\Projects\Certio   # or your Notal repo path
git clone . ../Certio-backup
```

**Remove sensitive files from all history:**

```bash
# Remove .env and common secret file names from entire history
git filter-repo --path .env --invert-paths --force
git filter-repo --path ai_agents/.env --invert-paths --force
git filter-repo --path appsettings.Development.json --invert-paths --force
git filter-repo --path appsettings.json --invert-paths --force
```

If you had different filenames (e.g. `secrets.txt`), add them:

```bash
git filter-repo --path path/to/secretfile --invert-paths --force
```

**Replace specific strings (e.g. a key that was in a code file):**

```bash
# Only if a key was hardcoded in a .py or .cs file
git filter-repo --replace-text <(echo "OLD_API_KEY==>REPLACED")
```

(`--replace-text` takes a file of `literal:replacement` lines; see git-filter-repo docs.)

### 4. Re-add remote and force-push

`git filter-repo` removes remotes by design. Re-add and push:

```bash
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO.git
git push --force origin main
```

**Warning:** Force-pushing rewrites history. Anyone else with a clone must re-clone. If this is only your repo, you’re fine.

### 5. Then make the repo public

GitHub → repo → Settings → Danger Zone → Change repository visibility → Public.

---

## Option B: Fresh repo (no history, no green graph from this repo)

If you don’t care about preserving this repo’s history:

1. Create a **new** GitHub repo (e.g. `notal-public`).
2. Copy only the **current** tree (no `.git`):

   ```bash
   cd C:\Projects\Certio
   # Exclude .env, bin, obj, etc.
   git archive main | tar -x -C ../notal-clean
   cd ../notal-clean
   git init
   git add .
   git commit -m "Initial public release"
   git remote add origin https://github.com/YOUR_USERNAME/notal-public.git
   git push -u origin main
   ```

3. Make that new repo public.

**Downside:** Your profile won’t show the 571 contributions from this project in the new repo (they’re tied to the old repo). So Option A is better if you want the graph.

---

## After going public

- Add a **README** with project overview, setup (without secrets), and maybe architecture.
- Ensure **.env** and all secret files stay in **.gitignore** (they already are in this project).
- Use **environment variables** or a secrets manager in production; never commit keys.

---

## Quick reference: files to strip from history (Option A)

Based on this repo’s `.gitignore`, these are good candidates to remove from history if they were ever committed:

- `.env`
- `ai_agents/.env`
- `appsettings.Development.json`
- `appsettings.json`
- `appsettings.Production.json`
- `secrets.json`

Run `git filter-repo --path <path> --invert-paths --force` for each that was ever committed.
