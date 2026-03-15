<template>
  <section class="toolbar">
    <form class="create-column" @submit.prevent="emit('create-column')">
      <input
        :value="newColumnTitle"
        type="text"
        maxlength="200"
        placeholder="New column title"
        @input="emit('update-new-column-title', ($event.target as HTMLInputElement).value)"
      />
      <button type="submit" :disabled="busy">Add Column</button>
    </form>
  </section>

  <section class="column-manager">
    <article v-for="column in columns" :key="column.id" class="column-manager-item">
      <label>
        Column title
        <input
          class="column-title"
          :value="columnTitleDrafts[column.id] ?? column.title"
          maxlength="200"
          @input="emit('update-column-draft', column.id, ($event.target as HTMLInputElement).value)"
        />
      </label>
      <div class="column-actions">
        <button @click="emit('save-column', column.id)">Save</button>
        <button class="danger" @click="emit('delete-column', column.id)">Delete</button>
      </div>
    </article>
  </section>
</template>

<script setup lang="ts">
import type { BoardColumn } from '../types/boardTypes';

defineProps<{
  columns: BoardColumn[];
  busy: boolean;
  newColumnTitle: string;
  columnTitleDrafts: Record<number, string>;
}>();

const emit = defineEmits<{
  'create-column': [];
  'update-new-column-title': [value: string];
  'update-column-draft': [columnId: number, value: string];
  'save-column': [columnId: number];
  'delete-column': [columnId: number];
}>();
</script>
