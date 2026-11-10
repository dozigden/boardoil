import { Trash2 } from '@lucide/vue';
import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { Column, ColumnEditModel } from '../../shared/types/boardTypes';

type ColumnOperations = {
  save: FormDefinition<ColumnEditModel, Column>['submit'];
  delete?: FormDefinition<ColumnEditModel, void>['submit'];
  isCurrent: () => boolean;
  canEdit: () => boolean;
};

export function createColumnForm(column: Column | null, operations: ColumnOperations) {
  return defineForm<ColumnEditModel, Column>({
    title: column ? 'Edit Column' : 'Add Column',
    submitLabel: column ? 'Save column' : 'Save new column',
    submitText: 'Save',
    cancelLabel: column ? 'Cancel editing' : 'Cancel new column',
    initialValues: () => ({ title: column?.title ?? '' }),
    fields: [{
      name: 'title', label: column ? 'Title' : 'Column title', type: 'text',
      autocomplete: 'off', placeholder: column ? undefined : 'New column title',
      disabled: () => !operations.canEdit()
    }],
    isCurrent: operations.isCurrent,
    canSubmit: operations.canEdit,
    submit: operations.save,
    actions: operations.delete ? [{ name: 'delete', label: 'Delete column', icon: Trash2, danger: true, run: operations.delete }] : []
  });
}
