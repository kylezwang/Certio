using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certio.Application.Services.Documents;

public interface IFileStorageService
{
    Task<string> StoreFileAsync(Stream fileStream, Guid documentId, string fileName, CancellationToken cancellationToken = default);
    Task<byte[]> RetrieveFileAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
    string GetStorageDirectory(Guid orgId);
}

public class FileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IOptions<FileStorageOptions> options,
        ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> StoreFileAsync(Stream fileStream, Guid documentId, string fileName, CancellationToken cancellationToken = default)
    {
        if (fileStream == null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(_options.LocalStoragePath))
        {
            throw new InvalidOperationException("File storage path not configured");
        }

        try
        {
            // Create storage directory structure: {basePath}/{year}/{month}/{documentId}/
            var now = DateTime.UtcNow;
            var relativePath = Path.Combine(
                now.Year.ToString(),
                now.Month.ToString("D2"),
                documentId.ToString());

            var fullDirectory = Path.Combine(_options.LocalStoragePath, relativePath);
            Directory.CreateDirectory(fullDirectory);

            // Sanitize filename and add document ID prefix for uniqueness
            var sanitizedFileName = SanitizeFileName(fileName);
            var storedFileName = $"{documentId}_{sanitizedFileName}";
            var fullPath = Path.Combine(fullDirectory, storedFileName);

            // Store the file
            using (var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await fileStream.CopyToAsync(fileStreamOut, cancellationToken);
            }

            // Return relative path for database storage
            var storagePath = Path.Combine(relativePath, storedFileName);
            _logger.LogInformation("Stored file for document {DocumentId} at {Path}", documentId, storagePath);

            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store file for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<byte[]> RetrieveFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));
        }

        if (string.IsNullOrWhiteSpace(_options.LocalStoragePath))
        {
            throw new InvalidOperationException("File storage path not configured");
        }

        try
        {
            var fullPath = Path.Combine(_options.LocalStoragePath, storagePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found at path: {Path}", storagePath);
                throw new FileNotFoundException($"File not found: {storagePath}");
            }

            return await File.ReadAllBytesAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Failed to retrieve file from {Path}", storagePath);
            throw;
        }
    }

    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(_options.LocalStoragePath))
        {
            return Task.FromResult(false);
        }

        var fullPath = Path.Combine(_options.LocalStoragePath, storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(_options.LocalStoragePath))
        {
            _logger.LogWarning("File storage path not configured, cannot delete");
            return Task.FromResult(false);
        }

        try
        {
            var fullPath = Path.Combine(_options.LocalStoragePath, storagePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogDebug("File already deleted or doesn't exist: {Path}", storagePath);
                return Task.FromResult(true);
            }

            File.Delete(fullPath);
            _logger.LogInformation("Deleted file at {Path}", storagePath);

            // Try to clean up empty directories
            CleanupEmptyDirectories(Path.GetDirectoryName(fullPath));

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file at {Path}", storagePath);
            return Task.FromResult(false);
        }
    }

    public string GetStorageDirectory(Guid orgId)
    {
        if (string.IsNullOrWhiteSpace(_options.LocalStoragePath))
        {
            throw new InvalidOperationException("File storage path not configured");
        }

        return Path.Combine(_options.LocalStoragePath, "orgs", orgId.ToString());
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Limit length to prevent filesystem issues
        const int maxLength = 200;
        if (sanitized.Length > maxLength)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = nameWithoutExt.Substring(0, maxLength - extension.Length) + extension;
        }

        return sanitized;
    }

    private void CleanupEmptyDirectories(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            // Only clean up within our storage path
            if (!directoryPath.StartsWith(_options.LocalStoragePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            while (!string.IsNullOrWhiteSpace(directoryPath) && 
                   directoryPath != _options.LocalStoragePath &&
                   Directory.Exists(directoryPath) &&
                   !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
                directoryPath = Path.GetDirectoryName(directoryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cleanup empty directories");
        }
    }
}

