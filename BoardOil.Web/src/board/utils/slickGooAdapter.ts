import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import { getSlickPresetCssValue } from '../../shared/utils/presetTheme';
import { deserializeStyle } from '../../shared/utils/stylePersistence';
import { getSurfaceStyle } from '../../shared/utils/styleRenderer';
type GooLayerDescriptor = {
  cardId: number;
  columnId: number;
  itemId: string;
  groupKey: string;
  colour: string;
};

export function buildSlickGooDescriptors(columns: BoardColumn[], slicksById: Map<number, Slick>): GooLayerDescriptor[] {
  const descriptors: GooLayerDescriptor[] = [];
  for (const column of columns) {
    for (const card of column.cards) {
      if (card.slickId === null || card.slickId === undefined) {
        continue;
      }

      const slickId = card.slickId;
      descriptors.push({
        cardId: card.id,
        columnId: column.id,
        itemId: `card-${card.id}`,
        groupKey: `slick-${slickId}`,
        colour: resolveSlickGooColour(slicksById.get(slickId), slickId)
      });
    }
  }

  return descriptors;
}

function resolveSlickGooColour(slick: Slick | undefined, slickId: number): string {
  if (slick) {
    const styleModel = deserializeStyle(slick);
    if (styleModel.styleName === 'presets') {
      return getSlickPresetCssValue(styleModel.presetIndex);
    }

    const surfaceStyle = getSurfaceStyle(slick, {
      fallbackBackground: hashedSlickColour(slickId),
      fallbackColor: '#111827',
      fallbackBorderColor: '#000000'
    });
    const styledBackground = surfaceStyle.background;
    if (typeof styledBackground === 'string' && styledBackground.trim().length > 0) {
      return styledBackground;
    }
  }

  return hashedSlickColour(slickId);
}

function hashedSlickColour(slickId: number): string {
  const hue = Math.abs((slickId * 47) % 360);
  return `hsl(${hue} 72% 46%)`;
}
