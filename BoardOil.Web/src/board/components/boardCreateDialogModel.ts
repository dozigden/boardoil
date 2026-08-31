export type BoardCreateMode = 'blank' | 'clone' | 'package';

export type BoardCreateDialogSubmitPayload =
  | { mode: 'blank'; name: string; description: string }
  | { mode: 'clone'; sourceBoardId: number; name: string }
  | { mode: 'package'; file: File; name?: string };

export type BoardCreateDraft = {
  mode: BoardCreateMode;
  boardName: string;
  boardDescription: string;
  cloneSourceBoardId: number | null;
  packageFile: File | null;
  packageBoardNameOverride: string;
};

export function canSubmitBoardCreateDraft(draft: BoardCreateDraft, busy: boolean) {
  if (busy) {
    return false;
  }

  if (draft.mode === 'blank') {
    return draft.boardName.trim().length > 0;
  }

  if (draft.mode === 'clone') {
    return draft.cloneSourceBoardId !== null
      && draft.cloneSourceBoardId > 0
      && draft.boardName.trim().length > 0;
  }

  return draft.packageFile !== null;
}

export function buildBoardCreateSubmitPayload(draft: BoardCreateDraft): BoardCreateDialogSubmitPayload | null {
  if (draft.mode === 'blank') {
    return {
      mode: 'blank',
      name: draft.boardName.trim(),
      description: draft.boardDescription.trim()
    };
  }

  if (draft.mode === 'clone') {
    if (draft.cloneSourceBoardId === null || draft.cloneSourceBoardId <= 0) {
      return null;
    }

    return {
      mode: 'clone',
      sourceBoardId: draft.cloneSourceBoardId,
      name: draft.boardName.trim()
    };
  }

  if (!draft.packageFile) {
    return null;
  }

  const overrideName = draft.packageBoardNameOverride.trim();
  return {
    mode: 'package',
    file: draft.packageFile,
    name: overrideName.length > 0 ? overrideName : undefined
  };
}
