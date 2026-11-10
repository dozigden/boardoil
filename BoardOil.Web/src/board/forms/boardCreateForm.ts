import { defineForm, type FormError, type FormFieldDefinition } from '../../shared/forms/formDefinition';
import type { BoardSummary } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import BoardCreateModeControl from '../components/BoardCreateModeControl.vue';

export type BoardCreateMode = 'blank' | 'clone' | 'package';
export type BoardCreateDraft = {
  mode: BoardCreateMode;
  name: string;
  description: string;
  sourceBoardId: number | null;
  file: File | null;
  nameOverride: string;
};

type Operations = {
  boards: () => readonly BoardSummary[];
  create: (name: string, description: string) => Promise<Result<BoardSummary, FormError>>;
  clone: (boardId: number, name: string) => Promise<Result<BoardSummary, FormError>>;
  import: (file: File | null, name?: string) => Promise<Result<BoardSummary, FormError>>;
};

export function createBoardForm(operations: Operations) {
  function fields(mode: BoardCreateMode): FormFieldDefinition<BoardCreateDraft>[] {
    const modeField: FormFieldDefinition<BoardCreateDraft> = {
      name: 'mode', label: 'Board create mode', type: 'custom', component: BoardCreateModeControl,
      reassessOnChange: true, clearFields: 'all'
    };
    if (mode === 'package') return [
      modeField,
      { name: 'file', label: 'Package ZIP file', type: 'file', accept: '.zip,application/zip' },
      { name: 'nameOverride', label: 'Board name override (optional)', type: 'text', autocomplete: 'off',
        placeholder: 'Leave empty to use package board name', errorFields: ['name'] }
    ];
    if (mode === 'clone') return [
      modeField,
      { name: 'sourceBoardId', label: 'Source board', type: 'select', options: () => [
        { value: null, label: 'Select a board' },
        ...operations.boards().map(board => ({ value: board.id, label: `#${board.id} ${board.name}` }))
      ] },
      { name: 'name', label: 'New board name', type: 'text', autocomplete: 'off',
        help: 'Copies the board configuration without cards, archives, or members.' }
    ];
    return [
      modeField,
      { name: 'name', label: 'Board name', type: 'text', autocomplete: 'off' },
      { name: 'description', label: 'Description (optional)', type: 'textarea', rows: 4 }
    ];
  }

  return defineForm<BoardCreateDraft, BoardSummary>({
    title: 'Create Board', submitLabel: values => values.mode === 'package' ? 'Import board' : 'Create board',
    cancelLabel: 'Cancel creation', closeLabel: 'Cancel creation',
    initialValues: () => ({ mode: 'blank', name: '', description: '', sourceBoardId: null, file: null, nameOverride: '' }),
    fields: fields('blank'),
    reassess: values => ({ values, fields: fields(values.mode) }),
    canSubmit: values => values.mode !== 'clone' || values.sourceBoardId !== null,
    submit: values => {
      switch (values.mode) {
        case 'blank': return operations.create(values.name, values.description);
        case 'clone': return operations.clone(values.sourceBoardId!, values.name);
        case 'package': return operations.import(values.file, values.nameOverride.trim() || undefined);
      }
    }
  });
}
