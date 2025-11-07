using System;

namespace Certio.Application.Configuration;

public sealed class DocumentIntegrationOptions
{
    public ProviderOptions GoogleDrive { get; set; } = new();

    public ProviderOptions OneDrive { get; set; } = new();

    public sealed class ProviderOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public TimeSpan TokenExpirySafetyWindow { get; set; } = TimeSpan.FromMinutes(5);
    }
}

