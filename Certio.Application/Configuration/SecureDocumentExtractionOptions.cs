using System;

namespace Certio.Application.Configuration;

public sealed class SecureDocumentExtractionOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "prebuilt-read";

    public int MaxDocumentSizeMb { get; set; } = 40;

    public int MaxCharacters { get; set; } = 120_000;

    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public string[] AllowedContentTypes { get; set; } =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/msword",
        "text/plain"
    };
}

