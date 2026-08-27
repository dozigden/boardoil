import { describe, expect, it } from 'vitest';
import {
  buildStylePropertiesJsonFromDraft,
  createRestrictedStyleDraft,
  createStyleDraft
} from './styleDraftAdapter';

describe('styleDraftAdapter', () => {
  it('maps non-canonical solid payloads to auto draft', () => {
    const draft = createStyleDraft({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#FFFFFF","textColorMode":"auto"}'
    });

    expect(draft.styleName).toBe('auto');
  });

  it('maps invalid payload to auto draft', () => {
    const draft = createStyleDraft({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"oops"}'
    });

    expect(draft.styleName).toBe('auto');
    expect(buildStylePropertiesJsonFromDraft(draft)).toBe('{}');
  });

  it('maps invalid slick payload to an allowed preset draft', () => {
    const draft = createRestrictedStyleDraft(
      {
        styleName: 'solid',
        stylePropertiesJson: '{"backgroundColor":"oops"}'
      },
      new Set(['solid', 'presets']),
      {
        styleName: 'presets',
        stylePropertiesJson: '{"presetIndex":2}'
      }
    );

    expect(draft.styleName).toBe('presets');
    expect(buildStylePropertiesJsonFromDraft(draft)).toBe('{"presetIndex":2}');
  });

  it('maps an unsupported system information gradient to auto', () => {
    const draft = createRestrictedStyleDraft(
      {
        styleName: 'gradient',
        stylePropertiesJson: '{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"auto"}'
      },
      new Set(['auto', 'presets', 'solid']),
      {
        styleName: 'auto',
        stylePropertiesJson: '{}'
      }
    );

    expect(draft.styleName).toBe('auto');
    expect(buildStylePropertiesJsonFromDraft(draft)).toBe('{}');
  });

  it('serializes custom border fields for manual styles', () => {
    const json = buildStylePropertiesJsonFromDraft({
      styleName: 'solid',
      textColorMode: 'auto',
      borderMode: 'custom',
      presetIndex: 2,
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
