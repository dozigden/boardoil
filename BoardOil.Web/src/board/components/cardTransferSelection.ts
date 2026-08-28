import type { BoardSummary, CardTransferPolicy } from '../../shared/types/boardTypes';

export const defaultCardTransferPolicy: CardTransferPolicy = 'keepMatching';

export function getDestinationBoards(sourceBoardId: number | null, boards: BoardSummary[]) {
  return boards.filter(board => board.id !== sourceBoardId);
}

export function canCopyMissingDefinitions(board: BoardSummary | null) {
  return board?.currentUserRole === 'Owner';
}

export function resolvePolicyForDestination(
  policy: CardTransferPolicy,
  board: BoardSummary | null
): CardTransferPolicy {
  if (policy === 'copyMissing' && !canCopyMissingDefinitions(board)) {
    return defaultCardTransferPolicy;
  }

  return policy;
}
