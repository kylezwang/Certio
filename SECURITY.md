# Security

Notal is a hosted product. This repository is a public copy of the code. Please do not probe the live site, and do not include passwords, tokens, or customer data in a report.

Use GitHub private vulnerability reporting on this repository. Do not file a public issue for a security bug.

The app expects secrets in environment variables or the host's secret store. `.env`, `appsettings.json`, and local database files are gitignored. If you find a credential in history anyway, report it the same way and treat it as compromised.
