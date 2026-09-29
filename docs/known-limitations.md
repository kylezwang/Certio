# Known limitations

These are real, and they are left alone on purpose. Splitting them is a behavior change, not a cleanup.

## Large UI files

Several views and scripts grew by accretion:

- `Views/Matter/_MatterTasks.cshtml` and `Views/Tasks/Index.cshtml` are each several thousand lines, with markup and script in the same file.
- `Views/Shared/_ClientLayout.cshtml` is the shell for most signed-in pages and is similarly large.
- `wwwroot/js/chat.js` owns streaming, layout, and agent-action parsing.

A change in one of those files is easy to get wrong because the same behavior is sometimes copied in two places (the task page and the matter task tab).

## Controllers

`HomeController`, `ClientController`, and `MatterController` are well over a thousand lines. Newer code goes through services. Older actions still query `ApplicationDbContext` directly.

## UpdatesHub

`UpdatesHub` is an empty hub with `[Authorize]`. Nothing sends on it. It is registered so the endpoint exists.

## Cache and scale

With Redis off, cache and SignalR are process-local. That is the laptop default. Production should set `USE_REDIS` before running more than one instance. See `Program.cs` where the backplane is added.

Permission cache entries live for several minutes. A role change can look ignored until the key expires or is removed.

## Tests

The .NET suite is small relative to the number of screens. View markup and the Python prompt text are not covered. A green `dotnet test` does not mean a Razor change is safe.

## Names

The product is Notal. Projects, namespaces, the SQL container, and the Python modules are still `Certio`. Renaming them would touch every file and the Azure app settings that point at this repo. The names were left as they are.
