import { describe, expect, it } from 'vitest';
import { getCardSurfaceStyle } from './cardTypeStyles';

describe('cardTypeStyles', () => {
  it('uses the solid card background as the thumbnail number halo colour', () => {
    const style = getCardSurfaceStyle({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#69C1CE","textColorMode":"auto","borderMode":"auto"}'
    });

    expect(style['--bo-card-thumbnail-halo-color']).toBe('#69C1CE');
  });

  it('uses the right-hand gradient colour beneath the thumbnail number halo', () => {
    const style = getCardSurfaceStyle({
      styleName: 'gradient',
      stylePropertiesJson: '{"leftColor":"#113355","rightColor":"#446688","textColorMode":"auto","borderMode":"auto"}'
    });

    expect(style['--bo-card-thumbnail-halo-color']).toBe('#446688');
  });
});
