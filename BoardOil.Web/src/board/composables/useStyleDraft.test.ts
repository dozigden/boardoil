import { describe, expect, it } from 'vitest';
import { useStyleDraft } from './useStyleDraft';
import type { StyleDraft } from '../../shared/utils/stylePresentation';

describe('useStyleDraft', () => {
  it('updates draft through shared mutators and produces style properties json', () => {
    const initialDraft: StyleDraft = {
      styleName: 'solid',
      textColorMode: 'auto',
      borderMode: 'auto',
      backgroundColor: '#69C1CE',
      leftColor: '#69C1CE',
      rightColor: '#69C1CE',
      textColor: '#111827',
      borderColor: '#D8CDEC'
    };

    const model = useStyleDraft(null);
    model.setStyleName('gradient');
    expect(model.draft.value).toBeNull();

    model.setDraft(initialDraft);
    model.setStyleName('gradient');
    model.setTextMode('custom');
    model.setBorderMode('custom');
    model.setField('leftColor', '#112233');
    model.setField('rightColor', '#445566');
    model.setField('textColor', '#FFFFFF');
    model.setField('borderColor', '#AABBCC');

    expect(model.draft.value?.styleName).toBe('gradient');
    expect(model.draft.value?.textColorMode).toBe('custom');
    expect(model.draft.value?.borderMode).toBe('custom');

    const json = model.stylePropertiesJson.value;
    expect(json).not.toBeNull();
    const parsed = JSON.parse(json ?? '{}') as Record<string, string>;
    expect(parsed).toEqual({
      leftColor: '#112233',
      rightColor: '#445566',
      textColorMode: 'custom',
      borderMode: 'custom',
      borderColor: '#AABBCC',
      textColor: '#FFFFFF'
    });

    model.clearDraft();
    expect(model.draft.value).toBeNull();
    expect(model.stylePropertiesJson.value).toBeNull();
  });
});
