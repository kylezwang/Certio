using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class DocumentEmbedService : IDocumentEmbedService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DocumentEmbedService> _logger;

    public DocumentEmbedService(ApplicationDbContext dbContext, ILogger<DocumentEmbedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> CreateEmbedUrlAsync(Guid documentId, Guid orgId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OrgId == orgId, cancellationToken)
                       ?? throw new InvalidOperationException("Document not found");

        var baseUrl = document.EmbedUrl ?? document.PreviewUrl ?? document.DownloadUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Document does not have an embeddable URL configured.");
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var separator = baseUrl.Contains('?') ? '&' : '?';
        var embedUrl = $"{baseUrl}{separator}embedToken={token}&requestedBy={requestingUserId}";

        _logger.LogInformation("Generated embed URL for document {DocumentId} by user {UserId}", documentId, requestingUserId);

        return embedUrl;
    }
}

