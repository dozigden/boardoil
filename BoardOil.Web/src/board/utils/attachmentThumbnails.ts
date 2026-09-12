export const attachmentThumbnailEdgeLength = 200;

export function attachmentThumbnailDimensions(width: number, height: number, maximum = attachmentThumbnailEdgeLength) {
  if (width <= 0 || height <= 0) { throw new Error('Image dimensions must be positive.'); }
  const scale = Math.min(1, maximum / width, maximum / height);
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

export async function createAttachmentThumbnail(source: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(source);
  try {
    const dimensions = attachmentThumbnailDimensions(bitmap.width, bitmap.height);
    const canvas = document.createElement('canvas');
    canvas.width = dimensions.width;
    canvas.height = dimensions.height;
    const context = canvas.getContext('2d');
    if (!context) { throw new Error('Canvas is unavailable.'); }
    context.drawImage(bitmap, 0, 0, dimensions.width, dimensions.height);
    return await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(blob => {
        if (blob) { resolve(blob); }
        else { reject(new Error('Thumbnail encoding failed.')); }
      }, 'image/png');
    });
  } finally { bitmap.close(); }
}

export function createAttachmentThumbnailQueue() {
  const pending = new Map<string, Promise<boolean>>();
  let tail = Promise.resolve();

  function run(key: string, work: () => Promise<boolean>) {
    const existing = pending.get(key);
    if (existing) { return existing; }
    const task = tail.catch(() => undefined).then(work);
    tail = task.then(() => undefined, () => undefined);
    pending.set(key, task);
    void task.then(() => pending.delete(key), () => pending.delete(key));
    return task;
  }

  return { run };
}

export const attachmentThumbnailQueue = createAttachmentThumbnailQueue();
