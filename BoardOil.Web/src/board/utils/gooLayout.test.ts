import { describe, expect, it } from 'vitest';
import { buildGooGroups, type GooConfig, type GooItem, type RectLike } from './gooLayout';

const boardRect: RectLike = {
  left: 0,
  top: 0,
  width: 1200,
  height: 900
};

const config: GooConfig = {
  widthAdjustPx: 0,
  horizontalOffsetPx: 0,
  heightAdjustPx: 0,
  verticalOffsetPx: 0,
  blurStdDeviation: 8,
  alphaMultiplier: 40,
  alphaOffset: -10,
  bridgeMaxGapPx: 120,
  bridgeMaxVerticalDeltaPx: 40,
  bridgeOverlapPx: 8,
  bridgeHeightRatio: 0.4,
  minBlobSizePx: 8,
  blobBorderRadiusPx: 10,
  clipHorizontalInsetPx: 0
};

describe('gooLayout', () => {
  it('groups blobs by group key and does not merge groups', () => {
    const items: GooItem[] = [
      makeItem('a1', 'slick-1', '#ff0000', { left: 20, top: 20, width: 50, height: 40 }),
      makeItem('b1', 'slick-2', '#00ff00', { left: 80, top: 20, width: 50, height: 40 })
    ];

    const groups = buildGooGroups(items, boardRect, config);

    expect(groups.map(x => x.id).sort()).toEqual(['slick-1', 'slick-2']);
    expect(groups.every(x => x.blobs.length === 1)).toBe(true);
  });

  it('creates bridge blobs only when gap and vertical delta are within limits', () => {
    const items: GooItem[] = [
      makeItem('left', 'slick-1', '#ff0000', { left: 20, top: 20, width: 40, height: 40 }),
      makeItem('right', 'slick-1', '#ff0000', { left: 90, top: 36, width: 40, height: 40 })
    ];

    const groups = buildGooGroups(items, boardRect, config);
    const group = groups.find(x => x.id === 'slick-1');
    expect(group).toBeDefined();
    expect(group!.blobs).toHaveLength(3);
    expect(group!.blobs.some(blob => blob.id.includes('-bridge-'))).toBe(true);
  });

  it('does not create bridge blobs when gap exceeds limit', () => {
    const items: GooItem[] = [
      makeItem('left', 'slick-1', '#ff0000', { left: 20, top: 20, width: 40, height: 40 }),
      makeItem('right', 'slick-1', '#ff0000', { left: 220, top: 20, width: 40, height: 40 })
    ];

    const groups = buildGooGroups(items, boardRect, config);
    const group = groups.find(x => x.id === 'slick-1');
    expect(group).toBeDefined();
    expect(group!.blobs).toHaveLength(2);
    expect(group!.blobs.some(blob => blob.id.includes('-bridge-'))).toBe(false);
  });

  it('does not create bridge blobs when vertical delta exceeds limit', () => {
    const items: GooItem[] = [
      makeItem('left', 'slick-1', '#ff0000', { left: 20, top: 20, width: 40, height: 40 }),
      makeItem('right', 'slick-1', '#ff0000', { left: 90, top: 120, width: 40, height: 40 })
    ];

    const groups = buildGooGroups(items, boardRect, config);
    const group = groups.find(x => x.id === 'slick-1');
    expect(group).toBeDefined();
    expect(group!.blobs).toHaveLength(2);
    expect(group!.blobs.some(blob => blob.id.includes('-bridge-'))).toBe(false);
  });

  it('does not create bridge blobs when items overlap', () => {
    const items: GooItem[] = [
      makeItem('left', 'slick-1', '#ff0000', { left: 20, top: 20, width: 60, height: 40 }),
      makeItem('right', 'slick-1', '#ff0000', { left: 70, top: 20, width: 60, height: 40 })
    ];

    const groups = buildGooGroups(items, boardRect, config);
    const group = groups.find(x => x.id === 'slick-1');
    expect(group).toBeDefined();
    expect(group!.blobs).toHaveLength(2);
    expect(group!.blobs.some(blob => blob.id.includes('-bridge-'))).toBe(false);
  });

  it('keeps blob geometry and applies clip path within clip bounds', () => {
    const clipRect: RectLike = {
      left: 60,
      top: 60,
      width: 60,
      height: 60
    };
    const items: GooItem[] = [
      makeItem('clipped', 'slick-1', '#ff0000', { left: 20, top: 20, width: 100, height: 100 }, clipRect)
    ];

    const groups = buildGooGroups(items, boardRect, config);
    const blob = groups[0]?.blobs[0];
    expect(blob).toBeDefined();

    expect(blob!.left).toBe(20);
    expect(blob!.top - (blob!.height / 2)).toBe(20);
    expect(blob!.width).toBe(100);
    expect(blob!.height).toBe(100);
    expect(blob!.clipPath).toBe('inset(20px 0px 0px 20px)');
    expect(blob!.clipInsets).toEqual({ top: 20, right: 0, bottom: 0, left: 20 });
  });

  it('drops clipped blobs that become smaller than min blob size', () => {
    const smallClipRect: RectLike = {
      left: 40,
      top: 40,
      width: 7,
      height: 7
    };
    const items: GooItem[] = [
      makeItem('tiny', 'slick-1', '#ff0000', { left: 20, top: 20, width: 30, height: 30 }, smallClipRect)
    ];

    const groups = buildGooGroups(items, boardRect, config);
    expect(groups).toHaveLength(0);
  });

  it('keeps blob coordinates aligned to scrollable board content when board is horizontally scrolled', () => {
    const boardViewportRect: RectLike = {
      left: 100,
      top: 0,
      width: 1200,
      height: 900
    };
    const items: GooItem[] = [
      makeItem('card-1', 'slick-1', '#ff0000', { left: 250, top: 80, width: 100, height: 40 })
    ];

    const groups = buildGooGroups(items, boardViewportRect, config, { left: 50, top: 0 });
    const blob = groups[0]?.blobs[0];

    expect(blob).toBeDefined();
    expect(blob!.left).toBe(200);
  });
});

function makeItem(
  id: string,
  groupKey: string,
  colour: string,
  rect: RectLike,
  clipRect: RectLike | null = null
): GooItem {
  return {
    id,
    groupKey,
    colour,
    rect,
    clipRect
  };
}
