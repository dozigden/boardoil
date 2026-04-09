import { describe, expect, it } from 'vitest';
import { getBrandTarget, getCurrentBoardName, getCurrentBoardTarget, getOtherBoards, getPageTitle } from './appHeaderNavigation';
import type { Board, BoardSummary } from '../types/boardTypes';

describe('appHeaderNavigation', () => {
  it('routes the brand link to board management when exactly one board exists', () => {
    const boards: BoardSummary[] = [
      {
        id: 7,
        name: 'Solo board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      }
    ];

    expect(getBrandTarget(boards)).toEqual({ name: 'boards' });
  });

  it('routes the brand link to board management when multiple boards exist', () => {
    const boards: BoardSummary[] = [
      {
        id: 7,
        name: 'Solo board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      },
      {
        id: 8,
        name: 'Second board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      }
    ];

    expect(getBrandTarget(boards)).toEqual({ name: 'boards' });
  });

  it('filters the current board out of the switcher list and sorts remaining boards by name', () => {
    const boards: BoardSummary[] = [
      {
        id: 7,
        name: 'Solo board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      },
      {
        id: 8,
        name: 'Zulu',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      },
      {
        id: 9,
        name: 'Alpha',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      }
    ];

    expect(getOtherBoards(boards, 7).map(board => board.id)).toEqual([9, 8]);
  });

  it('resolves the current board route for the board-name link', () => {
    expect(getCurrentBoardTarget(12)).toEqual({ name: 'board', params: { boardId: 12 } });
    expect(getCurrentBoardTarget(null)).toBeNull();
  });

  it('prefers the loaded board name and falls back to the catalogue list', () => {
    const board: Board = {
      id: 7,
      name: 'Loaded board',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z',
      columns: []
    };
    const boards: BoardSummary[] = [
      {
        id: 7,
        name: 'Catalogue board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      }
    ];

    expect(getCurrentBoardName(board, boards, 7)).toBe('Loaded board');
    expect(getCurrentBoardName(null, boards, 7)).toBe('Catalogue board');
  });

  it('uses the active board name as the page title', () => {
    const board: Board = {
      id: 7,
      name: 'Loaded board',
      createdAtUtc: '2026-03-15T00:00:00Z',
      updatedAtUtc: '2026-03-15T00:00:00Z',
      columns: []
    };
    const boards: BoardSummary[] = [
      {
        id: 7,
        name: 'Catalogue board',
        createdAtUtc: '2026-03-15T00:00:00Z',
        updatedAtUtc: '2026-03-15T00:00:00Z'
      }
    ];

    expect(getPageTitle(board, boards, 7, null)).toBe('Loaded board - Board Oil');
    expect(getPageTitle(null, boards, null, 7)).toBe('Catalogue board - Board Oil');
  });

  it('falls back to the product name when no active board can be resolved', () => {
    expect(getPageTitle(null, [], null, null)).toBe('Board Oil');
  });
});
