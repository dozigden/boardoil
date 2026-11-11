import { describe, expect, it } from 'vitest';
import { shallowRef } from 'vue';
import { useStyleDraft } from './useStyleDraft';
import type { StyleDraft } from '../utils/styleTypes';

describe('useStyleDraft', () => {
  it('replaces the supplied draft on edits and resets style-specific values when switching modes', () => {
    const initialDraft: StyleDraft = {
      styleName: 'solid',
      textColorMode: 'auto',
      borderMode: 'auto',
      presetIndex: 2,
      backgroundColor: '#69C1CE',
      leftColor: '#69C1CE',
      rightColor: '#69C1CE',
      textColor: '#111827',
      borderColor: '#D8CDEC'
    };

    const draft = shallowRef(initialDraft);
    const model = useStyleDraft(draft);
    model.setStyleName('gradient');
    model.setTextMode('custom');
    model.setBorderMode('custom');
    model.setField('leftColor', '#112233');
    model.setField('rightColor', '#445566');
    model.setField('textColor', '#FFFFFF');
    model.setField('borderColor', '#AABBCC');

    expect(initialDraft.styleName).toBe('solid');
    expect(initialDraft.leftColor).toBe('#69C1CE');
    expect(draft.value).toEqual({
      ...initialDraft,
      styleName: 'gradient',
      leftColor: '#112233',
      rightColor: '#445566',
      textColorMode: 'custom',
      borderMode: 'custom',
      borderColor: '#AABBCC',
      textColor: '#FFFFFF'
    });

    model.setStyleName('presets');
    expect(draft.value).toEqual({ ...initialDraft, styleName: 'presets' });
  });
});
