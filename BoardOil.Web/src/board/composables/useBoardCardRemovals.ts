import { onBeforeUnmount, shallowReactive } from 'vue';
import type { BoardColumn, Card } from '../../shared/types/boardTypes';
import { resolveCardBoundaryClass } from '../utils/slickCardBoundary';

type DepartingCard = {
  card: Card;
  columnId: number;
  followingIds: number[];
  boundaryClass: string | null;
};

// Keep only the visual snapshot after canonical removal. Stores and callers
// immediately see the real result; the motion coordinator releases this snapshot.
export function useBoardCardRemovals() {
  const departing = shallowReactive(new Map<number, DepartingCard>());
  // Each projected array owns a complete boundary snapshot. Goo-driven renders
  // reuse it until the source columns or departure membership change.
  const boundaryClasses = new WeakMap<Card[], Map<number, string | null>>();

  function retain(columns: BoardColumn[], removedIds: Set<number>) {
    const added: number[] = [];
    for (const column of columns) {
      column.cards.forEach((card, index) => {
        if (removedIds.has(card.id) && !departing.has(card.id)) {
          departing.set(card.id, {
            card, columnId: column.id,
            followingIds: column.cards.slice(index + 1).map(card => card.id),
            boundaryClass: resolveCardBoundaryClass(column.cards, index)
          });
          added.push(card.id);
        }
      });
    }
    return added;
  }

  function project(columns: BoardColumn[]) {
    if (departing.size === 0) { return columns; }
    return columns.map(column => {
      const cards = [...column.cards];
      const classes = new Map(column.cards.map((card, index) => [card.id, resolveCardBoundaryClass(column.cards, index)]));
      for (const entry of [...departing.values()].reverse()) {
        if (entry.columnId !== column.id) { continue; }
        const index = cards.findIndex(card => entry.followingIds.includes(card.id));
        cards.splice(index < 0 ? cards.length : index, 0, entry.card);
        classes.set(entry.card.id, entry.boundaryClass);
      }
      boundaryClasses.set(cards, classes);
      return { ...column, cards };
    });
  }

  function boundaryClass(cards: Card[], index: number) {
    const card = cards[index];
    if (!card) { return null; }
    const classes = boundaryClasses.get(cards);
    if (classes) { return classes.get(card.id) ?? null; }
    return resolveCardBoundaryClass(cards, index);
  }

  function prune(columns: BoardColumn[], canonicalIds: Set<number>) {
    const columnIds = new Set(columns.map(column => column.id));
    for (const [id, entry] of departing) {
      if (!columnIds.has(entry.columnId) || canonicalIds.has(id)) { departing.delete(id); }
    }
  }

  onBeforeUnmount(() => departing.clear());
  return {
    retain, project, boundaryClass, prune,
    has: (id: number) => departing.has(id),
    cardIds: () => [...departing.keys()],
    remove: (id: number) => { departing.delete(id); },
    clear: () => departing.clear()
  };
}
