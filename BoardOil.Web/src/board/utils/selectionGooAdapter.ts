import type { BoardColumn } from '../../shared/types/boardTypes';
import type { GooLayerDescriptor } from '../composables/useGooLayer';

const SELECTION_GROUP_KEY = 'selection';
const DEFAULT_SELECTION_GOO_COLOUR = 'var(--bo-colour-brand)';

export type SelectionGooStyle = {
  colour: string;
  styleSignature: string;
};

export function buildSelectionGooDescriptors(
  columns: BoardColumn[],
  selectedCardIds: ReadonlySet<number>,
  colour: string
): GooLayerDescriptor[] {
  const descriptors: GooLayerDescriptor[] = [];
  for (const column of columns) {
    for (const card of column.cards) {
      if (!selectedCardIds.has(card.id)) {
        continue;
      }

      descriptors.push({
        cardId: card.id,
        columnId: column.id,
        itemId: `selection-card-${card.id}`,
        groupKey: SELECTION_GROUP_KEY,
        colour
      });
    }
  }

  return descriptors;
}

export function buildSelectionGooMembershipSignature(
  columns: BoardColumn[],
  selectedCardIds: ReadonlySet<number>,
  selectionModeEnabled: boolean
): string {
  if (!selectionModeEnabled) {
    return 'off';
  }

  return columns
    .flatMap(column =>
      column.cards
        .filter(card => selectedCardIds.has(card.id))
        .map(card => `${column.id}:${card.id}`)
    )
    .join('|');
}

export function buildSelectionGooStyleSignature(colour: string): string {
  return `${SELECTION_GROUP_KEY}:${colour}`;
}

export function createSelectionGooStyle(colour: string = DEFAULT_SELECTION_GOO_COLOUR): SelectionGooStyle {
  return {
    colour,
    styleSignature: buildSelectionGooStyleSignature(colour)
  };
}
