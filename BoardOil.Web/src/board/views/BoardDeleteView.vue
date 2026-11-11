<template>
  <section class="board-delete-page">
    <h2>Delete Board</h2>
    <p class="board-delete-warning">
      This will permanently delete the board, including all columns and cards.
    </p>

    <form class="board-delete-panel" @submit.prevent="submit">
      <p class="board-delete-confirm-copy">
        Type <strong>{{ boardName }}</strong> to confirm deletion.
      </p>

      <FormFeedback ref="feedback" general-errors="toast">
        <FormFields :fields="fields" :values="draft" :busy="locked" @change="setValue" />
      </FormFeedback>

      <p v-if="!isOwner" class="board-delete-owner-note">Owner permission required to delete this board.</p>

      <div class="board-delete-actions">
        <button
          type="submit"
          class="btn btn--danger"
          :disabled="!canSubmit"
        >
          Delete board
        </button>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue';
import { useRouter } from 'vue-router';
import FormFeedback from '../../shared/components/FormFeedback.vue';
import FormFields from '../../shared/forms/FormFields.vue';
import { useForm } from '../../shared/forms/useForm';
import { useValidationStore } from '../../shared/stores/validationStore';
import { createBoardDeleteForm } from '../forms/boardDeleteForm';
import { useBoardStore } from '../stores/boardStore';

const router = useRouter();
const boardStore = useBoardStore();
const boardId = boardStore.currentBoardId!;
const boardName = computed(() => boardStore.board?.name ?? '');
const isOwner = computed(() => boardStore.isCurrentUserOwner);
const feedback = ref<InstanceType<typeof FormFeedback> | null>(null);
const validation = useValidationStore();
const {
  draft,
  fields,
  locked,
  canSubmit,
  setValue,
  submit: submitForm
} = useForm(createBoardDeleteForm({
  boardName: () => boardName.value,
  canDelete: () => isOwner.value,
  isCurrent: () => boardStore.currentBoardId === boardId,
  delete: async () => {
    const result = await boardStore.deleteBoard(boardId);
    // Deletion disposes this board's view before the form receives its result.
    if (result.ok && boardStore.currentBoardId === null) await router.push({ name: 'boards' });
    return result;
  }
}));
validation.clearValidation();
onBeforeUnmount(() => validation.clearValidation());

async function submit() {
  const result = await submitForm();
  if (result && !result.ok) await feedback.value?.showFeedback();
}
</script>

<style scoped>
.board-delete-page {
  display: grid;
  gap: 0.8rem;
  max-width: 36rem;
}

.board-delete-page h2 {
  margin: 0;
}

.board-delete-warning {
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-weight: 600;
}

.board-delete-panel {
  display: grid;
  gap: 0.75rem;
  padding: 0.9rem;
  border: 1px solid color-mix(in oklab, var(--bo-colour-danger) 55%, var(--bo-border-soft));
  border-radius: 12px;
  background: var(--bo-surface-panel);
}

.board-delete-confirm-copy,
.board-delete-owner-note {
  margin: 0;
}

.board-delete-owner-note {
  color: var(--bo-ink-muted);
}

.board-delete-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>
