import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';
import { err, ok } from '../../shared/types/result';
import { createBoardForm } from './boardCreateForm';

function setup() {
  const board = { id: 4, name: 'Created board', description: '', slickCohesionModeEnabled: true,
    cardAttachmentThumbnailsEnabled: true, currentUserRole: 'Owner', createdAtUtc: '', updatedAtUtc: '' };
  const operations = { boards: () => [board], create: vi.fn().mockResolvedValue(ok(board)),
    clone: vi.fn().mockResolvedValue(ok(board)), import: vi.fn().mockResolvedValue(ok(board)) };
  return { form: useForm(createBoardForm(operations)), operations };
}

describe('board creation form', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('retains hidden values while changing fields, clears old operation feedback and submits only the chosen mode', async () => {
    const { form, operations } = setup();
    await Promise.resolve();
    form.setValue('name', 'Blank name');
    form.setValue('description', 'Keep this description');
    const validation = useValidationStore();
    validation.setValidationResult(err({ message: 'Old general failure' }));
    form.setValue('mode', 'package');
    await Promise.resolve();
    expect(form.fields.value.map(field => field.name)).toEqual(['mode', 'file', 'nameOverride']);
    expect(validation.validation).toBeNull();
    const file = new File(['package'], 'board.zip');
    form.setValue('file', file);
    form.setValue('nameOverride', ' Imported name ');
    form.setValue('mode', 'clone');
    await Promise.resolve();
    expect(form.canSubmit.value).toBe(false);
    form.setValue('sourceBoardId', 4);
    expect(form.fields.value.map(field => field.name)).toEqual(['mode', 'sourceBoardId', 'name']);
    await form.submit();
    expect(operations.clone).toHaveBeenCalledWith(4, 'Blank name');
    expect(operations.create).not.toHaveBeenCalled();
    expect(operations.import).not.toHaveBeenCalled();
    form.setValue('mode', 'blank');
    await Promise.resolve();
    expect(form.draft.value.description).toBe('Keep this description');
    form.setValue('mode', 'package');
    await Promise.resolve();
    expect(form.draft.value.file).toBe(file);
    await form.submit();
    expect(operations.import).toHaveBeenCalledWith(file, 'Imported name');
  });

  it('distinguishes files with identical metadata, retains rejected selections and restores the saved file on reset', async () => {
    const { form, operations } = setup();
    await Promise.resolve();
    form.setValue('mode', 'package');
    await Promise.resolve();
    const first = new File(['one'], 'same.zip', { type: 'application/zip', lastModified: 123 });
    const replacement = new File(['two'], 'same.zip', { type: 'application/zip', lastModified: 123 });
    form.setValue('file', first);
    await form.submit();
    expect(form.dirty.value).toBe(false);
    form.setValue('file', replacement);
    expect(form.dirty.value).toBe(true);
    operations.import.mockResolvedValueOnce(err({ message: 'Invalid package', validationErrors: { file: ['Invalid ZIP'] } }));
    await form.submit();
    expect(operations.import).toHaveBeenLastCalledWith(replacement, undefined);
    expect(form.draft.value.file).toBe(replacement);
    expect(form.dirty.value).toBe(true);
    form.reset();
    await Promise.resolve();
    expect(form.draft.value.file).toBe(first);
    expect(form.draft.value.file?.name).toBe('same.zip');
    expect(await form.draft.value.file?.text()).toBe('one');
    expect(form.dirty.value).toBe(false);
    form.setValue('file', null);
    expect(form.dirty.value).toBe(true);
    form.reset();
    await Promise.resolve();
    expect(form.draft.value.file).toBe(first);
  });

  it('sends empty names and missing files to backend validation', async () => {
    const { form, operations } = setup();
    await Promise.resolve();
    await form.submit();
    expect(operations.create).toHaveBeenCalledWith('', '');
    form.setValue('mode', 'package');
    await Promise.resolve();
    await form.submit();
    expect(operations.import).toHaveBeenCalledWith(null, undefined);
  });
});
