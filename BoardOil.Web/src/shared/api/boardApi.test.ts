import { beforeEach, describe, expect, it, vi } from 'vitest';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import { createBoardApi } from './boardApi';
import { getBinary, getEnvelope, postData, postFormData, postJson, putData } from './http';

vi.mock('./http', () => ({
  deleteJson: vi.fn(),
  getBinary: vi.fn(),
  getEnvelope: vi.fn(),
  patchData: vi.fn(),
  postData: vi.fn(),
  postJson: vi.fn(),
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
      description: '',
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

describe('boardApi createBoard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('sends optional description in create payload', async () => {
    vi.mocked(postData).mockResolvedValue(ok({
      id: 1,
      name: 'Roadmap',
      description: 'Planning board',
      createdAtUtc: '2026-04-18T00:00:00Z',
      updatedAtUtc: '2026-04-18T00:00:00Z',
      currentUserRole: 'Owner',
      columns: []
    }));

    const api = createBoardApi();
    const result = await api.createBoard('Roadmap', 'Planning board');

    expect(result.ok).toBe(true);
    expect(postData).toHaveBeenCalledWith('/api/boards', { name: 'Roadmap', description: 'Planning board' });
  });
});

describe('boardApi saveBoard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('sends description in board update payload', async () => {
    vi.mocked(putData).mockResolvedValue(ok({
      id: 1,
      name: 'Roadmap',
      description: 'Updated description',
      createdAtUtc: '2026-04-18T00:00:00Z',
      updatedAtUtc: '2026-04-18T00:00:00Z',
      currentUserRole: 'Owner'
    }));

    const api = createBoardApi();
    const result = await api.saveBoard(1, 'Roadmap', 'Updated description');

    expect(result.ok).toBe(true);
    expect(putData).toHaveBeenCalledWith('/api/boards/1', { name: 'Roadmap', description: 'Updated description' });
  });
});

describe('boardApi saveCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('sends boardColumnId in update payload', async () => {
    const card = {
      id: 99,
      boardColumnId: 3,
      cardTypeId: 1,
      cardTypeName: 'Story',
      cardTypeEmoji: null,
      title: 'Updated card',
      description: 'Updated',
      sortKey: '00000000000000000001',
      tags: [],
      tagNames: [],
      createdAtUtc: '2026-04-17T00:00:00Z',
      updatedAtUtc: '2026-04-17T00:00:00Z'
    };
    vi.mocked(putData).mockResolvedValue(ok(card));

    const api = createBoardApi();
    const result = await api.saveCard(1, 99, 'Updated card', 'Updated', ['Bug'], 1, 3);

    expect(result.ok).toBe(true);
    expect(putData).toHaveBeenCalledWith('/api/boards/1/cards/99', {
      title: 'Updated card',
      description: 'Updated',
      tagNames: ['Bug'],
      cardTypeId: 1,
      boardColumnId: 3
    });
  });
});

describe('boardApi archived cards', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads archived cards with search and pagination query', async () => {
    vi.mocked(getEnvelope).mockResolvedValue(ok({
      success: true,
      data: {
        items: [],
        offset: 25,
        limit: 25,
        totalCount: 0
      },
      statusCode: 200
    }));

    const api = createBoardApi();
    const result = await api.getArchivedCards(7, { searchText: 'urgent', offset: 25, limit: 25 });

    expect(result.ok).toBe(true);
    expect(getEnvelope).toHaveBeenCalledWith('/api/boards/7/cards/archived?search=urgent&offset=25&limit=25');
  });

  it('loads a single archived card by id', async () => {
    vi.mocked(getEnvelope).mockResolvedValue(ok({
      success: true,
      data: {
        id: 3,
        boardId: 7,
        originalCardId: 42,
        title: 'Archived card',
        tagNames: ['Urgent'],
        archivedAtUtc: '2026-04-19T18:00:00Z',
        card: {
          id: 42,
          boardColumnId: 9,
          cardTypeId: 1,
          cardTypeName: 'Story',
          cardTypeEmoji: '📌',
          title: 'Archived card',
          description: 'Snapshot description',
          sortKey: 'A',
          tags: [],
          tagNames: ['Urgent'],
          createdAtUtc: '2026-04-18T18:00:00Z',
          updatedAtUtc: '2026-04-19T18:00:00Z'
        }
      },
      statusCode: 200
    }));

    const api = createBoardApi();
    const result = await api.getArchivedCard(7, 3);

    expect(result.ok).toBe(true);
    expect(getEnvelope).toHaveBeenCalledWith('/api/boards/7/cards/archived/3');
  });

  it('returns api error when archived list envelope has no data payload', async () => {
    vi.mocked(getEnvelope).mockResolvedValue(ok({
      success: true,
      data: null,
      statusCode: 200,
      message: 'Missing data'
    }));

    const api = createBoardApi();
    const result = await api.getArchivedCards(7);

    expect(result.ok).toBe(false);
    if (result.ok) {
      throw new Error('Expected error result.');
    }

    expect(result.error.kind).toBe('api');
    expect(result.error.message).toBe('Missing data');
  });

  it('archives a card via archive endpoint', async () => {
    vi.mocked(postJson).mockResolvedValue(ok(undefined));

    const api = createBoardApi();
    const result = await api.archiveCard(7, 33);

    expect(result.ok).toBe(true);
    expect(postJson).toHaveBeenCalledWith('/api/boards/7/cards/33/archive', {});
  });

  it('archives cards in bulk and returns summary payload', async () => {
    vi.mocked(postData).mockResolvedValue(ok({
      boardId: 7,
      requestedCount: 2,
      archivedCount: 2
    }));

    const api = createBoardApi();
    const result = await api.archiveCards(7, [11, 12]);

    expect(result.ok).toBe(true);
    expect(postData).toHaveBeenCalledWith('/api/boards/7/cards/archive', { cardIds: [11, 12] });
    if (!result.ok) {
      throw new Error('Expected success result.');
    }

    expect(result.data.boardId).toBe(7);
    expect(result.data.requestedCount).toBe(2);
    expect(result.data.archivedCount).toBe(2);
  });
});
