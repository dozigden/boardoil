import { onMounted, onUnmounted, ref } from 'vue';

export type ViewMode = 'board' | 'columns';

export function useViewHash() {
  const currentView = ref<ViewMode>(parseViewFromHash(window.location.hash));

  function syncViewFromHash() {
    currentView.value = parseViewFromHash(window.location.hash);
  }

  function goToView(view: ViewMode) {
    const hash = view === 'columns' ? '#columns' : '#board';
    if (window.location.hash !== hash) {
      window.location.hash = hash;
    } else {
      currentView.value = view;
    }
  }

  onMounted(() => {
    window.addEventListener('hashchange', syncViewFromHash);
  });

  onUnmounted(() => {
    window.removeEventListener('hashchange', syncViewFromHash);
  });

  return {
    currentView,
    goToView
  };
}

function parseViewFromHash(hash: string): ViewMode {
  return hash === '#columns' ? 'columns' : 'board';
}
