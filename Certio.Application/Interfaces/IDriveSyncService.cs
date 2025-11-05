using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IDriveSyncService
{
    Task SyncGoogleDriveAsync(Guid orgId, Guid userId, CancellationToken cancellationToken = default);

    Task SyncOneDriveAsync(Guid orgId, Guid userId, CancellationToken cancellationToken = default);

    Task<Guid> UpsertMetadataAsync(ProviderFileMetadata metadata, CancellationToken cancellationToken = default);
}
