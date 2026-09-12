import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  attachmentThumbnailDimensions,
  createAttachmentThumbnail,
  createAttachmentThumbnailQueue
} from './attachmentThumbnails';

describe('attachment thumbnails', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each([
    [40, 20, 40, 20],
    [400, 200, 200, 100],
    [200, 400, 100, 200]
  ])('fits %sx%s without upscaling', (width, height, expectedWidth, expectedHeight) => {
    expect(attachmentThumbnailDimensions(width, height)).toEqual({ width: expectedWidth, height: expectedHeight });
  });

  it('draws one decoded frame to a PNG canvas and closes the bitmap', async () => {
    const source = new Blob(['image'], { type: 'image/gif' });
    const close = vi.fn();
    const bitmap = { width: 400, height: 200, close } as unknown as ImageBitmap;
    const decode = vi.fn().mockResolvedValue(bitmap);
    vi.stubGlobal('createImageBitmap', decode);
    const drawImage = vi.fn();
    const output = new Blob(['thumbnail'], { type: 'image/png' });
    const canvas = {
      width: 0,
      height: 0,
      getContext: () => ({ drawImage }),
      toBlob: (callback: BlobCallback, type?: string) => {
        expect(type).toBe('image/png');
        callback(output);
      }
    } as unknown as HTMLCanvasElement;
    const createElement = vi.fn().mockReturnValue(canvas);
    vi.stubGlobal('document', { createElement });

    const result = await createAttachmentThumbnail(source);

    expect(result).toBe(output);
    expect(decode).toHaveBeenCalledWith(source);
    expect(canvas.width).toBe(200);
    expect(canvas.height).toBe(100);
    expect(drawImage).toHaveBeenCalledWith(bitmap, 0, 0, 200, 100);
    expect(close).toHaveBeenCalledOnce();
    expect(createElement).toHaveBeenCalledWith('canvas');
  });

  it('deduplicates matching work and runs different backfills sequentially', async () => {
    const queue = createAttachmentThumbnailQueue();
    const order: string[] = [];
    let releaseFirst!: () => void;
    let markFirstStarted!: () => void;
    const firstGate = new Promise<void>(resolve => { releaseFirst = resolve; });
    const firstStarted = new Promise<void>(resolve => { markFirstStarted = resolve; });
    const first = queue.run('1', async () => {
      order.push('first-start');
      markFirstStarted();
      await firstGate;
      order.push('first-end');
      return true;
    });
    const duplicate = queue.run('1', async () => false);
    const second = queue.run('2', async () => {
      order.push('second');
      return true;
    });
    await firstStarted;

    expect(duplicate).toBe(first);
    expect(order).toEqual(['first-start']);
    releaseFirst();
    await Promise.all([first, second]);
    expect(order).toEqual(['first-start', 'first-end', 'second']);
  });
});
