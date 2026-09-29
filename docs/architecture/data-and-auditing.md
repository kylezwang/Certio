# Data and auditing

## Database choice

`Program.cs` picks the connection at startup.

- `USE_AZURE_SQL=true` uses `AZURE_SQL_CONNECTION_STRING`, or builds one from `DB_PASSWORD`.
- Otherwise it uses local SQL Server from Docker. `SQL_PASSWORD` is the `sa` password in `docker-compose.yml`.

EF Core migrations live in `Certio.Infrastructure` and are applied with the web project as the startup project:

```
dotnet ef database update --project Certio.Infrastructure --startup-project Certio.Web
```

Do not hand-edit migration snapshots to tidy comments. They are generated.

## Soft delete

Many entities have `DeletedAt`. Reads are supposed to ignore rows where that is set. The global filters are on the `DbContext`. A new entity that should disappear when "deleted" needs the same filter, or it will keep showing up in lists.

## Audit

`AuditInterceptor` runs inside `SaveChanges`. Before the save it records old and new values. It also stamps `CreatedById`, `ModifiedById`, and delete fields from the current HTTP user when there is one. The audit rows go into `AuditLog` in the same transaction as the change.

Background jobs have no HTTP user, so those saves will not have a browser user id. That is expected.
