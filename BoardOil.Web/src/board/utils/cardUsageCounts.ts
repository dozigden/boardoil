import type { Board } from '../../shared/types/boardTypes';

export function countCardsByTagName(board: Board | null): Record<string, number> {
  const usageCountByTagName: Record<string, number> = {};
  if (!board) {
    return usageCountByTagName;
  }

  for (const column of board.columns) {
    for (const card of column.cards) {
      for (const tagName of card.tagNames) {
        usageCountByTagName[tagName] = (usageCountByTagName[tagName] ?? 0) + 1;
      }
    }
  }

  return usageCountByTagName;
}

export function countCardsBySlickId(board: Board | null): Record<number, number> {
  const usageCountBySlickId: Record<number, number> = {};
  if (!board) {
    return usageCountBySlickId;
  }

  for (const column of board.columns) {
    for (const card of column.cards) {
      if (typeof card.slickId === 'number') {
        usageCountBySlickId[card.slickId] = (usageCountBySlickId[card.slickId] ?? 0) + 1;
      }
    }
  }

  return usageCountBySlickId;
}

export function countCardsByCardTypeId(board: Board | null): Record<number, number> {
  const usageCountByCardTypeId: Record<number, number> = {};
  if (!board) {
    return usageCountByCardTypeId;
  }

  for (const column of board.columns) {
    for (const card of column.cards) {
      usageCountByCardTypeId[card.cardTypeId] = (usageCountByCardTypeId[card.cardTypeId] ?? 0) + 1;
    }
  }

  return usageCountByCardTypeId;
}

export function formatCardCount(cardCount: number) {
  if (cardCount === 1) {
    return '1 card';
  }

  return `${cardCount} cards`;
}
