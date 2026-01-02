using Azure.Storage.Blobs;

namespace Metrics.Engine;

public interface IStorageProvider
{
    Task SaveCsvToLocalAsync(string filePath, string content);
    Task SaveCsvToBlobAsync(string connectionString, string container, string blobPath, string content);
    Task SaveLogsToLocalAsync(string filePath, string content);
    Task SaveLogsToBlobAsync(string connectionString, string container, string blobPath, string content);
}

public sealed class StorageProvider : IStorageProvider
{
    public async Task SaveCsvToLocalAsync(string filePath, string content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task SaveCsvToBlobAsync(string connectionString, string container, string blobPath, string content)
    {
        try
        {
            var blobClient = new BlobClient(
                new Uri($"https://{ExtractAccountName(connectionString)}.blob.core.windows.net/{container}/{blobPath}"),
                new Azure.Storage.StorageSharedKeyCredential(
                    ExtractAccountName(connectionString),
                    ExtractAccountKey(connectionString)
                )
            );

            await blobClient.UploadAsync(new BinaryData(content), overwrite: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save CSV to blob storage: {ex.Message}", ex);
        }
    }

    public async Task SaveLogsToLocalAsync(string filePath, string content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task SaveLogsToBlobAsync(string connectionString, string container, string blobPath, string content)
    {
        try
        {
            var blobClient = new BlobClient(
                new Uri($"https://{ExtractAccountName(connectionString)}.blob.core.windows.net/{container}/{blobPath}"),
                new Azure.Storage.StorageSharedKeyCredential(
                    ExtractAccountName(connectionString),
                    ExtractAccountKey(connectionString)
                )
            );

            await blobClient.UploadAsync(new BinaryData(content), overwrite: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save logs to blob storage: {ex.Message}", ex);
        }
    }

    private static string ExtractAccountName(string connectionString)
    {
        var parts = connectionString.Split(';');
        var accountNamePart = parts.FirstOrDefault(p => p.StartsWith("AccountName="));
        return accountNamePart?.Substring("AccountName=".Length) ?? "storageaccount";
    }

    private static string ExtractAccountKey(string connectionString)
    {
        var parts = connectionString.Split(';');
        var accountKeyPart = parts.FirstOrDefault(p => p.StartsWith("AccountKey="));
        return accountKeyPart?.Substring("AccountKey=".Length) ?? "";
    }
}
