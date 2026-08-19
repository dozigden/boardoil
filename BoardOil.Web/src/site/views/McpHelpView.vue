<template>
  <section class="mcp-help-view">
    <MdViewer
      v-if="resolvedMcpHelpMarkdown"
      :active-heading-anchor="requestedSectionAnchor"
      :model-value="resolvedMcpHelpMarkdown"
      aria-label="MCP help"
      heading-anchors
      min-height="0"
    />
    <p v-else-if="errorMessage" class="error mcp-help-status" role="alert">
      {{ errorMessage }}
    </p>
    <p v-else class="mcp-help-status" aria-live="polite">Loading MCP help…</p>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import mcpHelpMarkdown from 'virtual:boardoil-mcp-help';
import { getMcpOAuthMetadata } from '../../shared/api/oauthMetadataApi';
import MdViewer from '../../shared/components/MdViewer.vue';
import { resolveMcpHelpContent } from '../utils/mcpHelpContent';

const resolvedMcpHelpMarkdown = ref('');
const errorMessage = ref<string | null>(null);
const route = useRoute();

const requestedSectionAnchor = computed(() => route.hash.replace(/^#/, ''));

onMounted(() => {
  void loadMcpHelp();
});

async function loadMcpHelp() {
  errorMessage.value = null;
  const result = await getMcpOAuthMetadata();
  if (!result.ok) {
    errorMessage.value = result.error.message;
    return;
  }

  try {
    resolvedMcpHelpMarkdown.value = resolveMcpHelpContent(
      mcpHelpMarkdown,
      result.data.resource
    );
  } catch {
    errorMessage.value = 'BoardOil returned an invalid MCP OAuth resource URL.';
  }
}
</script>

<style scoped>
.mcp-help-view {
  display: flex;
  height: 100%;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
}

.mcp-help-status {
  margin: 0;
}

.mcp-help-view :deep(.md-viewer-content .tiptap) {
  width: min(52rem, 100%);
  max-width: 52rem;
  margin: 0 auto;
  padding: 1.5rem;
  background: var(--bo-surface-panel);
  box-shadow: var(--bo-shadow-pop);
  font-size: 0.9rem;
  line-height: 1.6;
}

.mcp-help-view :deep(.md-viewer-content .tiptap h1),
.mcp-help-view :deep(.md-viewer-content .tiptap h2),
.mcp-help-view :deep(.md-viewer-content .tiptap h3) {
  font-family: "Montserrat", "Segoe UI", sans-serif;
  scroll-margin-top: 1rem;
}

.mcp-help-view :deep(.md-viewer-content .tiptap h1) {
  font-size: 1.6rem;
  line-height: 1.2;
}

.mcp-help-view :deep(.md-viewer-content .tiptap h2) {
  margin-top: 1.5rem;
  font-size: 1.25rem;
}

.mcp-help-view :deep(.md-viewer-content .tiptap h3) {
  margin-top: 1.25rem;
  font-size: 1.05rem;
}

.mcp-help-view :deep(.md-viewer-content .tiptap p),
.mcp-help-view :deep(.md-viewer-content .tiptap li) {
  line-height: 1.6;
}

.mcp-help-view :deep(.md-viewer-content .tiptap a) {
  color: var(--bo-link);
}

.mcp-help-view :deep(.md-viewer-content .tiptap pre) {
  overflow-x: auto;
  padding: 1rem;
  border-radius: 0.75rem;
  background: #182235;
  color: #f8fafc;
  white-space: pre;
}

.mcp-help-view :deep(.md-viewer-content .tiptap code) {
  font-family: ui-monospace, "Cascadia Code", "SFMono-Regular", Consolas, monospace;
}

.mcp-help-view :deep(.md-viewer-content .tiptap pre code) {
  background: transparent;
  color: inherit;
}
</style>
