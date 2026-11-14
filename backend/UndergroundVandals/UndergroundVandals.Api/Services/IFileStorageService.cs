using Microsoft.AspNetCore.Http;
using UndergroundVandals.Api.DTOs;

namespace UndergroundVandals.Api.Services;

public interface IFileStorageService
{
    // Obliga a especificar el contenedor (ej: "archive-media") y la carpeta (ej: "photos")
    Task<FileUploadResult> UploadImageAsync(IFormFile file, string containerName, string folderName);
    Task<FileUploadResult> UploadVideoAsync(IFormFile file, string containerName, string folderName);

    // Eliminación por publicId (ej: "archive-media/photos/guid.jpg")
    Task<bool> DeleteFileAsync(string publicId, string resourceType = "image");

    // Genera firma SAS pasando la ruta completa (ej: "archive-media/photos" o "gallery-media/articles")
    Task<Dictionary<string, object>> GenerateUploadParametersAsync(string targetPath);
}