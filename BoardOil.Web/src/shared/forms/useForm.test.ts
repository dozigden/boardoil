import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useValidationStore } from '../stores/validationStore';
import { err, ok, type Result } from '../types/result';
import { defineForm, type FormError } from './formDefinition';
import { useForm } from './useForm';

describe('form submission lifecycle', () => {
  beforeEach(() => setActivePinia(createPinia()));

  function definition() {
    return defineForm<{ name: string }, number>({
      title: 'Example', submitLabel: 'Save', category: 'example',
      initialValues: () => ({ name: 'Initial' }),
      fields: [{ name: 'name', label: 'Name', type: 'text' }],
      validate: values => values.name.trim() ? {} : { name: ['Name is required.'] },
      submit: vi.fn(async () => ok(42))
    });
  }

  it('retains the entered draft when local validation rejects submission', async () => {
    const config = definition();
    const form = useForm(config);
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'Previous result' }));
    form.setValue('name', '   ');
    expect((await form.submit())?.ok).toBe(false);
    expect(config.submit).not.toHaveBeenCalled();
    expect(form.draft.value.name).toBe('   ');
    expect(form.busy.value).toBe(false);
    expect(form.dirty.value).toBe(true);
    expect(validation.getFieldErrors('name', 'example')).toEqual(['Name is required.']);
    expect(validation.getFieldErrors('name', 'other')).toEqual([]);
  });

  it('clears feedback on retry, blocks concurrent submission and retains failures until a successful retry', async () => {
    const config = definition();
    let finish!: (result: Result<number, FormError>) => void;
    config.submit = vi.fn(() => new Promise<Result<number, FormError>>(resolve => { finish = resolve; }));
    const form = useForm(config);
    const validation = useValidationStore();
    form.setValue('name', ' Edited ');
    const failure = err({ message: 'Cannot save', validationErrors: { futureField: ['Unavailable'] } });
    validation.setValidationResult(failure);
    const pending = form.submit();
    expect(form.busy.value).toBe(true);
    expect(validation.validation).toBeNull();
    expect(await form.submit()).toBeUndefined();
    form.setValue('name', 'Ignored while busy');
    form.reset();
    expect(form.draft.value.name).toBe(' Edited ');
    expect(config.submit).toHaveBeenCalledTimes(1);
    expect(config.submit).toHaveBeenCalledWith({ name: ' Edited ' });
    finish(failure);
    expect(await pending).toBe(failure);
    expect(form.dirty.value).toBe(true);
    expect(validation.getValidationMessages(['name'], 'example')).toEqual(['Unavailable']);

    const retry = form.submit();
    expect(validation.validation).toBeNull();
    finish(ok(42));
    expect(await retry).toEqual(ok(42));
    expect(form.busy.value).toBe(false);
    expect(form.dirty.value).toBe(false);
    expect(validation.validation).toBeNull();
  });

  it('resets drafts and errors to fresh initial values', async () => {
    const form = useForm(definition());
    form.setValue('name', '');
    await form.submit();
    form.reset();
    expect(form.draft.value).toEqual({ name: 'Initial' });
    expect(form.dirty.value).toBe(false);
    expect(useValidationStore().validation).toBeNull();
  });

  it('adopts the saved response as the draft and reset baseline, retaining it after rejected edits', async () => {
    const config = definition();
    config.savedValues = value => ({ name: `Saved ${value}` });
    const form = useForm(config);
    form.setValue('name', 'Submitted');
    await form.submit();
    expect(form.draft.value).toEqual({ name: 'Saved 42' });
    expect(form.dirty.value).toBe(false);
    form.setValue('name', 'Rejected edit');
    config.submit = vi.fn().mockResolvedValue(err({ message: 'Cannot save' }));
    await form.submit();
    expect(form.draft.value.name).toBe('Rejected edit');
    expect(form.dirty.value).toBe(true);
    form.reset();
    expect(form.draft.value.name).toBe('Saved 42');
    expect(form.dirty.value).toBe(false);
  });

  it('releases busy state if an unexpected submit exception escapes', async () => {
    const config = definition();
    config.submit = vi.fn().mockRejectedValue(new Error('Unexpected'));
    const form = useForm(config);
    await expect(form.submit()).rejects.toThrow('Unexpected');
    expect(form.busy.value).toBe(false);
  });

  it('isolates compound drafts, tracks edits and clears the API keys represented by the corrected control', async () => {
    const initial = { style: { colour: 'red' }, emoji: null as string | null };
    const config = defineForm({
      title: 'Compound', submitLabel: 'Save', initialValues: () => initial,
      fields: [
        { name: 'style', label: 'Style', type: 'custom', component: {}, errorFields: ['styleName', 'stylePropertiesJson'] },
        { name: 'emoji', label: 'Emoji', type: 'custom', component: {} }
      ],
      submit: vi.fn(async (values: typeof initial) => {
        values.style.colour = 'Changed by submit callback';
        return err({ message: 'Cannot save' });
      })
    });
    const form = useForm(config);
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'Invalid', validationErrors: {
      styleName: ['Choose a style'], stylePropertiesJson: ['Invalid colour'], emoji: ['Choose an emoji']
    } }));
    form.setValue('style', { colour: 'blue' });
    expect(initial.style.colour).toBe('red');
    expect(form.dirty.value).toBe(true);
    expect(validation.getFieldErrors('styleName')).toEqual([]);
    expect(validation.getFieldErrors('stylePropertiesJson')).toEqual([]);
    expect(validation.getFieldErrors('emoji')).toEqual(['Choose an emoji']);
    await form.submit();
    expect(form.draft.value.style.colour).toBe('blue');
    form.reset();
    expect(form.draft.value.style.colour).toBe('red');
    expect(form.dirty.value).toBe(false);
  });

  it.each([true, false])('suppresses a late result after the owning context changes (success=%s)', async success => {
    const config = definition();
    let current = true;
    config.isCurrent = () => current;
    config.savedValues = vi.fn(() => ({ name: 'Old response' }));
    let finish!: (result: Result<number, FormError>) => void;
    config.submit = () => new Promise(resolve => { finish = resolve; });
    const form = useForm(config);
    const pending = form.submit();
    current = false;
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'New board feedback' }));
    finish(success ? ok(42) : err({ message: 'Old board failure' }));
    expect(await pending).toBeUndefined();
    expect(validation.getValidationMessages()).toEqual(['New board feedback']);
    expect(config.savedValues).not.toHaveBeenCalled();
    expect(form.draft.value.name).toBe('Initial');
    expect(form.busy.value).toBe(false);
  });

  it('cancels secondary actions without erasing feedback, then permits deletion despite an invalid draft', async () => {
    const config = definition();
    const form = useForm(config);
    const validation = useValidationStore();
    form.setValue('name', '');
    await form.submit();
    const action = { name: 'delete', label: 'Delete', confirm: vi.fn().mockResolvedValue(false), run: vi.fn().mockResolvedValue(ok(undefined)) };
    expect(await form.runAction(action)).toBeUndefined();
    expect(action.run).not.toHaveBeenCalled();
    expect(validation.getFieldErrors('name', 'example')).toEqual(['Name is required.']);
    action.confirm.mockResolvedValue(true);
    expect(await form.runAction(action)).toEqual(ok(undefined));
    expect(action.run).toHaveBeenCalledOnce();
    expect(validation.validation).toBeNull();
  });

  it('rechecks the owning context after secondary-action confirmation', async () => {
    const config = definition();
    let current = true;
    config.isCurrent = () => current;
    const action = {
      name: 'delete', label: 'Delete',
      confirm: async () => { current = false; return true; },
      run: vi.fn().mockResolvedValue(ok(undefined))
    };
    expect(await useForm(config).runAction(action)).toBeUndefined();
    expect(action.run).not.toHaveBeenCalled();
  });
});
