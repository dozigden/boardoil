import type { GooConfig, RectLike } from './gooLayout';

export type TrackedGooCardGeometry = {
  leftInClip: number;
  topInClip: number;
  width: number;
  height: number;
};

export function buildTrackedCardGeometry(
  cardRect: RectLike,
  clipRect: RectLike,
  scrollLeft: number,
  scrollTop: number
): TrackedGooCardGeometry {
  return {
    leftInClip: cardRect.left - clipRect.left + scrollLeft,
    topInClip: cardRect.top - clipRect.top + scrollTop,
    width: cardRect.width,
    height: cardRect.height
  };
}

export function projectTrackedCardRect(
  clipRect: RectLike,
  geometry: TrackedGooCardGeometry,
  scrollLeft: number,
  scrollTop: number
): RectLike {
  return {
    left: clipRect.left + geometry.leftInClip - scrollLeft,
    top: clipRect.top + geometry.topInClip - scrollTop,
    width: geometry.width,
    height: geometry.height
  };
}

export function resolveGooCullingMarginPx(config: Pick<GooConfig, 'bridgeMaxGapPx' | 'widthAdjustPx' | 'heightAdjustPx'>): number {
  return Math.max(
    24,
    config.bridgeMaxGapPx,
    Math.abs(config.widthAdjustPx),
    Math.abs(config.heightAdjustPx)
  );
}

export function intersectsExpandedRect(rect: RectLike, clipRect: RectLike, marginPx: number): boolean {
  const left = clipRect.left - marginPx;
  const top = clipRect.top - marginPx;
  const right = clipRect.left + clipRect.width + marginPx;
  const bottom = clipRect.top + clipRect.height + marginPx;

  const rectRight = rect.left + rect.width;
  const rectBottom = rect.top + rect.height;
  if (rectRight < left || rect.left > right) {
    return false;
  }

  if (rectBottom < top || rect.top > bottom) {
    return false;
  }

  return true;
}
