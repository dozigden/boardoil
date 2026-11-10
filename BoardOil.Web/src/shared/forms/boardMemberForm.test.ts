import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { ref } from 'vue';
import { createBoardMemberForm } from './boardMemberForm';
import { useForm } from './useForm';
import { useValidationStore } from '../stores/validationStore';
import { err } from '../types/result';
import type { UserDirectoryEntry } from '../types/authTypes';

const user = (id: number): UserDirectoryEntry => ({ id, userName: `user-${id}`, displayName: `User ${id}`, isActive: true });

beforeEach(() => setActivePinia(createPinia()));

describe('board member form', () => {
  it('updates user choices without replacing the chosen user or role', () => {
    const users = ref([user(1), user(2)]);
    const definition = createBoardMemberForm({ users: () => users.value, canManage: () => true, isCurrent: () => true, add: vi.fn() });
    const form = useForm(definition);
    form.setValue('userId', 2);
    form.setValue('role', 'Owner');

    users.value = [user(3), user(2), user(1)];

    expect(form.draft.value).toEqual({ userId: 2, role: 'Owner' });
    const field = definition.fields[0]!;
    if (field.type !== 'select' || typeof field.options !== 'function') throw new Error('Expected live user options.');
    expect(field.options(form.draft.value).map(option => option.value)).toEqual([0, 3, 2, 1]);
  });

  it('lets the backend validate an empty user selection and keeps the rejected draft', async () => {
    const rejection = err({ message: 'Validation failed.', validationErrors: { userId: ['Select an active user.'] } });
    const add = vi.fn().mockResolvedValue(rejection);
    const form = useForm(createBoardMemberForm({ users: () => [], canManage: () => true, isCurrent: () => true, add }));

    expect(await form.submit()).toBe(rejection);
    expect(add).toHaveBeenCalledWith({ userId: 0, role: 'Contributor' });
    expect(form.draft.value).toEqual({ userId: 0, role: 'Contributor' });
  });

  it('blocks lost permissions and ignores a response after the board changes', async () => {
    const canManage = ref(false);
    const current = ref(true);
    const rejection = err({ message: 'Old board failure.' });
    let finish!: (result: typeof rejection) => void;
    const add = vi.fn(() => new Promise<typeof rejection>(resolve => { finish = resolve; }));
    const form = useForm(createBoardMemberForm({ users: () => [user(1)], canManage: () => canManage.value, isCurrent: () => current.value, add }));
    expect(await form.submit()).toBeUndefined();
    expect(add).not.toHaveBeenCalled();
    canManage.value = true;
    const pending = form.submit();
    expect(form.locked.value).toBe(true);
    current.value = false;
    finish(rejection);

    expect(await pending).toBeUndefined();
    expect(useValidationStore().validation).toBeNull();
  });
});
