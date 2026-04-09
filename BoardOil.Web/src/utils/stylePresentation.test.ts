import { describe, expect, it } from 'vitest';
import {
  buildStylePropertiesJsonFromDraft,
  createStyleDraft,
  getSurfaceStyle
} from './stylePresentation';

const fallbackStylePropertiesJson = '{"backgroundColor":"#69C1CE","textColorMode":"auto","borderMode":"auto"}';

describe('stylePresentation', () => {
  it('defaults to auto border mode when border mode is missing', () => {
    const draft = createStyleDraft(
      {
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"#FFFFFF","textColorMode":"auto"}'
      },
      fallbackStylePropertiesJson
    );

    expect(draft.borderMode).toBe('auto');
  });

  it('uses fallback border color in auto mode for very light backgrounds', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"#FFFFFF","textColorMode":"auto","borderMode":"auto"}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      },
      fallbackStylePropertiesJson
    );

    expect(style.borderColor).toBe('#D8CDEC');
  });

  it('uses custom border color when custom mode is selected', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'gradient',
        stylePropertiesJson: '{"leftColor":"#113355","rightColor":"#446688","textColorMode":"auto","borderMode":"custom","borderColor":"#AA2244"}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      },
      fallbackStylePropertiesJson
    );

    expect(style.borderColor).toBe('#AA2244');
  });

  it('uses transparent border when border mode is none', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"#224466","textColorMode":"auto","borderMode":"none"}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      },
      fallbackStylePropertiesJson
    );

    expect(style.borderColor).toBe('transparent');
  });

  it('serializes border fields in style properties json', () => {
    const json = buildStylePropertiesJsonFromDraft({
      styleName: 'solid',
      textColorMode: 'auto',
      borderMode: 'custom',
      backgroundColor: '#69C1CE',
      leftColor: '#69C1CE',
      rightColor: '#69C1CE',
      textColor: '#111827',
      borderColor: '#334455'
    });

    expect(json).toContain('"borderMode":"custom"');
    expect(json).toContain('"borderColor":"#334455"');
  });
});
