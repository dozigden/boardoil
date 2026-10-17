import { onBeforeUnmount, onMounted, shallowReactive } from 'vue';

type CardPosition = {
  rect: DOMRect;
  clip: DOMRect | null;
  columnId: number;
  floating: boolean;
  sortKey: string;
  foreground: boolean;
};
type MovementSnapshot = {
  cards: Map<number, CardPosition>;
  columns: Map<number, { element: HTMLElement; height: number }>;
};
type MovingCard = {
  animation: Animation;
  element: HTMLElement;
  floating: HTMLElement | null;
  visibility: string;
  columnId: number;
  foreground: boolean;
};

export function useCardMovementAnimation(refreshGoo: () => void) {
  const floatingCards = shallowReactive(new Map<number, HTMLElement>());
  const active = new Map<number, MovingCard>();
  const columnAnimations = new Map<number, Animation>();
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let board: HTMLElement | null = null;
  let layer: HTMLElement | null = null;
  let frame: number | null = null;

  function refreshFrame() {
    // Neighbours finish together. Coalesce their final goo updates rather
    // than repeatedly measuring and rebuilding it during the same frame.
    if (frame !== null) {
      return;
    }
    frame = requestAnimationFrame(() => {
      frame = null;
      refreshGoo();
      if (active.size > 0 || columnAnimations.size > 0) {
        refreshFrame();
      }
    });
  }

  function settle(cardId: number) {
    const moving = active.get(cardId);
    if (!moving) {
      return;
    }
    moving.animation.onfinish = null;
    moving.animation.cancel();
    moving.element.style.visibility = moving.visibility;
    moving.floating?.remove();
    active.delete(cardId);
    floatingCards.delete(cardId);
  }

  function finishAll() {
    if (active.size === 0 && columnAnimations.size === 0 && frame === null) {
      return;
    }
    for (const cardId of active.keys()) {
      settle(cardId);
    }
    for (const animation of columnAnimations.values()) {
      animation.onfinish = null;
      animation.cancel();
    }
    columnAnimations.clear();
    if (frame !== null) {
      cancelAnimationFrame(frame);
      frame = null;
    }
    refreshGoo();
  }

  function setBoardRef(element: unknown) {
    board?.removeEventListener('scroll', finishAll, true);
    board = element instanceof HTMLElement ? element : null;
    board?.addEventListener('scroll', finishAll, { passive: true, capture: true });
  }

  function setLayerRef(element: unknown) {
    layer = element instanceof HTMLElement ? element : null;
  }

  function capture(columnIds: Set<number>) {
    const snapshot: MovementSnapshot = { cards: new Map(), columns: new Map() };
    if (!board || reducedMotion.matches) {
      return snapshot;
    }
    for (const column of board.querySelectorAll<HTMLElement>('[data-column-id]')) {
      const columnId = Number(column.dataset.columnId);
      if (!columnIds.has(columnId)) {
        continue;
      }
      const columnElement = column.parentElement!;
      snapshot.columns.set(columnId, { element: columnElement, height: columnElement.getBoundingClientRect().height });
      for (const card of column.querySelectorAll<HTMLElement>(':scope > .card[data-card-id]')) {
        const id = Number(card.dataset.cardId);
        const moving = active.get(id);
        snapshot.cards.set(id, {
          rect: (moving?.floating ?? card).getBoundingClientRect(),
          clip: moving?.floating ? null : column.getBoundingClientRect(),
          columnId, floating: Boolean(moving?.floating),
          sortKey: card.dataset.cardSortKey!, foreground: moving?.foreground ?? false
        });
      }
    }
    // Preserve the visible positions before cancelling any previous effect.
    for (const [id, moving] of active) {
      if (columnIds.has(moving.columnId) || snapshot.cards.has(id)) {
        settle(id);
      }
    }
    for (const columnId of columnIds) {
      const animation = columnAnimations.get(columnId);
      if (animation) {
        animation.onfinish = null;
        animation.cancel();
        columnAnimations.delete(columnId);
      }
    }
    return snapshot;
  }

  function play(snapshot: MovementSnapshot) {
    if (!board || !layer || reducedMotion.matches) {
      return;
    }
    const boardRect = board.getBoundingClientRect();
    for (const card of board.querySelectorAll<HTMLElement>('.column-content > .card[data-card-id]')) {
      const id = Number(card.dataset.cardId);
      const previous = snapshot.cards.get(id);
      if (!previous) {
        continue;
      }
      const target = card.getBoundingClientRect();
      const dx = previous.rect.left - target.left;
      const dy = previous.rect.top - target.top;
      if (Math.abs(dx) < 0.5 && Math.abs(dy) < 0.5) {
        continue;
      }
      const columnId = Number(card.parentElement!.dataset.columnId);
      let floating: HTMLElement | null = null;
      let animation: Animation;
      const visibility = card.style.visibility;
      // A changed sort key identifies the moved card, rather than neighbours
      // whose positions shifted to make room. Preserve this when interrupted.
      const foreground = previous.foreground || previous.sortKey !== card.dataset.cardSortKey;
      // A second move in this column may displace a card still travelling in
      // from another column. Keep that card floating until it reaches home.
      if (columnId !== previous.columnId || previous.floating) {
        const targetClip = card.parentElement!.getBoundingClientRect();
        // An offscreen endpoint has no visible travel path. Keep it clipped in
        // its column rather than letting a floating copy cross the headers.
        const sourceClipped = previous.clip
          && (previous.rect.top < previous.clip.top || previous.rect.bottom > previous.clip.bottom);
        if (sourceClipped
          || target.top < targetClip.top || target.bottom > targetClip.bottom) {
          continue;
        }
        floating = card.cloneNode(true) as HTMLElement;
        floating.dataset.movingCardId = String(id);
        floating.removeAttribute('data-card-id');
        floating.classList.remove('card--dragging', 'card--multi-dragging');
        Object.assign(floating.style, {
          position: 'absolute', margin: '0', visibility: 'visible',
          left: `${target.left - boardRect.left + board.scrollLeft}px`,
          top: `${target.top - boardRect.top + board.scrollTop}px`,
          width: `${target.width}px`, height: `${target.height}px`
        });
        layer.append(floating);
        card.style.visibility = 'hidden';
        animation = floating.animate([
          { transform: `translate(${dx}px, ${dy}px)`, width: `${previous.rect.width}px`, height: `${previous.rect.height}px` },
          { transform: 'translate(0, 0)', width: `${target.width}px`, height: `${target.height}px` }
        ], { duration: 200, easing: 'cubic-bezier(0.2, 0, 0, 1)' });
        floatingCards.set(id, floating);
      } else {
        const zIndex = foreground ? '2' : getComputedStyle(card).zIndex;
        animation = card.animate([
          { transform: `translate(${dx}px, ${dy}px)`, zIndex },
          { transform: 'translate(0, 0)', zIndex }
        ], { duration: 200, easing: 'cubic-bezier(0.2, 0, 0, 1)' });
      }
      active.set(id, { animation, element: card, floating, visibility, columnId, foreground });
      animation.onfinish = () => {
        settle(id);
        refreshFrame();
      };
    }
    // Columns size to their content. Preserve their old clipping area while
    // neighbours close/open gaps, rather than clipping them at the final size.
    for (const [columnId, previous] of snapshot.columns) {
      if (!previous.element.isConnected) {
        continue;
      }
      const height = previous.element.getBoundingClientRect().height;
      if (Math.abs(height - previous.height) < 0.5) {
        continue;
      }
      const animation = previous.element.animate([
        { height: `${previous.height}px` }, { height: `${height}px` }
      ], { duration: 200, easing: 'cubic-bezier(0.2, 0, 0, 1)' });
      columnAnimations.set(columnId, animation);
      animation.onfinish = () => {
        columnAnimations.delete(columnId);
        animation.cancel();
        refreshFrame();
      };
    }
    refreshFrame();
  }

  function onMotionPreferenceChange() {
    if (reducedMotion.matches) {
      finishAll();
    }
  }

  function prune() {
    for (const moving of active.values()) {
      if (!moving.element.isConnected) {
        finishAll();
        return;
      }
    }
  }

  onMounted(() => {
    reducedMotion.addEventListener('change', onMotionPreferenceChange);
    window.addEventListener('resize', finishAll);
  });
  onBeforeUnmount(() => {
    reducedMotion.removeEventListener('change', onMotionPreferenceChange);
    window.removeEventListener('resize', finishAll);
    board?.removeEventListener('scroll', finishAll, true);
    finishAll();
  });

  return { floatingCards, setBoardRef, setLayerRef, capture, play, finishAll, prune };
}
