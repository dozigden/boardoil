import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useBoardMembersStore } from './boardMembersStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { err, ok } from '../../shared/types/result';

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
    await store.removeMember(3, 7);

    expect(added?.userId).toBe(7);
    expect(api.addBoardMember).toHaveBeenCalledWith(3, { userId: 7, role: 'Contributor' });
    expect(api.updateBoardMemberRole).toHaveBeenCalledWith(3, { userId: 7, role: 'Owner' });
    expect(api.removeBoardMember).toHaveBeenCalledWith(3, 7);
  });

  it('reports API errors during loads', async () => {
    const store = useBoardMembersStore();
    const feedback = useUiFeedbackStore();
    api.getBoardMembers.mockResolvedValueOnce(err({ kind: 'api', message: 'Could not load members.' }));

    const loaded = await store.loadMembers(3);

    expect(loaded).toBe(false);
    expect(feedback.errorMessage).toBe('Could not load members.');
  });
});

function makeMember(
  userId: number,
  displayName: string,
  userName: string,
  role: 'Owner' | 'Contributor'
) {
  return {
    userId,
    displayName,
    userName,
    role,
    profileImageRelativePath: null
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
