using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

/// <summary>
/// Service for building comprehensive user data context for AI RAG system
/// Provides AI with access to all user-scoped data across all modules
/// </summary>
public interface IUserDataContextService
{
    /// <summary>
    /// Build comprehensive context from all available user data
    /// </summary>
    Task<UserDataContextResult> BuildUserDataContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get summary of available data for a user (for AI awareness)
    /// </summary>
    Task<Dictionary<string, object>> GetUserDataSummaryAsync(
        int userId, 
        int organizationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync user data context to Python AI service
    /// </summary>
    Task SyncUserDataToPythonAsync(
        int userId, 
        int organizationId, 
        CancellationToken cancellationToken = default);
}

