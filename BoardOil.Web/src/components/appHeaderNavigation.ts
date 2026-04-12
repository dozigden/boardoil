import type { Board, BoardSummary } from '../types/boardTypes';
import type { RouteLocationRaw } from 'vue-router';

const BROWSER_TITLE_SUFFIX = ' - Board Oil';
const PRODUCT_TITLE = 'Board Oil';

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
  return currentBoardName === '' ? PRODUCT_TITLE : `${currentBoardName}${BROWSER_TITLE_SUFFIX}`;
}

export function getSortedBoards(boards: BoardSummary[]) {
  return [...boards].sort((left, right) => {
    const byName = left.name.localeCompare(right.name);
    if (byName !== 0) {
      return byName;
    }

    return left.id - right.id;
  });
}

export function getOtherBoards(boards: BoardSummary[], currentBoardId: number | null) {
  return getSortedBoards(boards).filter(board => board.id !== currentBoardId);
}
