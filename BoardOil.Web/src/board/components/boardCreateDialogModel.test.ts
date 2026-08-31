import { describe, expect, it } from 'vitest';
import {
  buildBoardCreateSubmitPayload,
  canSubmitBoardCreateDraft,
  type BoardCreateDraft
} from './boardCreateDialogModel';

function makeDraft(overrides: Partial<BoardCreateDraft> = {}): BoardCreateDraft {
  return {
    mode: 'blank',
    boardName: '',
    boardDescription: '',
    cloneSourceBoardId: null,
    packageFile: null,
    packageBoardNameOverride: '',
    ...overrides
  };
}

describe('boardCreateDialogModel', () => {
  it('allows blank mode submit only when board name is non-empty', () => {
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'blank', boardName: '' }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'blank', boardName: '  Roadmap  ' }), false)).toBe(true);
  });

  it('allows package mode submit only when file is selected', () => {
    const file = new File(['zip'], 'board.boardoil.zip', { type: 'application/zip' });

    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'package', packageFile: null }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'package', packageFile: file }), false)).toBe(true);
  });

  it('allows clone mode submit only with a source board and new name', () => {
    expect(canSubmitBoardCreateDraft(makeDraft({
      mode: 'clone',
      cloneSourceBoardId: null,
      boardName: 'Clone'
    }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({
      mode: 'clone',
      cloneSourceBoardId: 4,
      boardName: '   '
    }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({
      mode: 'clone',
      cloneSourceBoardId: 4,
      boardName: '  Clone  '
    }), false)).toBe(true);
  });

  it('builds clone submit payload with a trimmed name', () => {
    const payload = buildBoardCreateSubmitPayload(makeDraft({
      mode: 'clone',
      cloneSourceBoardId: 4,
      boardName: '  Cloned board  '
    }));

    expect(payload).toEqual({
      mode: 'clone',
      sourceBoardId: 4,
      name: 'Cloned board'
    });
  });

  it('builds package submit payload with optional trimmed name override', () => {
    const file = new File(['zip'], 'board.boardoil.zip', { type: 'application/zip' });

    const withOverride = buildBoardCreateSubmitPayload(
      makeDraft({
        mode: 'package',
        packageFile: file,
        packageBoardNameOverride: '  Renamed board  '
      })
    );
    expect(withOverride).toEqual({
      mode: 'package',
      file,
      name: 'Renamed board'
    });

    const withoutOverride = buildBoardCreateSubmitPayload(
      makeDraft({
        mode: 'package',
        packageFile: file,
        packageBoardNameOverride: '   '
      })
    );
    expect(withoutOverride).toEqual({
      mode: 'package',
      file,
      name: undefined
    });
  });

  it('builds blank submit payload with trimmed description', () => {
    const payload = buildBoardCreateSubmitPayload(
      makeDraft({
        mode: 'blank',
        boardName: '  Roadmap  ',
        boardDescription: '  Board guidance  '
      })
    );

    expect(payload).toEqual({
      mode: 'blank',
      name: 'Roadmap',
      description: 'Board guidance'
    });
  });
});
