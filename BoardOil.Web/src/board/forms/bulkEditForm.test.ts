import { beforeEach, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { ref } from 'vue';
import { createBulkEditForm } from './bulkEditForm';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';
import { err, ok } from '../../shared/types/result';

vi.mock('../components/BulkTagFormControl.vue', () => ({ default: {} }));

beforeEach(() => setActivePinia(createPinia()));

function setup() {
  const count = ref(2);
  const save = vi.fn().mockResolvedValue(ok([]));
  const form = useForm(createBulkEditForm({
    columns: () => [{ id: 2, title: 'Doing' }], slicks: () => [{ name: 'Release' }],
    tagNames: () => ['Feature', 'Bug'], selectedCount: () => count.value,
    save, isCurrent: () => true
  }));
  return { form, count, save, validation: useValidationStore() };
}

it('submits column and tag changes without changing slick membership', async () => {
  const { form, save } = setup();
  await Promise.resolve();
  expect(form.canSubmit.value).toBe(false);
  form.setValue('targetColumnId', 2);
  form.setValue('tagStates', { feature: 'include', bug: 'exclude' });

  await form.submit();

  expect(save).toHaveBeenCalledWith({ moveTargetColumnId: 2, addTagNames: ['Feature'], removeTagNames: ['Bug'] });
});

it('requires a slick for Set and keeps set, clear and unchanged operations distinct', async () => {
  const { form, save, validation } = setup();
  await Promise.resolve();
  form.setValue('targetColumnId', 2);
  form.setValue('slickOperation', 'set');
  await Promise.resolve();
  expect(form.fields.value.some(field => field.name === 'targetSlickName')).toBe(true);

  await form.submit();
  expect(save).not.toHaveBeenCalled();
  expect(validation.getFieldErrors('targetSlickName')).toEqual(['Select a slick.']);
  form.setValue('targetSlickName', 'Release');
  await form.submit();
  expect(save).toHaveBeenLastCalledWith({ moveTargetColumnId: 2, addTagNames: [], removeTagNames: [], slickName: 'Release' });

  form.setValue('slickOperation', 'clear');
  await Promise.resolve();
  expect(form.fields.value.some(field => field.name === 'targetSlickName')).toBe(false);
  expect(form.draft.value.targetSlickName).toBeNull();
  await form.submit();
  expect(save).toHaveBeenLastCalledWith({ moveTargetColumnId: 2, addTagNames: [], removeTagNames: [], slickName: null });

  form.setValue('slickOperation', 'none');
  await Promise.resolve();
  await form.submit();
  expect(save).toHaveBeenLastCalledWith({ moveTargetColumnId: 2, addTagNames: [], removeTagNames: [] });
});

it('retains failed edits, clears mapped errors on correction and prevents submission after selection disappears', async () => {
  const { form, save, count, validation } = setup();
  await Promise.resolve();
  form.setValue('targetColumnId', 2);
  form.setValue('tagStates', { feature: 'include' });
  save.mockResolvedValueOnce(err({ message: 'Validation failed.', validationErrors: { 'move.targetColumnId': ['Choose another column.'] } }));

  await form.submit();

  expect(form.draft.value).toMatchObject({ targetColumnId: 2, tagStates: { feature: 'include' } });
  expect(validation.getFieldErrors('move.targetColumnId')).toEqual(['Choose another column.']);
  form.setValue('targetColumnId', null);
  expect(validation.getFieldErrors('move.targetColumnId')).toEqual([]);
  count.value = 0;
  await form.submit();
  expect(save).toHaveBeenCalledTimes(1);
  count.value = 1;
  await form.submit();
  expect(save).toHaveBeenLastCalledWith({ moveTargetColumnId: null, addTagNames: ['Feature'], removeTagNames: [] });
});
