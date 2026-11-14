using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using UndergroundVandals.Api.DTOs;

namespace UndergroundVandals.Api.Services;

public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly string _cloudName;
    private readonly string _apiKey;

    public CloudinaryStorageService(IOptions<CloudinarySettings> config)
    {
        _cloudName = config.Value.CloudName;
        _apiKey = config.Value.ApiKey;

        var acc = new Account(
            _cloudName,
            _apiKey,
            config.Value.ApiSecret
        );

        _cloudinary = new Cloudinary(acc);
    }

    public async Task<FileUploadResult> UploadImageAsync(IFormFile file, string containerName, string folderName)
    {
        if (file.Length == 0)
            return new FileUploadResult { Success = false, Error = "The file is empty." };

        using var stream = file.OpenReadStream();
        var destinationFolder = $"{containerName}/{folderName}".Trim('/');

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = destinationFolder,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            return new FileUploadResult { Success = false, Error = result.Error.Message };

        return new FileUploadResult
        {
            Success = true,
            Url = result.SecureUrl.ToString(),
            PublicId = result.PublicId
        };
    }

    public async Task<FileUploadResult> UploadVideoAsync(IFormFile file, string containerName, string folderName)
    {
        if (file.Length == 0)
            return new FileUploadResult { Success = false, Error = "The file is empty." };

        using var stream = file.OpenReadStream();
        var destinationFolder = $"{containerName}/{folderName}".Trim('/');

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = destinationFolder
        };

        const long tenMegaBytes = 10 * 1024 * 1024;
        VideoUploadResult result;

        if (file.Length > tenMegaBytes)
        {
            result = await _cloudinary.UploadLargeAsync(uploadParams);
        }
        else
        {
            result = await _cloudinary.UploadAsync(uploadParams);
        }

        if (result.Error != null)
            return new FileUploadResult { Success = false, Error = result.Error.Message };

        return new FileUploadResult
        {
            Success = true,
            Url = result.SecureUrl.ToString(),
            PublicId = result.PublicId
        };
    }

    public async Task<bool> DeleteFileAsync(string publicId, string resourceType = "image")
    {
        var type = resourceType.ToLower() switch
        {
            "video" => ResourceType.Video,
            "raw" => ResourceType.Raw,
            _ => ResourceType.Image
        };

        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = type
        };

        var result = await _cloudinary.DestroyAsync(deleteParams);
        return result.Result == "ok";
    }

    public async Task <Dictionary<string, object>> GenerateUploadParametersAsync(string targetPath)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parameters = new SortedDictionary<string, object>
        {
            { "timestamp", timestamp },
            { "folder", targetPath }
        };

        var signature = _cloudinary.Api.SignParameters(parameters);

        return new Dictionary<string, object>
        {
            { "timestamp", timestamp },
            { "signature", signature },
            { "cloudName", _cloudName },
            { "apiKey", _apiKey },
            { "folder", targetPath }
        };
    }
}