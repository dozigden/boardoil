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
    tasksMdUrl: '',
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

  it('allows tasksmd mode submit only when url is non-empty', () => {
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'tasksmd', tasksMdUrl: '' }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'tasksmd', tasksMdUrl: 'https://tasks.example.net/' }), false)).toBe(true);
  });

  it('allows package mode submit only when file is selected', () => {
    const file = new File(['zip'], 'board.boardoil.zip', { type: 'application/zip' });

    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'package', packageFile: null }), false)).toBe(false);
    expect(canSubmitBoardCreateDraft(makeDraft({ mode: 'package', packageFile: file }), false)).toBe(true);
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
