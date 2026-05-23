import type { Card, CardEditModel } from '../../shared/types/boardTypes';

export function createEditModelFromCard(card: Card): CardEditModel {
  return {
    title: card.title,
    description: card.description,
    tagNames: [...card.tagNames],
    cardTypeId: card.cardTypeId,
    boardColumnId: card.boardColumnId,
    assignedUserId: card.assignedUserId ?? null,
    slickName: card.slickName ?? null
  };
}
