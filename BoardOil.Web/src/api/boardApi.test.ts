import { beforeEach, describe, expect, it, vi } from 'vitest';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import { createBoardApi } from './boardApi';
import { getBinary, postFormData } from './http';

vi.mock('./http', () => ({
  deleteJson: vi.fn(),
  getBinary: vi.fn(),
  getEnvelope: vi.fn(),
  patchData: vi.fn(),
  postData: vi.fn(),
  postFormData: vi.fn(),
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

describe('boardApi importBoardPackage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('posts multipart form data with file and optional name override', async () => {
    const board = {
      id: 42,
      name: 'Imported Board',
      createdAtUtc: '2026-04-08T00:00:00Z',
      updatedAtUtc: '2026-04-08T00:00:00Z',
      currentUserRole: 'Owner',
      columns: []
    };
    vi.mocked(postFormData).mockResolvedValue(ok(board));
    const file = new File(['zip-data'], 'board.boardoil.zip', { type: 'application/zip' });

    const api = createBoardApi();
    const result = await api.importBoardPackage(file, 'Renamed Board');

    expect(result.ok).toBe(true);
    expect(postFormData).toHaveBeenCalledTimes(1);
    expect(vi.mocked(postFormData).mock.calls[0]?.[0]).toBe('/api/boards/import');
    const payload = vi.mocked(postFormData).mock.calls[0]?.[1];
    expect(payload).toBeInstanceOf(FormData);
    expect(payload?.get('name')).toBe('Renamed Board');
    const uploadedFile = payload?.get('file');
    expect(uploadedFile).toBeInstanceOf(File);
    expect((uploadedFile as File).name).toBe('board.boardoil.zip');
  });
});
