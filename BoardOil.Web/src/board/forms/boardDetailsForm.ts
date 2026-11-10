import { defineForm, type FormDefinition, type FormFieldDefinition } from '../../shared/forms/formDefinition';
import type { BoardEditModel, BoardSummary } from '../../shared/types/boardTypes';

type BoardDetailsOperations = {
  save: FormDefinition<BoardEditModel, BoardSummary>['submit'];
  canEdit: () => boolean;
  isCurrent: () => boolean;
  supportsAttachments: boolean;
};

function boardValues(board: BoardSummary): BoardEditModel {
  return {
    name: board.name, description: board.description,
    slickCohesionModeEnabled: board.slickCohesionModeEnabled,
    cardAttachmentThumbnailsEnabled: board.cardAttachmentThumbnailsEnabled
  };
}

export function createBoardDetailsForm(board: BoardSummary, operations: BoardDetailsOperations) {
  const disabled = () => !operations.canEdit();
  const fields: FormFieldDefinition<BoardEditModel>[] = [
    { name: 'name', label: 'Board name', type: 'text', disabled },
    { name: 'description', label: 'Description (optional)', type: 'textarea', rows: 8, disabled },
    { name: 'slickCohesionModeEnabled', label: 'Enable slick cohesion mode', type: 'checkbox', disabled }
  ];
  if (operations.supportsAttachments) {
    fields.push({ name: 'cardAttachmentThumbnailsEnabled', label: 'Show attachment thumbnails on cards', type: 'checkbox', disabled });
  }
  return defineForm<BoardEditModel, BoardSummary>({
    title: 'Details', submitLabel: 'Save details',
    initialValues: () => boardValues(board),
    fields,
    savedValues: boardValues,
    isCurrent: operations.isCurrent,
    canSubmit: operations.canEdit,
    submit: operations.save
  });
}
