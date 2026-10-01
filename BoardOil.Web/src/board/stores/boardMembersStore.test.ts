import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardMembersStore } from './boardMembersStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok, type Result } from '../../shared/types/result';
import type { BoardMember } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';

const api = {
  getBoardMembers: vi.fn(),
  addBoardMember: vi.fn(),
  updateBoardMemberRole: vi.fn(),
  removeBoardMember: vi.fn()
};

vi.mock('../../shared/api/boardApi', () => ({
  createBoardApi: () => api
}));

describe('boardMembersStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    api.getBoardMembers.mockResolvedValue(ok([]));
    api.addBoardMember.mockResolvedValue(ok(makeMember(7, 'A User', 'a.user', 'Contributor')));
    api.updateBoardMemberRole.mockResolvedValue(ok(makeMember(7, 'A User', 'a.user', 'Owner')));
    api.removeBoardMember.mockResolvedValue(ok(undefined));
  });

  it('loads board members for the selected board', async () => {
    const store = useBoardMembersStore();
    api.getBoardMembers.mockResolvedValueOnce(ok([makeMember(7, 'A User', 'a.user', 'Contributor')]));

    const loaded = await store.loadMembers(3);

    expect(loaded).toBe(true);
    expect(store.activeBoardId).toBe(3);
    expect(api.getBoardMembers).toHaveBeenCalledWith(3);
    expect(store.members.map(x => x.userName)).toEqual(['a.user']);
  });

  it('ignores stale loadMembers responses when board changes mid-load', async () => {
    const store = useBoardMembersStore();
    const firstRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeMember>[] }>();
    const secondRequest = createDeferred<{ ok: true; data: ReturnType<typeof makeMember>[] }>();

    api.getBoardMembers
      .mockImplementationOnce(() => firstRequest.promise)
      .mockImplementationOnce(() => secondRequest.promise);

    const firstLoad = store.loadMembers(1);
    const secondLoad = store.loadMembers(2);

    secondRequest.resolve({ ok: true, data: [makeMember(20, 'Second Board User', 'second.user', 'Contributor')] });
    await secondLoad;

    firstRequest.resolve({ ok: true, data: [makeMember(10, 'First Board User', 'first.user', 'Contributor')] });
    await firstLoad;

    expect(store.activeBoardId).toBe(2);
    expect(store.members.map(x => x.userName)).toEqual(['second.user']);
  });

  it('passes board id to member mutations', async () => {
    const store = useBoardMembersStore();
    const added = await store.addMember(3, { userId: 7, role: 'Contributor' });
    await store.updateMemberRole(3, { userId: 7, role: 'Owner' });
    await store.deleteMember(3, 7);

    expect(added?.userId).toBe(7);
    expect(api.addBoardMember).toHaveBeenCalledWith(3, { userId: 7, role: 'Contributor' });
    expect(api.updateBoardMemberRole).toHaveBeenCalledWith(3, { userId: 7, role: 'Owner' });
    expect(api.removeBoardMember).toHaveBeenCalledWith(3, 7);
  });

  it.each([false, true])('applies the added member response without reloading (already listed: %s)', async alreadyListed => {
    const store = useBoardMembersStore();
    const existingMembers = [makeMember(8, 'Z User', 'z.user', 'Owner')];
    if (alreadyListed) {
      existingMembers.push(makeMember(7, 'Old Name', 'a.user', 'Contributor'));
    }
    api.getBoardMembers.mockResolvedValueOnce(ok(existingMembers));
    await store.loadMembers(3);
    const returnedMember = makeMember(7, 'A User', 'a.user', 'Owner');
    api.addBoardMember.mockResolvedValueOnce(ok(returnedMember));

    const added = await store.addMember(3, { userId: 7, role: 'Owner' });

    expect(added).toEqual(returnedMember);
    expect(store.members).toEqual([returnedMember, existingMembers[0]]);
    expect(store.activeBoardId).toBe(3);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it('does not apply an added member or reload the old board after switching boards', async () => {
    const store = useBoardMembersStore();
    await store.loadMembers(1);
    const request = createDeferred<{ ok: true; data: ReturnType<typeof makeMember> }>();
    api.addBoardMember.mockReturnValueOnce(request.promise);
    const adding = store.addMember(1, { userId: 7, role: 'Contributor' });
    const otherMember = makeMember(20, 'Other Board User', 'other.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([otherMember]));
    await store.loadMembers(2);

    const returnedMember = makeMember(7, 'A User', 'a.user', 'Contributor');
    request.resolve({ ok: true, data: returnedMember });

    expect(await adding).toEqual(returnedMember);
    expect(store.activeBoardId).toBe(2);
    expect(store.members).toEqual([otherMember]);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(2);
  });

  it('does not repopulate a disposed store when an addition completes', async () => {
    const store = useBoardMembersStore();
    await store.loadMembers(3);
    const request = createDeferred<{ ok: true; data: ReturnType<typeof makeMember> }>();
    api.addBoardMember.mockReturnValueOnce(request.promise);
    const adding = store.addMember(3, { userId: 7, role: 'Contributor' });
    store.dispose();

    const returnedMember = makeMember(7, 'A User', 'a.user', 'Contributor');
    request.resolve({ ok: true, data: returnedMember });

    expect(await adding).toEqual(returnedMember);
    expect(store.activeBoardId).toBeNull();
    expect(store.members).toEqual([]);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it('preserves members and reports a failed addition without reloading', async () => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    const existingMember = makeMember(8, 'Existing User', 'existing.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([existingMember]));
    await store.loadMembers(3);
    api.addBoardMember.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not add member.' }));

    const added = await store.addMember(3, { userId: 7, role: 'Contributor' });

    expect(added).toBeNull();
    expect(store.members).toEqual([existingMember]);
    expect(feedback.errorMessage).toBe('Could not add member.');
    expect(store.busy).toBe(false);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it('applies a role update response without reloading or changing other members', async () => {
    const store = useBoardMembersStore();
    const otherMember = makeMember(8, 'Z User', 'z.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([otherMember, makeMember(7, 'A User', 'a.user', 'Contributor')]));
    await store.loadMembers(3);
    const returnedMember = makeMember(7, 'A User', 'a.user', 'Owner');
    api.updateBoardMemberRole.mockResolvedValueOnce(ok(returnedMember));

    expect(await store.updateMemberRole(3, { userId: 7, role: 'Owner' })).toEqual(returnedMember);
    expect(store.members).toEqual([returnedMember, otherMember]);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it('retains the existing role and error when a role update fails', async () => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    const owner = makeMember(7, 'A User', 'a.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([owner]));
    await store.loadMembers(3);
    api.updateBoardMemberRole.mockResolvedValueOnce(err({ kind: 'api', message: 'Board must have at least one owner.' }));

    expect(await store.updateMemberRole(3, { userId: 7, role: 'Contributor' })).toBeNull();
    expect(store.members).toEqual([owner]);
    expect(feedback.errorMessage).toBe('Board must have at least one owner.');
    expect(store.busy).toBe(false);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it.each(['switch', 'dispose'])('does not apply a late role update after %s', async transition => {
    const store = useBoardMembersStore();
    const member = makeMember(7, 'A User', 'a.user', 'Contributor');
    api.getBoardMembers.mockResolvedValue(ok([member]));
    await store.loadMembers(1);
    const request = createDeferred<{ ok: true; data: ReturnType<typeof makeMember> }>();
    api.updateBoardMemberRole.mockReturnValueOnce(request.promise);
    const updating = store.updateMemberRole(1, { userId: 7, role: 'Owner' });
    if (transition === 'switch') {
      await store.loadMembers(2);
    } else {
      store.dispose();
    }
    const currentBoardId = store.activeBoardId;
    const currentMembers = [...store.members];
    const loadCount = api.getBoardMembers.mock.calls.length;
    const returnedMember = makeMember(7, 'A User', 'a.user', 'Owner');
    request.resolve({ ok: true, data: returnedMember });

    expect(await updating).toEqual(returnedMember);
    expect(store.activeBoardId).toBe(currentBoardId);
    expect(store.members).toEqual(currentMembers);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(loadCount);
  });

  it('removes only the requested member without reloading', async () => {
    const store = useBoardMembersStore();
    const otherMember = makeMember(8, 'Z User', 'z.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([makeMember(7, 'A User', 'a.user', 'Contributor'), otherMember]));
    await store.loadMembers(3);

    expect(await store.deleteMember(3, 7)).toBe(true);
    expect(store.members).toEqual([otherMember]);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it('retains members and reports the error when removal fails', async () => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    const owner = makeMember(7, 'A User', 'a.user', 'Owner');
    api.getBoardMembers.mockResolvedValueOnce(ok([owner]));
    await store.loadMembers(3);
    api.removeBoardMember.mockResolvedValueOnce(err({ kind: 'api', message: 'Board must have at least one owner.' }));

    expect(await store.deleteMember(3, 7)).toBe(false);
    expect(store.members).toEqual([owner]);
    expect(feedback.errorMessage).toBe('Board must have at least one owner.');
    expect(store.busy).toBe(false);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(1);
  });

  it.each(['switch', 'dispose'])('does not apply a late removal after %s', async transition => {
    const store = useBoardMembersStore();
    api.getBoardMembers.mockResolvedValue(ok([makeMember(7, 'A User', 'a.user', 'Contributor')]));
    await store.loadMembers(1);
    const request = createDeferred<{ ok: true; data: undefined }>();
    api.removeBoardMember.mockReturnValueOnce(request.promise);
    const removing = store.deleteMember(1, 7);
    if (transition === 'switch') {
      await store.loadMembers(2);
    } else {
      store.dispose();
    }
    const currentBoardId = store.activeBoardId;
    const currentMembers = [...store.members];
    const loadCount = api.getBoardMembers.mock.calls.length;
    request.resolve({ ok: true, data: undefined });

    expect(await removing).toBe(true);
    expect(store.activeBoardId).toBe(currentBoardId);
    expect(store.members).toEqual(currentMembers);
    expect(api.getBoardMembers).toHaveBeenCalledTimes(loadCount);
  });

  it('reports API errors during loads', async () => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    api.getBoardMembers.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load members.' }));

    const loaded = await store.loadMembers(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load members.');
    expect(store.activeBoardId).toBeNull();
    expect(store.busy).toBe(false);
  });

  it('can load members again after a failed lookup clears its context', async () => {
    const store = useBoardMembersStore();
    api.getBoardMembers.mockResolvedValueOnce(err({ kind: 'api', message: 'Unavailable.' }));
    await store.loadMembers(3);
    expect(store.activeBoardId).toBeNull();
    const member = makeMember(7, 'A User', 'a.user', 'Contributor');
    api.getBoardMembers.mockResolvedValueOnce(ok([member]));

    expect(await store.loadMembers(3)).toBe(true);
    expect(store.activeBoardId).toBe(3);
    expect(store.members).toEqual([member]);
    expect(useUiFeedbackStore().errorMessage).toBe('');
  });

  it.each(['switch', 'dispose'])('ignores late load success and failure after %s', async transition => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    const success = createDeferred<Result<BoardMember[], AppError>>();
    const failure = createDeferred<Result<never, AppError>>();
    api.getBoardMembers.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
    const successfulLoad = store.loadMembers(1);
    const failedLoad = store.loadMembers(1);
    const currentLoad = createDeferred<Result<BoardMember[], AppError>>();
    let loading: Promise<boolean> | undefined;
    if (transition === 'switch') {
      api.getBoardMembers.mockReturnValueOnce(currentLoad.promise);
      loading = store.loadMembers(2);
    } else {
      store.dispose();
    }
    feedback.setError('Current error.');
    success.resolve(ok([makeMember(7, 'Old User', 'old.user', 'Owner')]));
    failure.resolve(err({ kind: 'api', message: 'Old error.' }));

    expect(await successfulLoad).toBe(false);
    expect(await failedLoad).toBe(false);
    expect(store.members).toEqual([]);
    expect(feedback.errorMessage).toBe('Current error.');
    expect(store.busy).toBe(transition === 'switch');
    currentLoad.resolve(ok([]));
    await loading;
  });

  it('applies same-board loads in response arrival order', async () => {
    const store = useBoardMembersStore();
    const first = createDeferred<Result<BoardMember[], AppError>>();
    api.getBoardMembers.mockReturnValueOnce(first.promise);
    const loading = store.loadMembers(1);
    await store.loadMembers(1);
    const member = makeMember(7, 'A User', 'a.user', 'Contributor');
    first.resolve(ok([member]));

    expect(await loading).toBe(true);
    expect(store.members).toEqual([member]);
  });

  it.each([
    ['add', 'switch'], ['update', 'switch'], ['delete', 'switch'],
    ['add', 'dispose'], ['update', 'dispose'], ['delete', 'dispose']
  ])('ignores late %s feedback and busy changes after %s', async (operation, transition) => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    await store.loadMembers(1);
    const success = createDeferred<Result<BoardMember | undefined, AppError>>();
    const failure = createDeferred<Result<never, AppError>>();
    let requests: Promise<unknown>[];
    if (operation === 'add') {
      api.addBoardMember.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
      requests = [store.addMember(1, { userId: 7, role: 'Owner' }), store.addMember(1, { userId: 8, role: 'Owner' })];
    } else if (operation === 'update') {
      api.updateBoardMemberRole.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
      requests = [store.updateMemberRole(1, { userId: 7, role: 'Owner' }), store.updateMemberRole(1, { userId: 8, role: 'Owner' })];
    } else {
      api.removeBoardMember.mockReturnValueOnce(success.promise).mockReturnValueOnce(failure.promise);
      requests = [store.deleteMember(1, 7), store.deleteMember(1, 8)];
    }
    const currentLoad = createDeferred<Result<BoardMember[], AppError>>();
    let loading: Promise<boolean> | undefined;
    if (transition === 'switch') {
      api.getBoardMembers.mockReturnValueOnce(currentLoad.promise);
      loading = store.loadMembers(2);
    } else {
      store.dispose();
    }
    feedback.setError('Current error.');
    if (operation === 'delete') {
      success.resolve(ok(undefined));
    } else {
      success.resolve(ok(makeMember(7, 'Old User', 'old.user', 'Owner')));
    }
    await requests[0];
    expect(feedback.errorMessage).toBe('Current error.');
    expect(store.busy).toBe(transition === 'switch');
    failure.resolve(err({ kind: 'api', message: 'Old error.' }));
    await requests[1];
    expect(feedback.errorMessage).toBe('Current error.');
    expect(store.busy).toBe(transition === 'switch');
    expect(store.members).toEqual([]);
    currentLoad.resolve(ok([]));
    await loading;
  });
});

function makeMember(
  userId: number,
  displayName: string,
  userName: string,
  role: 'Owner' | 'Contributor'
): BoardMember {
  return {
    userId,
    displayName,
    userName,
    role,
    profileImageRelativePath: null,
    createdAtUtc: '2026-10-01T00:00:00Z',
    updatedAtUtc: '2026-10-01T00:00:00Z'
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}
