using System.Threading;
using System.Threading.Tasks;

namespace Certio.Application.Interfaces
{
    public interface ICalendarSyncService
    {
        Task SyncGoogleCalendarAsync(int orgId, int userId, CancellationToken cancellationToken);
        Task SyncOutlookCalendarAsync(int orgId, int userId, CancellationToken cancellationToken);
        Task RefreshTokenIfNeededAsync(int integrationId, CancellationToken cancellationToken);
    }
}

