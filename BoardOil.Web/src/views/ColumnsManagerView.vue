<template>
  <template v-if="board">
    <section class="toolbar">
      <form class="create-column" @submit.prevent="createColumn">
        <input
          :value="newColumnTitle"
          type="text"
          maxlength="200"
          placeholder="New column title"
          @input="updateNewColumnTitle(($event.target as HTMLInputElement).value)"
        />
        <button type="submit" :disabled="busy">Add Column</button>
      </form>
    </section>

    <section class="column-manager">
      <article v-for="column in board.columns" :key="column.id" class="column-manager-item">
        <label>
          Column title
          <input
            class="column-title"
            :value="columnTitleDrafts[column.id] ?? column.title"
            maxlength="200"
            @input="updateColumnDraft(column.id, ($event.target as HTMLInputElement).value)"
          />
        </label>
        <div class="column-actions">
          <button @click="saveColumn(column.id)">Save</button>
          <button class="danger" @click="deleteColumn(column.id)">Delete</button>
        </div>
      </article>
    </section>
  </template>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { ref } from 'vue';
import { useBoardStore } from '../stores/boardStore';

const newColumnTitle = ref('');
const columnTitleDrafts = ref<Record<number, string>>({});

const boardStore = useBoardStore();
const { board, busy } = storeToRefs(boardStore);
const { createColumn: createColumnAction, saveColumn: saveColumnAction, deleteColumn } = boardStore;

function updateNewColumnTitle(value: string) {
  newColumnTitle.value = value;
}

async function createColumn() {
  await createColumnAction(newColumnTitle.value);
  newColumnTitle.value = '';
}

function updateColumnDraft(columnId: number, value: string) {
  columnTitleDrafts.value[columnId] = value;
}

async function saveColumn(columnId: number) {
  const title = columnTitleDrafts.value[columnId];
  if (title === undefined) {
    return;
  }

  await saveColumnAction(columnId, title);
}
</script>
