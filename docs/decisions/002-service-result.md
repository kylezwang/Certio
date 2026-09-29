# ServiceResult

Application services return `ServiceResult` or `ServiceResult<T>` instead of throwing for expected failures (validation, not found, permission denied).

The result carries `Success`, `ErrorMessage`, `ErrorCode`, and optional `ValidationErrors`. Controllers turn that into a view message or a JSON error. Unexpected bugs still throw and land in the normal exception middleware.

The pattern is in `Certio.Application/DTOs/ServiceResult.cs`. Not every service uses it yet. Some older actions still return `null` or set `TempData` themselves.
