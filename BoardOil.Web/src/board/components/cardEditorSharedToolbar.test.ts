import { describe, expect, it } from 'vitest';
import { createDisabledToolbarState, resolveActiveIsPlainTextMode, resolveActiveToolbarState } from './cardEditorSharedToolbar';
import type { MdEditorToolbarActionId, MdEditorToolbarActionState } from '../../shared/components/mdEditorToolbarActions';

describe('cardEditorSharedToolbar', () => {
  it('creates disabled default state for each toolbar action', () => {
    const actionIds: MdEditorToolbarActionId[] = ['bold', 'heading', 'link'];
    const state = createDisabledToolbarState(actionIds);

    expect(state.bold).toEqual({ disabled: true, isActive: false });
    expect(state.heading).toEqual({ disabled: true, isActive: false });
    expect(state.link).toEqual({ disabled: true, isActive: false });
  });

  it('routes active toolbar state to description by default', () => {
    const descriptionState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {
      bold: { disabled: false, isActive: true }
    };
    const commentState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {
      italic: { disabled: false, isActive: true }
    };
    const fallback = createDisabledToolbarState(['bold', 'italic']);

    expect(resolveActiveToolbarState('description', descriptionState, commentState, fallback)).toBe(descriptionState);
  });

  it('routes active toolbar state to comment when comment editor is active', () => {
    const descriptionState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {
      bold: { disabled: false, isActive: true }
    };
    const commentState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {
      italic: { disabled: false, isActive: true }
    };
    const fallback = createDisabledToolbarState(['bold', 'italic']);

    expect(resolveActiveToolbarState('comment', descriptionState, commentState, fallback)).toBe(commentState);
  });

  it('falls back to disabled state when active editor state is empty', () => {
    const descriptionState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {};
    const commentState: Partial<Record<MdEditorToolbarActionId, MdEditorToolbarActionState>> = {};
    const fallback = createDisabledToolbarState(['bold']);

    expect(resolveActiveToolbarState('description', descriptionState, commentState, fallback)).toBe(fallback);
    expect(resolveActiveToolbarState('comment', descriptionState, commentState, fallback)).toBe(fallback);
  });

  it('routes plain text mode based on active editor', () => {
    expect(resolveActiveIsPlainTextMode('description', true, false)).toBe(true);
    expect(resolveActiveIsPlainTextMode('description', false, true)).toBe(false);
    expect(resolveActiveIsPlainTextMode('comment', true, false)).toBe(false);
    expect(resolveActiveIsPlainTextMode('comment', false, true)).toBe(true);
  });
});
