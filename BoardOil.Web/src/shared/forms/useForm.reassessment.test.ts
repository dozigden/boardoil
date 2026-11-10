import { createPinia, setActivePinia } from 'pinia';
import { effectScope } from 'vue';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useValidationStore } from '../stores/validationStore';
import { err, ok } from '../types/result';
import { defineForm, type FormAssessment } from './formDefinition';
import { useForm } from './useForm';

type Draft = { board: number | null; column: number | null; name: string };
function definition() {
  return defineForm<Draft, number>({
    title: 'Reassessment', submitLabel: 'Save',
    initialValues: () => ({ board: null, column: null, name: 'Initial' }),
    fields: [
      { name: 'board', label: 'Board', type: 'select', options: [], reassessOnChange: true, clearFields: ['column'] },
      { name: 'column', label: 'Column', type: 'select', options: [] },
      { name: 'name', label: 'Name', type: 'text' }
    ],
    reassess: vi.fn((values: Draft) => ({ values: { ...values, column: values.board } })),
    submit: vi.fn(async () => ok(42))
  });
}
function pendingAssessment() {
  let finish!: (value: FormAssessment<Draft>) => void;
  const promise = new Promise<FormAssessment<Draft>>(resolve => { finish = resolve; });
  return { promise, finish };
}

describe('form reassessment', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('runs initially and on flagged edits, without recursing when it applies returned values', async () => {
    const config = definition();
    config.reassess = vi.fn(values => ({ values: { ...values, board: values.board ?? 1, column: values.board ?? 1 } }));
    const form = useForm(config);
    await Promise.resolve();
    expect(config.reassess).toHaveBeenCalledOnce();
    expect(form.draft.value).toEqual({ board: 1, column: 1, name: 'Initial' });
    expect(form.dirty.value).toBe(false);
    form.setValue('name', 'Edited');
    form.setValue('board', 1);
    expect(config.reassess).toHaveBeenCalledOnce();
    form.setValue('board', 2);
    await Promise.resolve();
    expect(config.reassess).toHaveBeenCalledTimes(2);
    expect(form.draft.value).toEqual({ board: 2, column: 2, name: 'Edited' });
    expect(form.dirty.value).toBe(true);
    form.reset();
    await Promise.resolve();
    expect(form.draft.value).toEqual({ board: 1, column: 1, name: 'Initial' });
    expect(form.dirty.value).toBe(false);
  });

  it('blocks edits, reset, submissions, secondary actions and another reassessment while loading', async () => {
    const config = definition();
    const form = useForm(config);
    await Promise.resolve();
    const pending = pendingAssessment();
    config.reassess = vi.fn(() => pending.promise);
    form.setValue('board', 2);
    expect(form.locked.value).toBe(true);
    expect(form.canSubmit.value).toBe(false);
    expect(form.busy.value).toBe(false);
    form.setValue('board', 3);
    form.setValue('name', 'Ignored');
    form.reset();
    await form.reassess();
    await form.submit();
    const action = { name: 'delete', label: 'Delete', run: vi.fn(async () => ok(undefined)) };
    await form.runAction(action);
    expect(config.submit).not.toHaveBeenCalled();
    expect(action.run).not.toHaveBeenCalled();
    expect(config.reassess).toHaveBeenCalledOnce();
    expect(form.draft.value).toEqual({ board: 2, column: null, name: 'Initial' });
    const fields = config.fields.map(field => field.name === 'name' ? { ...field, label: 'Updated name' } : field);
    pending.finish({ values: { board: 2, column: 9, name: 'Initial' }, fields });
    await pending.promise;
    expect(form.fields.value).toBe(fields);
    expect(form.draft.value.column).toBe(9);
    expect(form.locked.value).toBe(false);
    expect(form.canSubmit.value).toBe(true);
  });

  it('keeps lookup failure separate from submission validation and permits retry', async () => {
    const config = definition();
    const form = useForm(config);
    await Promise.resolve();
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'Invalid', validationErrors: { name: ['Name rejected'] } }));
    config.reassess = vi.fn(values => ({ values: { ...values, column: null }, error: 'Columns unavailable' }));
    form.setValue('board', 2);
    await Promise.resolve();
    expect(form.reassessmentError.value).toBe('Columns unavailable');
    expect(form.canSubmit.value).toBe(false);
    expect(form.locked.value).toBe(false);
    expect(validation.getFieldErrors('name')).toEqual(['Name rejected']);
    config.reassess = vi.fn(values => ({ values: { ...values, column: 9 } }));
    await form.reassess();
    expect(form.reassessmentError.value).toBe('');
    expect(form.draft.value.column).toBe(9);
    expect(form.canSubmit.value).toBe(true);
    expect(validation.getFieldErrors('name')).toEqual(['Name rejected']);
  });

  it.each(['disposal', 'board change'])('does not apply a lookup response after %s', async change => {
    const config = definition();
    let current = true;
    config.isCurrent = () => current;
    const scope = effectScope();
    const form = scope.run(() => useForm(config))!;
    await Promise.resolve();
    const pending = pendingAssessment();
    config.reassess = () => pending.promise;
    form.setValue('board', 2);
    if (change === 'disposal') scope.stop();
    else current = false;
    pending.finish({ values: { board: 2, column: 9, name: 'Old response' }, error: 'Old error' });
    await pending.promise;
    expect(form.draft.value).toEqual({ board: 2, column: null, name: 'Initial' });
    expect(form.reassessmentError.value).toBe('');
    scope.stop();
  });
});
