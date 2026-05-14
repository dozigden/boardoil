import { describe, expect, it } from 'vitest';
import { deserializeStyle, serializeStyleModel } from './stylePersistence';

describe('stylePersistence', () => {
  it('deserializes presets and ignores extra payload fields', () => {
    const model = deserializeStyle({
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":3,"textColorMode":"custom","textColor":"#FF00FF"}'
    });

    expect(model).toEqual({ styleName: 'presets', presetIndex: 3 });
  });

  it('falls back to auto model on invalid presets payload', () => {
    const model = deserializeStyle({
      styleName: 'presets',
      stylePropertiesJson: '{"presetIndex":"banana"}'
    });

    expect(model).toEqual({ styleName: 'auto' });
  });

  it('falls back to auto model on invalid solid payload', () => {
    const model = deserializeStyle({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#69C1CE","textColorMode":"custom","borderMode":"custom"}'
    });

    expect(model).toEqual({ styleName: 'auto' });
  });

  it('falls back to auto model when style json is unparsable', () => {
    const model = deserializeStyle({
      styleName: 'gradient',
      stylePropertiesJson: '{not json'
    });

    expect(model).toEqual({ styleName: 'auto' });
  });

  it('supports historical solid payloads that omit borderMode', () => {
    const model = deserializeStyle({
      styleName: 'solid',
      stylePropertiesJson: '{"backgroundColor":"#69C1CE","textColorMode":"auto"}'
    });

    expect(model).toEqual({
      styleName: 'solid',
      backgroundColor: '#69C1CE',
      textColorMode: 'auto',
      borderMode: 'auto',
      textColor: '#111827',
      borderColor: '#D8CDEC'
    });
  });

  it('supports historical gradient payloads that used backgroundColor only', () => {
    const model = deserializeStyle({
      styleName: 'gradient',
      stylePropertiesJson: '{"backgroundColor":"#69C1CE","textColorMode":"auto"}'
    });

    expect(model).toEqual({
      styleName: 'gradient',
      leftColor: '#69C1CE',
      rightColor: '#69C1CE',
      textColorMode: 'auto',
      borderMode: 'auto',
      textColor: '#111827',
      borderColor: '#D8CDEC'
    });
  });

  it('serializes presets with preset index only', () => {
    const json = serializeStyleModel({ styleName: 'presets', presetIndex: 4 });
    expect(json).toContain('"presetIndex":4');
    expect(json).not.toContain('backgroundColor');
  });
});
