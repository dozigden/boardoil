<template>
  <section
    class="user-authentication-view"
    :class="{ 'user-authentication-view--mcp-help': isMcpHelp }"
  >
    <nav class="user-authentication-tabs" role="tablist" aria-label="Authentication and MCP help">
      <RouterLink
        :to="{ name: 'user-admin-oauth-connections' }"
        class="user-authentication-tab"
        :class="{ 'is-active': route.name === 'user-admin-oauth-connections' }"
        role="tab"
        :aria-selected="route.name === 'user-admin-oauth-connections'"
      >
        OAuth
      </RouterLink>
      <RouterLink
        :to="{ name: 'user-admin-access-tokens' }"
        class="user-authentication-tab"
        :class="{ 'is-active': route.name === 'user-admin-access-tokens' }"
        role="tab"
        :aria-selected="route.name === 'user-admin-access-tokens'"
      >
        Access Tokens
      </RouterLink>
      <RouterLink
        :to="{ name: 'user-admin-mcp-help' }"
        class="user-authentication-tab"
        :class="{ 'is-active': route.name === 'user-admin-mcp-help' }"
        role="tab"
        :aria-selected="route.name === 'user-admin-mcp-help'"
      >
        MCP Help
      </RouterLink>
    </nav>

    <RouterView />
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';

const route = useRoute();
const isMcpHelp = computed(() => route.name === 'user-admin-mcp-help');
</script>

<style scoped>
.user-authentication-view {
  display: grid;
  gap: 1rem;
}

.user-authentication-view--mcp-help {
  grid-template-rows: auto minmax(0, 1fr);
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.user-authentication-tabs {
  display: flex;
  align-items: flex-end;
  gap: 0.2rem;
  overflow-x: auto;
  border-bottom: 1px solid var(--bo-border-default);
}

.user-authentication-tab {
  flex: 0 0 auto;
  margin-bottom: -1px;
  padding: 0.65rem 0.7rem 0.55rem;
  border-bottom: 3px solid transparent;
  color: var(--bo-ink-muted);
  font-weight: 650;
  text-decoration: none;
}

.user-authentication-tab:hover,
.user-authentication-tab:focus-visible {
  color: var(--bo-link);
}

.user-authentication-tab:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: -2px;
}

.user-authentication-tab.is-active {
  border-bottom-color: var(--bo-colour-energy);
  color: var(--bo-ink-strong);
}
</style>
