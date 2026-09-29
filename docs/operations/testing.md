# Testing

.NET tests:

```
dotnet test
```

They use xUnit, Moq, and the EF Core in-memory provider. They do not need SQL Server or Redis. The suite covers services, a few controllers, hub authorization, and permission checks. It is not a full pass over the Razor views.

Python tests need a dummy model key so `main.py` can import:

```
set OPENAI_API_KEY=sk-test-placeholder
set AI_API_KEY=test-secret
cd ai_agents
python -m pytest tests -q
```

`scripts/test_performance.ps1` hits a running site and prints timings. It is not part of CI.

The text check:

```
python scripts/check_text_hygiene.py
```
