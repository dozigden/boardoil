export function formatAttachmentSize(bytes: number) {
  if (bytes < 1024) { return `${bytes} B`; }
  if (bytes < 1024 * 1024) { return `${(bytes / 1024).toFixed(1)} KiB`; }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MiB`;
}
