namespace Certio.Application.Configuration
{
    /// <summary>
    /// Shared caps for list-style queries that previously had no upper bound
    /// (e.g. matter lists, billing lists, audit lists). These are a safety net,
    /// not real pagination: they stop a single large tenant from turning a list
    /// endpoint into an unbounded table scan / huge JSON payload while the UI and
    /// API contracts are migrated to proper Skip/Take pagination in a later phase.
    /// </summary>
    public static class QueryLimits
    {
        /// <summary>
        /// Default hard cap for interactive list endpoints (dashboards, index pages).
        /// </summary>
        public const int DefaultMaxResults = 500;
    }
}
