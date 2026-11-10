<template>
  <FormDialog :definition="definition" @close="emit('close')" @saved="emit('close')" />
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import { createBoardMemberForm } from '../../shared/forms/boardMemberForm';
import type { UserDirectoryEntry } from '../../shared/types/authTypes';
import type { BoardMember, BoardMemberEditModel } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

const props = defineProps<{
  boardId: number;
  currentBoardId: number | null;
  canManage: boolean;
  users: UserDirectoryEntry[];
  addMember: (boardId: number, model: BoardMemberEditModel) => Promise<Result<BoardMember, AppError>>;
}>();
const emit = defineEmits<{ close: [] }>();
const boardId = props.boardId;
const definition = createBoardMemberForm({
  users: () => props.users,
  canManage: () => props.canManage,
  isCurrent: () => props.currentBoardId === boardId,
  add: model => props.addMember(boardId, model)
});
</script>
