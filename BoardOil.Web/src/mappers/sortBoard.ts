import type { Board } from '../types/boardTypes';

export function sortBoard(source: Board): Board {
  return {
    ...source,
    columns: [...source.columns]
      .sort((a, b) => a.position - b.position)
      .map(column => ({
        ...column,
        cards: [...column.cards].sort((a, b) => a.position - b.position)
      }))
  };
}
