import { describe, expect, it, vi } from 'vitest';
import { createTagForm } from './tagForm';
import { ok } from '../../shared/types/result';
import type { Tag } from '../../shared/types/boardTypes';

const tag: Tag = {
  id: 7, name: 'Release', emoji: '🚀', styleName: 'presets', stylePropertiesJson: '{"presetIndex":5}',
  createdAtUtc: '', updatedAtUtc: ''
};

it('loads the creation colour and submits a complete gradient tag without losing custom properties', async () => {
  const save = vi.fn().mockResolvedValue(ok(tag));
  const form = createTagForm(null, {
    save, isCurrent: () => true,
    loadDefaultStyle: async () => ({ styleName: 'presets', stylePropertiesJson: '{"presetIndex":8}' })
  });
  const { values } = await form.reassess!(form.initialValues());
  expect(values.style).toMatchObject({ styleName: 'presets', presetIndex: 8 });
  values.name = '  Release  ';
  values.emoji = '🚀';
  Object.assign(values.style, {
    styleName: 'gradient', leftColor: '#123456', rightColor: '#654321',
    textColorMode: 'custom', textColor: '#ABCDEF', borderMode: 'custom', borderColor: '#FEDCBA'
  });

  await form.submit(values);

  expect(save).toHaveBeenCalledWith(expect.objectContaining({ name: '  Release  ', emoji: '🚀', styleName: 'gradient' }));
  expect(JSON.parse(save.mock.calls[0]![0].stylePropertiesJson)).toEqual({
    leftColor: '#123456', rightColor: '#654321', textColorMode: 'custom', textColor: '#ABCDEF',
    borderMode: 'custom', borderColor: '#FEDCBA'
  });
});

describe.each(['not JSON', '{"backgroundColor":"invalid"}'])('existing invalid style %s', stylePropertiesJson => {
  it('opens a replacement draft and saves valid auto styling', async () => {
    const save = vi.fn().mockResolvedValue(ok(tag));
    const form = createTagForm({ ...tag, styleName: 'solid', stylePropertiesJson }, {
      save, isCurrent: () => true, loadDefaultStyle: vi.fn()
    });

    await form.submit(form.initialValues());

    expect(save).toHaveBeenCalledWith({ name: 'Release', emoji: '🚀', styleName: 'auto', stylePropertiesJson: '{}' });
  });
});
