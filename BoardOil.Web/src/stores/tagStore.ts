import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../api/boardApi';
import { useUiFeedbackStore } from './uiFeedbackStore';
import type { Tag, TagStyleName } from '../types/boardTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

export const useTagStore = defineStore('tag', () => {
  const tags = ref<Tag[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function initialize() {
  }

  function dispose() {
    activeBoardId.value = null;
    tags.value = [];
    busy.value = false;
  }

  async function loadTags(boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      tags.value = [];
      return false;
    }

    activeBoardId.value = resolvedBoardId;
    const result = await api.getTags(resolvedBoardId);
    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    tags.value = [...result.data].sort((a, b) => a.name.localeCompare(b.name));
    feedback.clearError();
    return true;
  }

  async function createTag(tagName: string, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.createTag(resolvedBoardId, tagName));
    if (!result.ok) {
      return null;
    }

    upsertTag(result.data);
    return result.data;
  }

  async function ensureTagsExist(tagNames: string[], boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return [];
    }

    const resolvedTagNames: string[] = [];
    for (const rawTagName of tagNames) {
      const trimmedTagName = rawTagName.trim();
      if (!trimmedTagName) {
        continue;
      }

      const existing = getTagByName(trimmedTagName);
      if (existing) {
        resolvedTagNames.push(existing.name);
        continue;
      }

      const created = await createTag(trimmedTagName, resolvedBoardId);
      if (created) {
        resolvedTagNames.push(created.name);
      }
    }

    return dedupeTagNames(resolvedTagNames);
  }

  async function updateTagStyle(
    tagId: number,
    styleName: TagStyleName,
    stylePropertiesJson: string,
    boardId: number | null = activeBoardId.value
  ) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return null;
    }

    const result = await runBusy(() => api.updateTagStyle(resolvedBoardId, tagId, styleName, stylePropertiesJson));
    if (!result.ok) {
      return null;
    }

    upsertTag(result.data);
    return result.data;
  }

  async function deleteTag(tagId: number, boardId: number | null = activeBoardId.value) {
    const resolvedBoardId = resolveBoardId(boardId);
    if (resolvedBoardId === null) {
      return false;
    }

    const result = await runBusy(() => api.deleteTag(resolvedBoardId, tagId));
    if (!result.ok) {
      return false;
    }

    removeTag(tagId);
    return true;
  }

  function getTagById(tagId: number | null) {
    if (tagId === null) {
      return null;
    }

    return tags.value.find(x => x.id === tagId) ?? null;
  }

  function getTagByName(tagName: string | null) {
    if (tagName === null) {
      return null;
    }

    return tags.value.find(x => x.name === tagName)
      ?? tags.value.find(x => x.name.toLowerCase() === tagName.toLowerCase())
      ?? null;
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function upsertTag(tag: Tag) {
    const existingIndex = tags.value.findIndex(x => x.id === tag.id || x.name === tag.name);
    if (existingIndex < 0) {
      tags.value = [...tags.value, tag].sort((a, b) => a.name.localeCompare(b.name));
      return;
    }

    const next = [...tags.value];
    next[existingIndex] = tag;
    tags.value = next.sort((a, b) => a.name.localeCompare(b.name));
  }

  function removeTag(tagId: number) {
    tags.value = tags.value.filter(tag => tag.id !== tagId);
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  function resolveBoardId(boardId: number | null) {
    const resolved = boardId ?? activeBoardId.value;
    if (resolved === null) {
      feedback.setError('No board selected.');
      return null;
    }

    return resolved;
  }

  return {
    tags,
    busy,
    activeBoardId,
    initialize,
    dispose,
    loadTags,
    createTag,
    ensureTagsExist,
    updateTagStyle,
    deleteTag,
    getTagById,
    getTagByName
  };
});

function dedupeTagNames(tagNames: string[]) {
  const deduped: string[] = [];
  const seen = new Set<string>();
  for (const tagName of tagNames) {
    const key = tagName.trim().toLowerCase();
    if (!key || seen.has(key)) {
      continue;
    }

    seen.add(key);
    deduped.push(tagName.trim());
  }

  return deduped;
}
