import type { GooCard } from '../goo/types';

export const SELECTION_GROUP_ID = 0;

export function buildSelectionBridgeCards(cards: GooCard[], selectedCardIds: ReadonlySet<number>): GooCard[] {
  return cards.map(card => ({ ...card, group: selectedCardIds.has(card.id) ? SELECTION_GROUP_ID : null }));
}
