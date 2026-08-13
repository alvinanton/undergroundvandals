using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using UndergroundVandals.Api.DTOs;

namespace UndergroundVandals.Api.Services;

public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(IOptions<CloudinarySettings> config)
    {
        var acc = new Account(
            config.Value.CloudName,
            config.Value.ApiKey,
            config.Value.ApiSecret
        );

        _cloudinary = new Cloudinary(acc);
    }

    public async Task<FileUploadResult> UploadImageAsync(IFormFile file)
    {
        if (file.Length == 0)
            return new FileUploadResult { Success = false, Error = "El archivo está vacío." };

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "underground_vandals/photos",
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

    public async Task<FileUploadResult> UploadVideoAsync(IFormFile file)
    {
        if (file.Length == 0)
            return new FileUploadResult { Success = false, Error = "El archivo está vacío." };

        using var stream = file.OpenReadStream();
        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "underground_vandals/videos"
        };

        var result = await _cloudinary.UploadLargeAsync(uploadParams);

        if (result.Error != null)
            return new FileUploadResult { Success = false, Error = result.Error.Message };

        return new FileUploadResult
        {
            Success = true,
            Url = result.SecureUrl.ToString(),
            PublicId = result.PublicId
        };
    }

    public async Task<bool> DeleteFileAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);
        return result.Result == "ok";
    }
}