# AI Agents Documentation

**Last Updated:** July 1, 2026

Documentation for the Certio Python AI service (`ai_agents/`). For the service overview and runtime instructions, see [`../README.md`](../README.md).

---

## Structure

```
ai_agents/
├── README.md          — Service overview, dependencies, running locally
└── docs/
    ├── INDEX.md       — This file
    ├── QUICK_START.md
    ├── AZURE_PRODUCTION_GUIDE.md
    ├── MIGRATION_GUIDE.md
    ├── LLM_TRAINING_GUIDE.md
    ├── RAG_SYSTEM_IMPROVEMENTS.md
    ├── ADVANCED_OPTIMIZATION_GUIDE.md
    └── archive/       — Outdated fix logs, status snapshots, session summaries
```

---

## Guides

| Document | Description |
|----------|-------------|
| [`QUICK_START.md`](QUICK_START.md) | Get the AI service running locally in minutes |
| [`AZURE_PRODUCTION_GUIDE.md`](AZURE_PRODUCTION_GUIDE.md) | Deploying and operating the service on Azure |
| [`MIGRATION_GUIDE.md`](MIGRATION_GUIDE.md) | Migrating between AI service versions |
| [`LLM_TRAINING_GUIDE.md`](LLM_TRAINING_GUIDE.md) | Training and fine-tuning LLM models |
| [`RAG_SYSTEM_IMPROVEMENTS.md`](RAG_SYSTEM_IMPROVEMENTS.md) | RAG pipeline enhancements and configuration |
| [`ADVANCED_OPTIMIZATION_GUIDE.md`](ADVANCED_OPTIMIZATION_GUIDE.md) | Performance and cost optimization strategies |

---

## Archive

The [`archive/`](archive/) folder contains historical session documents:

| Document | Notes |
|----------|-------|
| `CONVERSATIONAL_RESPONSE_FIX.md` | Bug fix log — type error in conversational response |
| `FILE_ANALYSIS.md` | One-time analysis of which files to keep after simplification |
| `IMPLEMENTATION_STATUS.md` | Status snapshot of the simplified cost optimization migration |
| `MATPLOTLIB_FIX.md` | Bug fix log — matplotlib dependency removed |
| `NOTAL_RAG_SYSTEM_UPDATE_SUMMARY.md` | RAG pipeline update summary (brand rename session) |
| `TRAINING_IMPLEMENTATION_SUMMARY.md` | Session summary for the LLM training system build |
| `WEBAPP_INTEGRATION_STATUS.md` | One-time compatibility check — no longer relevant |
