import { beforeEach, describe, expect, it, vi } from 'vitest';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import { createBoardApi } from './boardApi';
import { getBinary } from './http';

vi.mock('./http', () => ({
  deleteJson: vi.fn(),
  getBinary: vi.fn(),
  getEnvelope: vi.fn(),
  patchData: vi.fn(),
  postData: vi.fn(),
  putData: vi.fn()
}));

describe('boardApi exportBoard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns binary export payload and server-provided filename', async () => {
    const blob = new Blob(['zip'], { type: 'application/zip' });
    vi.mocked(getBinary).mockResolvedValue(
      ok({
        blob,
        fileName: 'BoardOil.boardoil.zip',
        contentType: 'application/zip'
      })
    );

    const api = createBoardApi();
    const result = await api.exportBoard(1);

    expect(result.ok).toBe(true);
    if (!result.ok) {
      throw new Error('Expected success result.');
    }

    expect(result.data.fileName).toBe('BoardOil.boardoil.zip');
    expect(result.data.contentType).toBe('application/zip');
    expect(result.data.blob).toBe(blob);
  });

  it('propagates export API errors', async () => {
    const apiError: AppError = {
      kind: 'http',
      message: 'Forbidden',
      statusCode: 403
    };
    vi.mocked(getBinary).mockResolvedValue(err(apiError));

    const api = createBoardApi();
    const result = await api.exportBoard(7);

    expect(result.ok).toBe(false);
    if (result.ok) {
      throw new Error('Expected error result.');
    }

    expect(result.error).toEqual(apiError);
  });
});
