import { onBeforeUnmount, onMounted, toValue, type MaybeRefOrGetter } from 'vue';

type ClickOutsideEvent = Pick<PointerEvent, 'target'> & {
  composedPath?: () => EventTarget[];
};

export function isClickOutsideElement(event: ClickOutsideEvent, element: HTMLElement | null): boolean {
  if (!element) {
    return false;
  }

  const composedPath = typeof event.composedPath === 'function' ? event.composedPath() : null;
  if (composedPath) {
    return !composedPath.includes(element);
  }

  const target = event.target;
  if (target === null || target === undefined) {
    return true;
  }

  return !element.contains(target as Node);
}

export function useClickOutside(
  elementRef: MaybeRefOrGetter<HTMLElement | null>,
  onOutsideClick: (event: PointerEvent) => void,
  enabled: MaybeRefOrGetter<boolean> = true
) {
  function handlePointerDown(event: PointerEvent) {
    if (!toValue(enabled)) {
      return;
    }

    const element = toValue(elementRef);
    if (!isClickOutsideElement(event, element)) {
      return;
    }

    onOutsideClick(event);
  }

  onMounted(() => {
    document.addEventListener('pointerdown', handlePointerDown, true);
  });

  onBeforeUnmount(() => {
    document.removeEventListener('pointerdown', handlePointerDown, true);
  });
}
