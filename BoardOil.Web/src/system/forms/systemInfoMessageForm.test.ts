import { beforeEach, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { systemInfoMessageForm } from './systemInfoMessageForm';
import { useForm } from '../../shared/forms/useForm';
import { ok } from '../../shared/types/result';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';

beforeEach(() => setActivePinia(createPinia()));

const message: SystemInfoMessageDto = {
  enabled: true, emoji: null, title: 'Maintenance', description: 'Details',
  styleName: 'presets', stylePropertiesJson: '{"presetIndex":7}'
};

it('starts an unconfigured message disabled with the default preset', () => {
  const form = systemInfoMessageForm(null, vi.fn());
  expect(form.initialValues()).toMatchObject({ enabled: false, emoji: null, title: '', description: '', style: { styleName: 'presets', presetIndex: 2 } });
});

it('retains supported style properties and adopts the saved response as a clean draft', async () => {
  const persisted = { ...message, title: 'Saved', styleName: 'solid' as const,
    stylePropertiesJson: '{"backgroundColor":"#123456","textColorMode":"custom","textColor":"#ABCDEF","borderMode":"none"}' };
  const save = vi.fn().mockResolvedValue(ok(persisted));
  const form = useForm(systemInfoMessageForm(message, save));
  form.setValue('title', '  Saved  ');
  form.setValue('style', { ...form.draft.value.style, styleName: 'solid', backgroundColor: '#123456', textColorMode: 'custom', textColor: '#ABCDEF', borderMode: 'none' });
  await form.submit();
  expect(save).toHaveBeenCalledWith(expect.objectContaining({ title: 'Saved', description: 'Details', styleName: 'solid' }));
  expect(JSON.parse(save.mock.calls[0]![0].stylePropertiesJson)).toEqual(JSON.parse(persisted.stylePropertiesJson));
  expect(form.draft.value.title).toBe('Saved');
  expect(form.draft.value.style).toMatchObject({ styleName: 'solid', backgroundColor: '#123456', textColorMode: 'custom', textColor: '#ABCDEF', borderMode: 'none' });
  expect(form.dirty.value).toBe(false);
});

it.each([
  { styleName: 'presets', stylePropertiesJson: 'broken JSON' },
  { styleName: 'gradient', stylePropertiesJson: '{"leftColor":"#111111","rightColor":"#222222"}' }
])('opens invalid saved styles with an allowed replacement: $styleName', async invalid => {
  const save = vi.fn().mockResolvedValue(ok(message));
  const form = systemInfoMessageForm({ ...message, ...invalid } as SystemInfoMessageDto, save);
  const draft = form.initialValues();
  expect(draft.style.styleName).toBe('auto');
  await form.submit(draft);
  expect(save).toHaveBeenCalledWith(expect.objectContaining({ styleName: 'auto', stylePropertiesJson: '{}' }));
});
