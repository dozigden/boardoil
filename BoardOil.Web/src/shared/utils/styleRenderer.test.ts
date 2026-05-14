import { describe, expect, it } from 'vitest';
import { getSemanticStyleClasses, getSurfaceStyle } from './styleRenderer';

describe('styleRenderer', () => {
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
      }
    );

    expect(style.borderColor).toBe('#D8CDEC');
  });

  it('uses custom border color in gradient custom mode', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'gradient',
        stylePropertiesJson: '{"leftColor":"#113355","rightColor":"#446688","textColorMode":"auto","borderMode":"custom","borderColor":"#AA2244"}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      }
    );

    expect(style.borderColor).toBe('#AA2244');
  });

  it('renders presets as semantic classes and no inline style', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'presets',
        stylePropertiesJson: '{"presetIndex":4}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      }
    );

    expect(style).toEqual({});
    expect(
      getSemanticStyleClasses(
        {
          styleName: 'presets',
          stylePropertiesJson: '{"presetIndex":4}'
        },
        'card'
      )
    ).toEqual(['bo-card-style-presets', 'bo-card-style-presets-4']);
  });

  it('renders auto as semantic class and no inline style', () => {
    const style = getSurfaceStyle(
      {
        styleName: 'auto',
        stylePropertiesJson: '{"textColorMode":"custom","textColor":"#FF00FF","borderMode":"none"}'
      },
      {
        fallbackBackground: '#F1EBFB',
        fallbackColor: '#2B1247',
        fallbackBorderColor: '#D8CDEC'
      }
    );

    expect(style).toEqual({});
    expect(
      getSemanticStyleClasses(
        {
          styleName: 'auto',
          stylePropertiesJson: '{"textColorMode":"custom","textColor":"#FF00FF","borderMode":"none"}'
        },
        'tag'
      )
    ).toEqual(['bo-tag-style-auto']);
  });
});
