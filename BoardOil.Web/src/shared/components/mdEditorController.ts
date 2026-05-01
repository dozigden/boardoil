import type { Editor as TiptapEditor } from '@tiptap/core';
import { mdEditorToolbarActions, type MdEditorToolbarActionEvent } from './mdEditorToolbarActions';

export function normaliseToolbarActionEvent(actionEvent: MdEditorToolbarActionEvent): MdEditorToolbarActionEvent {
  if (actionEvent.id !== 'heading') {
    return { id: actionEvent.id };
  }

  return {
    id: actionEvent.id,
    headingLevel: actionEvent.headingLevel ?? 1
  };
}

export function runMdEditorToolbarAction(
  actionEvent: MdEditorToolbarActionEvent,
  editor: TiptapEditor | null,
  isPlainTextMode: boolean,
  openLinkDialog: (editor: TiptapEditor) => void
) {
  if (isPlainTextMode || !editor) {
    return false;
  }

  const nextActionEvent = normaliseToolbarActionEvent(actionEvent);
  const action = mdEditorToolbarActions.find(x => x.id === nextActionEvent.id);
  if (!action || !action.canRun(editor, nextActionEvent)) {
    return false;
  }

  action.run(editor, { openLinkDialog }, nextActionEvent);
  return true;
}
