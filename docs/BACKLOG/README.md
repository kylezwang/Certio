# Engineering Backlog

Deferred work from the July 2026 architecture review. These items are **known limitations / technical debt**, tracked here rather than left as open "phases" in the architecture docs.

**Source review:** [`../architecture/ARCHITECTURE_REVIEW_2026.md`](../architecture/ARCHITECTURE_REVIEW_2026.md)  
**Completed work:** [`../architecture/PHASE_1_SCALABILITY_FIXES.md`](../architecture/PHASE_1_SCALABILITY_FIXES.md)

## Contents

| Document | Description |
|----------|-------------|
| [`SCALABILITY_AND_ARCHITECTURE.md`](SCALABILITY_AND_ARCHITECTURE.md) | Remaining scalability, multi-instance, pagination, and structural cleanup items |
| [`AI_TERMINOLOGY_CLEANUP_AND_RUNTIME_ISSUES.md`](AI_TERMINOLOGY_CLEANUP_AND_RUNTIME_ISSUES.md) | Record of the "Matter"/legal AI-terminology cleanup, plus dashboard-card latency, streaming-timeout, and Google Drive retry issues found in server logs |

## How to use this folder

- Treat items as **backlog**, not committed roadmap.
- Prefer pulling work when a trigger is hit (multi-instance deploy, truncation warnings, large tenants) rather than doing everything "because Phase 2 exists."
- When an item ships, move or archive its section and update the architecture review status.
