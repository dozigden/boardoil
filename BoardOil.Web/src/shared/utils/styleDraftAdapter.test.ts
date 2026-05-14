import { describe, expect, it } from 'vitest';
import { buildStylePropertiesJsonFromDraft, createStyleDraft } from './styleDraftAdapter';

describe('styleDraftAdapter', () => {
  it('creates draft with auto border mode when missing in input', () => {
    const draft = createStyleDraft({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#FFFFFF","textColorMode":"auto"}'
    });

    expect(draft.borderMode).toBe('auto');
  });

  it('maps invalid payload to auto draft', () => {
    const draft = createStyleDraft({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"oops"}'
    });

    expect(draft.styleName).toBe('auto');
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
