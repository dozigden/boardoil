import type { MdEditorToolbarActionId, MdEditorToolbarActionState } from '../../shared/components/mdEditorToolbarActions';

export type CardEditorActiveEditor = 'description' | 'comment';

export function createDisabledToolbarState(actionIds: MdEditorToolbarActionId[]) {
  const state: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {};
  for (const actionId of actionIds) {
    state[actionId] = {
      disabled: true,
      isActive: false
    };
  }

  return state;
}

export function resolveActiveToolbarState(
  activeEditor: CardEditorActiveEditor,
  descriptionToolbarState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>,
  commentToolbarState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>,
  disabledToolbarState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>>
) {
  if (activeEditor === 'comment') {
    return Object.keys(commentToolbarState).length > 0 ? commentToolbarState : disabledToolbarState;
  }

  return Object.keys(descriptionToolbarState).length > 0 ? descriptionToolbarState : disabledToolbarState;
}

export function resolveActiveIsPlainTextMode(
  activeEditor: CardEditorActiveEditor,
  descriptionIsPlainTextMode: boolean,
  commentIsPlainTextMode: boolean
) {
  return activeEditor === 'comment' ? commentIsPlainTextMode : descriptionIsPlainTextMode;
}
