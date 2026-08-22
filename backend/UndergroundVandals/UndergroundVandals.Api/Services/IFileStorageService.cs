using UndergroundVandals.Api.DTOs;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadImageAsync(IFormFile file);
    Task<FileUploadResult> UploadVideoAsync(IFormFile file);
    Task<bool> DeleteFileAsync(string publicId);

    // Generates secure cryptographic parameters for client-side direct uploads
    Dictionary<string, object> GenerateUploadParameters(string folderName);
}