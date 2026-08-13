using UndergroundVandals.Api.DTOs;

namespace UndergroundVandals.Api.Services;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadImageAsync(IFormFile file);
    Task<FileUploadResult> UploadVideoAsync(IFormFile file);
    Task<bool> DeleteFileAsync(string publicId);
}