import { defineForm, type FormError, type FormFieldDefinition } from '../../shared/forms/formDefinition';
import type { BulkEditCardsOptions, BulkEditSlickOperation } from '../../shared/types/bulkEditTypes';
import type { Card } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import BulkTagFormControl from '../components/BulkTagFormControl.vue';

export type BulkEditDraft = {
  targetColumnId: number | null;
  slickOperation: BulkEditSlickOperation;
  targetSlickName: string | null;
  tagStates: TagFilterStateMap;
};

type BulkEditOperations = {
  columns: () => readonly { id: number; title: string }[];
  slicks: () => readonly { name: string }[];
  tagNames: () => readonly string[];
  selectedCount: () => number;
  save: (options: BulkEditCardsOptions) => Promise<Result<Card[], FormError>>;
  isCurrent: () => boolean;
};

export function createBulkEditForm(operations: BulkEditOperations) {
  function fields(values: BulkEditDraft): FormFieldDefinition<BulkEditDraft>[] {
    const result: FormFieldDefinition<BulkEditDraft>[] = [
      {
        name: 'targetColumnId', label: 'Column', type: 'select', errorFields: ['move.targetColumnId'],
        options: () => [{ value: null, label: 'No change' }, ...operations.columns().map(column => ({ value: column.id, label: column.title }))]
      },
      {
        name: 'slickOperation', label: 'Slick', type: 'select', reassessOnChange: true, clearFields: ['targetSlickName'],
        options: [{ value: 'none', label: 'No change' }, { value: 'clear', label: 'No slick' }, { value: 'set', label: 'Set slick' }]
      }
    ];
    if (values.slickOperation === 'set') result.push({
      name: 'targetSlickName', label: 'Slick to set', type: 'select', errorFields: ['slick.name'],
      options: () => [{ value: null, label: 'Select slick' }, ...operations.slicks().map(slick => ({ value: slick.name, label: slick.name }))]
    });
    result.push({
      name: 'tagStates', label: 'Tags', type: 'custom', component: BulkTagFormControl,
      errorFields: ['addTagNames', 'removeTagNames']
    });
    return result;
  }

  const initialValues = (): BulkEditDraft => ({ targetColumnId: null, slickOperation: 'none', targetSlickName: null, tagStates: {} });

  return defineForm<BulkEditDraft, Card[]>({
    title: 'Bulk Edit Selected Cards', submitLabel: 'Apply edits', closeLabel: 'Close bulk edit',
    initialValues, fields: fields(initialValues()),
    reassess: values => {
      if (values.slickOperation !== 'set') values.targetSlickName = null;
      return { values, fields: fields(values) };
    },
    canSubmit: values => operations.selectedCount() > 0 && (values.targetColumnId !== null
      || values.slickOperation !== 'none' || Object.values(values.tagStates).some(state => state !== 'none')),
    validate: values => {
      if (values.slickOperation === 'set' && values.targetSlickName === null) {
        return { targetSlickName: ['Select a slick.'] };
      }
      return {};
    },
    isCurrent: operations.isCurrent,
    submit: values => {
      const addTagNames: string[] = [];
      const removeTagNames: string[] = [];
      for (const name of operations.tagNames()) {
        const state = values.tagStates[name.trim().toLocaleLowerCase()];
        if (state === 'include') addTagNames.push(name);
        if (state === 'exclude') removeTagNames.push(name);
      }
      const options: BulkEditCardsOptions = { moveTargetColumnId: values.targetColumnId, addTagNames, removeTagNames };
      if (values.slickOperation === 'clear') options.slickName = null;
      if (values.slickOperation === 'set') options.slickName = values.targetSlickName;
      return operations.save(options);
    }
  });
}
