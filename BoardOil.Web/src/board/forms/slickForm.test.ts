import { describe, expect, it, vi } from 'vitest';
import { createSlickForm } from './slickForm';
import { ok } from '../../shared/types/result';
import type { Slick, StyleDefault } from '../../shared/types/boardTypes';

const slick: Slick = {
  id: 7, name: 'Release', styleName: 'presets', stylePropertiesJson: '{"presetIndex":5}',
  createdAtUtc: '', updatedAtUtc: ''
};

function makeForm(existing: Slick | null, defaultStyle: StyleDefault | null = null) {
  const save = vi.fn().mockResolvedValue(ok(slick));
  return { save, form: createSlickForm(existing, { save, loadDefaultStyle: async () => defaultStyle, isCurrent: () => true }) };
}

it('uses the creation default and retains all editable solid properties when submitting', async () => {
  const { form, save } = makeForm(null, { styleName: 'presets', stylePropertiesJson: '{"presetIndex":8}' });
  const { values } = await form.reassess!(form.initialValues());
  expect(values.style).toMatchObject({ styleName: 'presets', presetIndex: 8 });
  values.name = '  Release  ';
  Object.assign(values.style, {
    styleName: 'solid', backgroundColor: '#123456', textColorMode: 'custom', textColor: '#ABCDEF',
    borderMode: 'custom', borderColor: '#654321'
  });
  await form.submit(values);
  expect(save).toHaveBeenCalledWith(expect.objectContaining({ name: '  Release  ', styleName: 'solid' }));
  expect(JSON.parse(save.mock.calls[0]![0].stylePropertiesJson)).toEqual({
    backgroundColor: '#123456', textColorMode: 'custom', textColor: '#ABCDEF', borderMode: 'custom', borderColor: '#654321'
  });
});

describe.each([
  { styleName: 'gradient', stylePropertiesJson: '{"leftColor":"#111111","rightColor":"#222222"}' },
  { styleName: 'presets', stylePropertiesJson: 'invalid JSON' }
])('invalid existing style $styleName / $stylePropertiesJson', invalid => {
  it('opens an allowed replacement draft and can save it', async () => {
    const { form, save } = makeForm({ ...slick, ...invalid } as Slick);
    const values = form.initialValues();
    expect(values.style).toMatchObject({ styleName: 'presets', presetIndex: 2 });
    await form.submit(values);
    expect(save).toHaveBeenCalledWith({ name: 'Release', styleName: 'presets', stylePropertiesJson: '{"presetIndex":2}' });
  });
});
