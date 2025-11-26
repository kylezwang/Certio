namespace Certio.Application.Configuration;

public class FileStorageOptions
{
    public string LocalStoragePath { get; set; } = "uploads";
    public bool UseAzureBlobStorage { get; set; } = false;
    public string? AzureBlobConnectionString { get; set; }
    public string? AzureBlobContainerName { get; set; }
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
}

