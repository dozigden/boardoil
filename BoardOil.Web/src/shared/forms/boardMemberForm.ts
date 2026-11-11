import { defineForm, type FormDefinition } from './formDefinition';
import type { UserDirectoryEntry } from '../types/authTypes';
import type { BoardMember, BoardMemberEditModel } from '../types/boardTypes';

type MemberOperations = {
  users: () => readonly UserDirectoryEntry[];
  canManage: () => boolean;
  isCurrent: () => boolean;
  add: FormDefinition<BoardMemberEditModel, BoardMember>['submit'];
};

export function createBoardMemberForm(operations: MemberOperations) {
  return defineForm<BoardMemberEditModel, BoardMember>({
    title: 'Add Member',
    submitLabel: 'Add member',
    cancelLabel: 'Cancel add member',
    initialValues: () => ({ userId: operations.users()[0]?.id ?? 0, role: 'Contributor' }),
    fields: [
      {
        name: 'userId',
        label: 'User',
        type: 'select',
        disabled: () => !operations.canManage(),
        options: () => [
          {
            value: 0,
            label: operations.users().length ? 'Select a user' : 'No users available',
            disabled: true
          },
          ...operations.users().map(user => ({
            value: user.id,
            label: `${user.displayName} (@${user.userName}, #${user.id})${user.isActive ? '' : ' - Inactive'}`
          }))
        ]
      },
      {
        name: 'role',
        label: 'Role',
        type: 'select',
        disabled: () => !operations.canManage(),
        options: [{ value: 'Contributor', label: 'Contributor' }, { value: 'Owner', label: 'Owner' }]
      }
    ],
    canSubmit: operations.canManage,
    isCurrent: operations.isCurrent,
    submit: operations.add
  });
}
