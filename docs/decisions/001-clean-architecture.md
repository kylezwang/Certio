# Clean Architecture split

The domain model does not reference EF Core or ASP.NET. Infrastructure references Domain. The web project references all of the others and is the composition root.

That split is why a permission rule or an entity change can be tested without starting the site. It is also why some "application" logic still sits in `Certio.Web/Services`: those types need `HttpContext`, SignalR, or the cache, and moving them would be a large mechanical change for little gain.

The cost is four projects and a lot of interfaces. A new feature usually means a domain type, a service, and a controller action, not a single file.
