import { describe, expect, it, vi } from 'vitest';
import { normaliseToolbarActionEvent, runMdEditorToolbarAction } from './mdEditorController';
import type { MdEditorToolbarActionEvent } from './mdEditorToolbarActions';

function createFakeEditor(canRun = true) {
  const toggleHeading = vi.fn(() => ({ run: () => canRun }));
  const toggleTaskList = vi.fn(() => ({ run: () => canRun }));
  const canChainRun = vi.fn(() => canRun);
  const actionChainRun = vi.fn(() => true);

  const canChain = () => ({
    focus: () => ({
      toggleBold: () => ({
        run: canChainRun
      }),
      toggleHeading,
      toggleTaskList
    })
  });

  const chain = () => ({
    focus: () => ({
      toggleBold: () => ({
        run: actionChainRun
      }),
      toggleHeading,
      toggleTaskList
    })
  });

  return {
    editor: {
      can: () => ({ chain: canChain }),
      chain
    } as any,
    spies: {
      toggleHeading,
      toggleTaskList,
      canChainRun,
      actionChainRun
    }
  };
}

describe('normaliseToolbarActionEvent', () => {
  it('normalises heading events with default level 1', () => {
    expect(normaliseToolbarActionEvent({ id: 'heading' })).toEqual({ id: 'heading', headingLevel: 1 });
  });

  it('preserves heading level when provided', () => {
    expect(normaliseToolbarActionEvent({ id: 'heading', headingLevel: 3 })).toEqual({ id: 'heading', headingLevel: 3 });
  });

  it('removes irrelevant properties from non-heading actions', () => {
    expect(normaliseToolbarActionEvent({ id: 'bold', headingLevel: 3 } as MdEditorToolbarActionEvent)).toEqual({ id: 'bold' });
  });
});

describe('runMdEditorToolbarAction', () => {
  it('does not run actions in plain text mode', () => {
    const { editor } = createFakeEditor(true);
    const openLinkDialog = vi.fn();

    const ran = runMdEditorToolbarAction({ id: 'bold' }, editor, true, openLinkDialog);

    expect(ran).toBe(false);
    expect(openLinkDialog).not.toHaveBeenCalled();
  });

  it('does not run actions when editor is missing', () => {
    const ran = runMdEditorToolbarAction({ id: 'bold' }, null, false, vi.fn());
    expect(ran).toBe(false);
  });

  it('runs heading action with default heading level when not provided', () => {
    const { editor, spies } = createFakeEditor(true);

    const ran = runMdEditorToolbarAction({ id: 'heading' }, editor, false, vi.fn());

    expect(ran).toBe(true);
    expect(spies.toggleHeading).toHaveBeenCalledWith({ level: 1 });
  });

  it('does not run when action cannot run', () => {
    const { editor } = createFakeEditor(false);
    const ran = runMdEditorToolbarAction({ id: 'bold' }, editor, false, vi.fn());
    expect(ran).toBe(false);
  });

  it('runs task-list action when available', () => {
    const { editor, spies } = createFakeEditor(true);
    const ran = runMdEditorToolbarAction({ id: 'task-list' }, editor, false, vi.fn());
    expect(ran).toBe(true);
    expect(spies.toggleTaskList).toHaveBeenCalled();
  });
});
