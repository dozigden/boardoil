import { describe, expect, it } from 'vitest';
import {
  buildTrackedCardGeometry,
  intersectsExpandedRect,
  projectTrackedCardRect,
  resolveGooCullingMarginPx
} from './gooGeometry';

describe('gooGeometry', () => {
  it('projects fast-path rect from cached geometry using scroll offsets', () => {
    const clipRect = { left: 100, top: 200, width: 300, height: 500 };
    const cardRect = { left: 140, top: 260, width: 180, height: 72 };

    const geometry = buildTrackedCardGeometry(cardRect, clipRect, 0, 0);
    const projected = projectTrackedCardRect(clipRect, geometry, 0, 40);

    expect(projected.left).toBe(140);
    expect(projected.top).toBe(220);
    expect(projected.width).toBe(180);
    expect(projected.height).toBe(72);
  });

  it('ensures culling margin keeps bridge continuity and minimum baseline', () => {
    const withBridgeGapDominating = resolveGooCullingMarginPx({
      bridgeMaxGapPx: 180,
      widthAdjustPx: 12,
      heightAdjustPx: 9
    });
    expect(withBridgeGapDominating).toBe(180);

    const withMinimumDominating = resolveGooCullingMarginPx({
      bridgeMaxGapPx: 10,
      widthAdjustPx: 3,
      heightAdjustPx: 5
    });
    expect(withMinimumDominating).toBe(24);
  });

  it('treats near-edge rects as visible when inside expanded margin and culls far ones', () => {
    const clipRect = { left: 100, top: 100, width: 200, height: 300 };
    const margin = 50;

    const nearEdgeRect = { left: 55, top: 120, width: 30, height: 40 };
    const farRect = { left: 10, top: 120, width: 20, height: 20 };

    expect(intersectsExpandedRect(nearEdgeRect, clipRect, margin)).toBe(true);
    expect(intersectsExpandedRect(farRect, clipRect, margin)).toBe(false);
  });
});
