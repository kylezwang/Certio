# Deployment

Two workflows deploy from the `production` branch.

| Workflow | What it ships | Host |
|----------|---------------|------|
| `production_notal-app.yml` | The .NET solution | Azure Web App `Notal-app` |
| `production_notal-ai.yml` | The `ai_agents` Docker image | Azure Web App `notal-ai` |

The .NET workflow restores, builds Release, runs tests, generates an idempotent SQL script, deploys with the publish profile stored as a GitHub secret, then polls `/healthz`.

The Python workflow compiles the core modules, runs pytest, pushes an image to the container registry, deploys that image, then polls `/health`.

`pr-validation.yml` runs on pull requests to `production` and `main`. It builds, tests, checks that migrations still script, and runs `scripts/check_text_hygiene.py`. That script fails if a tracked text file contains an emoji, an em dash, or an en dash. Vendor files under `wwwroot/lib` and EF migration files are skipped.

A push to `production` deploys. Do not use that branch for experiments.
