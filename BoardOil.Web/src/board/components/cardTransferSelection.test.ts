import { describe, expect, it } from 'vitest';
import type { BoardSummary } from '../../shared/types/boardTypes';
import {
  canCopyMissingDefinitions,
  defaultCardTransferPolicy,
  getDestinationBoards,
  resolvePolicyForDestination
} from './cardTransferSelection';

describe('card transfer selection', () => {
  const boards: BoardSummary[] = [
    makeBoard(1, 'Owner'),
    makeBoard(2, 'Contributor'),
    makeBoard(3, 'Owner')
  ];

  it('defaults to keeping matching definitions', () => {
    expect(defaultCardTransferPolicy).toBe('keepMatching');
  });

  it('excludes the source board from destination choices', () => {
    expect(getDestinationBoards(1, boards).map(board => board.id)).toEqual([2, 3]);
  });

  it('allows copy missing only for destination owners', () => {
    expect(canCopyMissingDefinitions(boards[2]!)).toBe(true);
    expect(canCopyMissingDefinitions(boards[1]!)).toBe(false);
    expect(resolvePolicyForDestination('copyMissing', boards[1]!)).toBe('keepMatching');
    expect(resolvePolicyForDestination('copyMissing', boards[2]!)).toBe('copyMissing');
  });
});

function makeBoard(id: number, currentUserRole: 'Owner' | 'Contributor'): BoardSummary {
  return {
    id,
    name: `Board ${id}`,
    description: '',
    slickCohesionModeEnabled: true,
    createdAtUtc: '2026-08-28T00:00:00Z',
    updatedAtUtc: '2026-08-28T00:00:00Z',
    currentUserRole
  };
}
