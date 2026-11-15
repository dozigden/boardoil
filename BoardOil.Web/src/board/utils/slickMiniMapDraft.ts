import type { BoardColumn, Card } from '../../shared/types/boardTypes';

export function projectSlickMiniMapDraft(columns: BoardColumn[], draft: Card | null, cohesionEnabled: boolean): BoardColumn[] {
  if (!draft) { return columns; }
  const original = columns.flatMap(column => column.cards).find(card => card.id === draft.id);
  if (!original) { return columns; }

  return columns.map(column => {
    if (original.boardColumnId === draft.boardColumnId) {
      if (column.id !== draft.boardColumnId) { return column; }
      return { ...column, cards: column.cards.map(card => card.id === draft.id ? draft : card) };
    }

    const cards = column.cards.filter(card => card.id !== draft.id);
    if (column.id === draft.boardColumnId) {
      let insertionIndex = 0;
      if (cohesionEnabled && original.slickId != null) {
        // UpdateCardService resolves movement using the saved slick before applying the draft membership.
        const firstSlickIndex = cards.findIndex(card => card.slickId === original.slickId);
        if (firstSlickIndex >= 0) { insertionIndex = firstSlickIndex; }
      }
      cards.splice(insertionIndex, 0, draft);
    }
    return { ...column, cards };
  });
}
