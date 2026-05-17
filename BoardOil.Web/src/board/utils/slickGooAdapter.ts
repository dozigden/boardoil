import type { BoardColumn, Slick } from '../../shared/types/boardTypes';
import { getPresetCssValue } from '../../shared/utils/presetTheme';
import { deserializeStyle } from '../../shared/utils/stylePersistence';
import { getSurfaceStyle } from '../../shared/utils/styleRenderer';
import type { GooLayerDescriptor } from '../composables/useGooLayer';

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
        itemId: `card-${card.id}`,
        groupKey: `slick-${slickId}`,
        colour: resolveSlickGooColour(slicksById.get(slickId), slickId)
      });
    }
  }

  return descriptors;
}

export function buildSlickGooMembershipSignature(columns: BoardColumn[]): string {
  return columns
    .flatMap(column =>
      column.cards
        .filter(card => card.slickId !== null && card.slickId !== undefined)
        .map(card => `${card.id}:${card.slickId}`)
    )
    .join('|');
}

export function buildSlickGooStyleSignature(slicks: Slick[]): string {
  return [...slicks]
    .sort((left, right) => left.id - right.id)
    .map(slick => `${slick.id}:${slick.styleName}:${slick.stylePropertiesJson}`)
    .join('|');
}

function resolveSlickGooColour(slick: Slick | undefined, slickId: number): string {
  if (slick) {
    const styleModel = deserializeStyle(slick);
    if (styleModel.styleName === 'presets') {
      return getPresetCssValue(styleModel.presetIndex);
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
