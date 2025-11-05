using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IDocumentAuditService
{
    Task LogAsync(DocumentAuditEvent auditEvent, CancellationToken cancellationToken = default);
}
