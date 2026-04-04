import type { Board, BoardSummary } from '../types/boardTypes';
import type { RouteLocationRaw } from 'vue-router';

export function getBrandTarget(boards: BoardSummary[]): RouteLocationRaw {
  return { name: 'boards' };
}

export function getCurrentBoardName(board: Board | null, boards: BoardSummary[], currentBoardId: number | null) {
  return board?.name ?? boards.find(entry => entry.id === currentBoardId)?.name ?? '';
}

export function getCurrentBoardTarget(currentBoardId: number | null): RouteLocationRaw | null {
  if (currentBoardId === null) {
    return null;
  }

  return { name: 'board', params: { boardId: currentBoardId } };
}

export function getPageTitle(
  board: Board | null,
  boards: BoardSummary[],
  currentBoardId: number | null,
  routeBoardId: number | null
) {
  const activeBoardId = currentBoardId ?? routeBoardId;
  const currentBoardName = getCurrentBoardName(board, boards, activeBoardId);
  return currentBoardName === '' ? 'BoardOil' : currentBoardName;
}

export function getOtherBoards(boards: BoardSummary[], currentBoardId: number | null) {
  return boards
    .filter(board => board.id !== currentBoardId)
    .sort((left, right) => {
      const byName = left.name.localeCompare(right.name);
      if (byName !== 0) {
        return byName;
      }

      return left.id - right.id;
    });
}
