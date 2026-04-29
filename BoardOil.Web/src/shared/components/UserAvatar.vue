<template>
  <img
    v-if="resolvedImageUrl"
    :src="resolvedImageUrl"
    alt=""
    class="user-avatar"
    :style="avatarStyle"
    aria-hidden="true"
  />
  <span
    v-else
    class="user-avatar user-avatar--fallback"
    :style="avatarStyle"
    aria-hidden="true"
  >
    {{ initials }}
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { buildApiUrl } from '../api/config';
import { buildInitials } from '../utils/initials';

const props = withDefaults(defineProps<{
  imageUrl?: string | null;
  imageRelativePath?: string | null;
  displayName: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}>(), {
  imageUrl: null,
  imageRelativePath: null,
  size: 'md'
});

const resolvedImageUrl = computed(() => {
  if (props.imageUrl) {
    return props.imageUrl;
  }

  return props.imageRelativePath ? buildApiUrl(`/images/${props.imageRelativePath}`) : null;
});

const initials = computed(() => buildInitials(props.displayName));
const sizeMap: Record<NonNullable<typeof props.size>, { avatar: string; font: string }> = {
  sm: { avatar: '1.25rem', font: '0.6rem' },
  md: { avatar: '1.5rem', font: '0.65rem' },
  lg: { avatar: '2rem', font: '0.75rem' },
  xl: { avatar: '6.75rem', font: '1.65rem' }
};
const avatarStyle = computed(() => ({
  '--user-avatar-size': sizeMap[props.size].avatar,
  '--user-avatar-fallback-font-size': sizeMap[props.size].font
}));
</script>

<style scoped>
.user-avatar {
  width: var(--user-avatar-size);
  height: var(--user-avatar-size);
  border-radius: 999px;
  object-fit: cover;
  flex: 0 0 auto;
}

.user-avatar--fallback {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bo-surface-brand);
  color: var(--bo-link);
  font-size: var(--user-avatar-fallback-font-size);
  font-weight: 700;
}
</style>
