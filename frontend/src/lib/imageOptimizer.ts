// src/lib/imageOptimizer.ts

interface CompressionOptions {
  quality?: number;      // Calidad de compresión (0.1 a 1.0)
  maxDimension?: number; // Resolución máxima permitida (ej. 2560px para 2K/4K web)
}

/**
 * Convierte y comprime una imagen a formato WebP usando la API nativa de Canvas.
 * Ignora GIFs animados y SVGs para mantener sus propiedades nativas.
 */
export async function compressImageToWebP(
  file: File,
  options: CompressionOptions = {}
): Promise<File> {
  const { quality = 0.82, maxDimension = 2560 } = options;

  // Si no es imagen o es SVG/GIF, retornar el archivo original intacto
  if (!file.type.startsWith("image/") || file.type === "image/svg+xml" || file.type === "image/gif") {
    return file;
  }

  return new Promise((resolve) => {
    const img = new Image();
    const objectUrl = URL.createObjectURL(file);

    img.onload = () => {
      URL.revokeObjectURL(objectUrl);

      let { width, height } = img;

      // Redimensionar proporcionalmente si excede la dimensión máxima
      if (width > maxDimension || height > maxDimension) {
        if (width > height) {
          height = Math.round((height * maxDimension) / width);
          width = maxDimension;
        } else {
          width = Math.round((width * maxDimension) / height);
          height = maxDimension;
        }
      }

      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;

      const ctx = canvas.getContext("2d");
      if (!ctx) {
        resolve(file); // Fallback al archivo original en caso de error
        return;
      }

      ctx.drawImage(img, 0, 0, width, height);

      canvas.toBlob(
        (blob) => {
          if (!blob) {
            resolve(file);
            return;
          }

          // Construir nuevo nombre con extensión .webp
          const newFileName = file.name.replace(/\.[^/.]+$/, "") + ".webp";
          const optimizedFile = new File([blob], newFileName, {
            type: "image/webp",
            lastModified: Date.now(),
          });

          // Solo usar la versión optimizada si efectivamente es más liviana
          if (optimizedFile.size < file.size) {
            resolve(optimizedFile);
          } else {
            resolve(file);
          }
        },
        "image/webp",
        quality
      );
    };

    img.onerror = () => {
      URL.revokeObjectURL(objectUrl);
      resolve(file); // Fallback al archivo original en caso de error de lectura
    };

    img.src = objectUrl;
  });
}