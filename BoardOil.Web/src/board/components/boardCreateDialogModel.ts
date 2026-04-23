export type BoardCreateMode = 'blank' | 'tasksmd' | 'package';

export type BoardCreateDialogSubmitPayload =
  | { mode: 'blank'; name: string; description: string }
  | { mode: 'tasksmd'; url: string }
  | { mode: 'package'; file: File; name?: string };

export type BoardCreateDraft = {
  mode: BoardCreateMode;
  boardName: string;
  boardDescription: string;
  tasksMdUrl: string;
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

  if (draft.mode === 'tasksmd') {
    return draft.tasksMdUrl.trim().length > 0;
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

  if (draft.mode === 'tasksmd') {
    return { mode: 'tasksmd', url: draft.tasksMdUrl.trim() };
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
