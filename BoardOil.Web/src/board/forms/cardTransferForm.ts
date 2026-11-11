import { defineForm, type FormError, type FormFieldDefinition } from '../../shared/forms/formDefinition';
import type { BoardSummary, CardTransferPolicy, CardTransferResult, Column } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import { canCopyMissingDefinitions, defaultCardTransferPolicy } from '../components/cardTransferSelection';

export type CardTransferDraft = {
  destinationBoardId: number | null;
  destinationColumnId: number | null;
  transferPolicy: CardTransferPolicy;
};

type TransferOperations = {
  boards: () => readonly BoardSummary[];
  loadBoards: () => Promise<boolean>;
  loadColumns: (boardId: number) => Promise<Result<Column[], FormError>>;
  transfer: (boardId: number, columnId: number, policy: CardTransferPolicy) => Promise<Result<CardTransferResult, FormError>>;
  isCurrent: () => boolean;
};

export function createCardTransferForm(operations: TransferOperations) {
  let boardsLoaded = false;
  const canCopyMissing = (values: CardTransferDraft) => canCopyMissingDefinitions(
    operations.boards().find(board => board.id === values.destinationBoardId) ?? null
  );

  function fields(columns: readonly Column[]): readonly FormFieldDefinition<CardTransferDraft>[] {
    return [
      {
        name: 'destinationBoardId',
        label: 'Destination board',
        type: 'select',
        reassessOnChange: true,
        clearFields: ['destinationColumnId', 'transferPolicy'],
        options: () => {
          const boards = boardsLoaded ? operations.boards() : [];
          const placeholder = boards.length ? 'Select board' : 'No destination boards available';
          return [{ value: null, label: placeholder }, ...boards.map(board => ({ value: board.id, label: board.name }))];
        }
      },
      {
        name: 'destinationColumnId',
        label: 'Destination column',
        type: 'select',
        disabled: () => columns.length === 0,
        options: values => {
          if (!columns.length) return [{ value: null, label: values.destinationBoardId === null ? 'Select a board first' : 'No columns available' }];
          return columns.map(column => ({ value: column.id, label: column.title }));
        }
      },
      {
        name: 'transferPolicy',
        label: 'Board-specific content',
        type: 'radio',
        options: values => [
          {
            value: 'destinationDefaults',
            label: 'Use destination defaults',
            description: 'Use the default card type and remove tags and slick.'
          },
          {
            value: 'keepMatching',
            label: 'Keep matching only',
            description: "Use the destination card type, tags, and slick where they match, otherwise they're cleared."
          },
          {
            value: 'copyMissing',
            label: 'Copy missing',
            description: 'Any card type, tags, or slick that are missing will be created on the destination board.',
            disabled: !canCopyMissing(values)
          }
        ]
      }
    ];
  }

  return defineForm<CardTransferDraft, CardTransferResult>({
    title: 'Move card to another board',
    submitLabel: 'Move card',
    closeLabel: 'Cancel moving card',
    initialValues: () => ({
      destinationBoardId: null,
      destinationColumnId: null,
      transferPolicy: defaultCardTransferPolicy
    }),
    fields: fields([]),
    reassess: async values => {
      if (!boardsLoaded) {
        boardsLoaded = await operations.loadBoards();
        if (!boardsLoaded) return {
          values,
          fields: fields([]),
          error: 'Could not load destination boards.'
        };
      }
      values.destinationColumnId = null;
      if (values.transferPolicy === 'copyMissing' && !canCopyMissing(values)) values.transferPolicy = defaultCardTransferPolicy;
      if (values.destinationBoardId === null) return { values, fields: fields([]) };

      const result = await operations.loadColumns(values.destinationBoardId);
      if (!result.ok) return {
        values,
        fields: fields([]),
        error: result.error.message
      };
      values.destinationColumnId = result.data[0]?.id ?? null;
      return { values, fields: fields(result.data) };
    },
    canSubmit: values => values.destinationBoardId !== null && values.destinationColumnId !== null
      && (values.transferPolicy !== 'copyMissing' || canCopyMissing(values)),
    isCurrent: operations.isCurrent,
    submit: values => operations.transfer(values.destinationBoardId!, values.destinationColumnId!, values.transferPolicy)
  });
}
