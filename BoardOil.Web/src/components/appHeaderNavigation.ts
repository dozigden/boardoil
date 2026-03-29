import type { Board, BoardSummary } from '../types/boardTypes';
import type { RouteLocationRaw } from 'vue-router';

export function getBrandTarget(boards: BoardSummary[]): RouteLocationRaw {
  if (boards.length === 1) {
    return { name: 'board', params: { boardId: boards[0].id } };
  }

  return { name: 'boards' };
}

export function getCurrentBoardName(board: Board | null, boards: BoardSummary[], currentBoardId: number | null) {
  return board?.name ?? boards.find(entry => entry.id === currentBoardId)?.name ?? '';
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
  return boards.filter(board => board.id !== currentBoardId);
}
