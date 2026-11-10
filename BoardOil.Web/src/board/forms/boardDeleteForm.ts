import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';

type BoardDeleteDraft = { confirmationName: string };

type BoardDeleteOperations = {
  boardName: () => string;
  canDelete: () => boolean;
  isCurrent: () => boolean;
  delete: FormDefinition<BoardDeleteDraft, void>['submit'];
};

export function createBoardDeleteForm(operations: BoardDeleteOperations) {
  return defineForm<BoardDeleteDraft, void>({
    title: 'Delete Board', submitLabel: 'Delete board',
    initialValues: () => ({ confirmationName: '' }),
    fields: [{ name: 'confirmationName', label: 'Confirm board name', type: 'text', autocomplete: 'off', disabled: () => !operations.canDelete() }],
    isCurrent: operations.isCurrent,
    canSubmit: values => operations.canDelete() && values.confirmationName.trim() === operations.boardName().trim(),
    submit: operations.delete
  });
}
