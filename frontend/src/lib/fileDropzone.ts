interface FileDropzoneOptions {
  feedback?: HTMLElement | null;
  uploadOnDrop?: boolean;
  nextStep?: "submit" | "button";
}

export function attachFileDropzone(
  dropzone: HTMLElement | null,
  input: HTMLInputElement | null,
  { feedback = null, uploadOnDrop = false, nextStep = "button" }: FileDropzoneOptions = {},
): void {
  if (!dropzone || !input) return;

  const imageExtensions = new Set([".avif", ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".svg", ".tif", ".tiff", ".webp"]);
  const videoExtensions = new Set([".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".webm"]);
  const acceptedByInput = (file: File) => {
    const rules = input.accept.split(",").map((rule) => rule.trim().toLowerCase()).filter(Boolean);
    if (rules.length === 0) return true;
    const extension = file.name.toLowerCase().match(/\.[^.]+$/)?.[0] || "";
    return rules.some((rule) =>
      rule.startsWith(".")
        ? extension === rule
        : rule.endsWith("/*")
          ? file.type.startsWith(rule.slice(0, -1)) || (rule === "image/*" && imageExtensions.has(extension)) || (rule === "video/*" && videoExtensions.has(extension))
          : file.type.toLowerCase() === rule,
    );
  };

  const resetHighlight = () => {
    dropzone.classList.remove("border-orange-500", "bg-orange-500/10");
    dropzone.classList.add("border-zinc-700", "bg-zinc-950/70");
  };

  dropzone.addEventListener("dragenter", (event) => {
    event.preventDefault();
    dropzone.classList.add("border-orange-500", "bg-orange-500/10");
    dropzone.classList.remove("border-zinc-700", "bg-zinc-950/70");
  });
  dropzone.addEventListener("dragover", (event) => {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
  });
  dropzone.addEventListener("dragleave", (event) => {
    if (event.relatedTarget instanceof Node && dropzone.contains(event.relatedTarget)) return;
    resetHighlight();
  });
  dropzone.addEventListener("drop", (event) => {
    event.preventDefault();
    resetHighlight();

    const droppedFiles = Array.from(event.dataTransfer?.files || []);
    if (!droppedFiles.length) return;

    const accepted = droppedFiles.filter(acceptedByInput);
    const selected = input.multiple ? accepted : accepted.slice(0, 1);
    const skipped = droppedFiles.length - selected.length;

    if (!selected.length) {
      if (feedback) {
        feedback.textContent = "These files are not supported here. Choose a supported file type.";
        feedback.className = "mt-2 text-xs text-red-300";
      }
      return;
    }

    const transfer = new DataTransfer();
    selected.forEach((file) => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event("change", { bubbles: true }));

    if (feedback) {
      const selectedText = `${selected.length} file${selected.length === 1 ? "" : "s"} ${uploadOnDrop ? "received" : "selected"}${uploadOnDrop ? "; upload starting." : nextStep === "submit" ? ". Save the form to start the upload." : ". Select Upload to continue."}`;
      const skippedText = skipped ? ` ${skipped} unsupported or extra file${skipped === 1 ? " was" : "s were"} skipped.` : "";
      feedback.textContent = `${selectedText}${skippedText}`;
      feedback.className = skipped ? "mt-2 text-xs text-amber-300" : "mt-2 text-xs text-zinc-400";
    }
  });
}
