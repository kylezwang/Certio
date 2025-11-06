using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Certio.Domain.Documents;

namespace Certio.Application.Interfaces;

public interface IDocumentContentService
{
    Task<DocumentContentResult> FetchContentAsync(Document document, Guid? versionId = default, CancellationToken cancellationToken = default);
}

public sealed record DocumentContentResult(string Content, bool IsPartial, IReadOnlyDictionary<string, string?> AdditionalMetadata)
{
    public bool HasContent => !string.IsNullOrWhiteSpace(Content);

    public static DocumentContentResult Empty(string reason) => new(string.Empty, false,
        new Dictionary<string, string?>
        {
            { "extractionStatus", reason }
        });
}

