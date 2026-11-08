using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using UndergroundVandals.Api.DTOs;

namespace UndergroundVandals.Api.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public AzureBlobStorageService(IOptions<AzureStorageSettings> config)
    {
        _blobServiceClient = new BlobServiceClient(config.Value.ConnectionString);
    }

    public async Task<FileUploadResult> UploadImageAsync(IFormFile file, string containerName, string folderName)
    {
        return await UploadFileToBlobAsync(file, containerName, folderName);
    }

    public async Task<FileUploadResult> UploadVideoAsync(IFormFile file, string containerName, string folderName)
    {
        return await UploadFileToBlobAsync(file, containerName, folderName);
    }

    private async Task<FileUploadResult> UploadFileToBlobAsync(IFormFile file, string containerName, string folderName)
    {
        if (file == null || file.Length == 0)
            return new FileUploadResult { Success = false, Error = "The file is empty." };

        if (string.IsNullOrWhiteSpace(containerName))
            return new FileUploadResult { Success = false, Error = "Container name must be provided." };

        try
        {
            var formattedContainer = containerName.ToLower().Trim();
            var containerClient = _blobServiceClient.GetBlobContainerClient(formattedContainer);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobPath = string.IsNullOrWhiteSpace(folderName)
                ? $"{Guid.NewGuid()}_{file.FileName}"
                : $"{folderName.Trim('/')}/{Guid.NewGuid()}_{file.FileName}";

            var blobClient = containerClient.GetBlobClient(blobPath);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

            return new FileUploadResult
            {
                Success = true,
                Url = blobClient.Uri.ToString(),
                PublicId = $"{formattedContainer}/{blobPath}"
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Removes a blob and all its snapshots from storage using its combined publicId (e.g., "archive-media/photos/guid").
    /// </summary>
    public async Task<bool> DeleteFileAsync(string publicId, string resourceType = "image")
    {
        if (string.IsNullOrWhiteSpace(publicId)) return false;

        var parts = publicId.Split('/', 2);
        if (parts.Length < 2) return false;

        var containerClient = _blobServiceClient.GetBlobContainerClient(parts[0]);
        var blobClient = containerClient.GetBlobClient(parts[1]);

        // DeleteSnapshotsOption.IncludeSnapshots asegura que borre todo aunque existan snapshots
        return await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
    }

    /// <summary>
    /// Generates a SAS URL for direct client-side (Frontend) uploads.
    /// targetPath format expected: "container-name/folder-name" (e.g., "archive-media/photos").
    /// </summary>
    public async Task<Dictionary<string, object>> GenerateUploadParametersAsync(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be empty", nameof(targetPath));

        var pathParts = targetPath.Trim('/').Split('/', 2);
        var containerName = pathParts[0].ToLower();
        var folderPrefix = pathParts.Length > 1 ? $"{pathParts[1].Trim('/')}/" : string.Empty;

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Eliminado .bin para evitar distorsión de extensiones en Azure Storage
        var blobName = $"{folderPrefix}{Guid.NewGuid()}";
        var blobClient = containerClient.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return new Dictionary<string, object>
        {
            { "uploadUrl", sasUri.ToString() },
            { "fileUrl", blobClient.Uri.ToString() },
            { "publicId", $"{containerName}/{blobName}" },
            { "provider", "AzureBlob" }
        };
    }

    /// <summary>
    /// Helper to convert a full Blob URL back into a valid PublicId for deletion.
    /// Example: "https://uvmediamanager.blob.core.windows.net/archive-media/thumbnails/guid_poster.webp" 
    /// => "archive-media/thumbnails/guid_poster.webp"
    /// </summary>
    public string ExtractPublicIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var uri = new Uri(url);
        return uri.AbsolutePath.TrimStart('/');
    }
}