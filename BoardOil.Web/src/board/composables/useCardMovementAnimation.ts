import { onBeforeUnmount, onMounted, shallowReactive } from 'vue';
import type { BoardAnimationHandle, BoardAnimationRunner } from '../utils/boardAnimationRunner';

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
  animation: BoardAnimationHandle;
  element: HTMLElement;
  floating: HTMLElement | null;
  columnId: number;
  foreground: boolean;
};

export function useCardMovementAnimation(runner: BoardAnimationRunner) {
  const floatingCards = shallowReactive(new Map<number, HTMLElement>());
  const active = new Map<number, MovingCard>();
  const columnAnimations = new Map<number, BoardAnimationHandle>();
  let board: HTMLElement | null = null;
  let layer: HTMLElement | null = null;

  function settle(cardId: number) {
    active.get(cardId)?.animation.cancel();
  }

  function finishAll() {
    if (active.size === 0 && columnAnimations.size === 0) {
      return;
    }
    for (const cardId of active.keys()) {
      settle(cardId);
    }
    for (const animation of columnAnimations.values()) {
      animation.finish();
    }
    columnAnimations.clear();
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
    if (!board || runner.reducedMotion) {
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
        animation.cancel();
      }
    }
    return snapshot;
  }

  function play(snapshot: MovementSnapshot) {
    if (!board || !layer || runner.reducedMotion) {
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
      let keyframes: Keyframe[];
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
        keyframes = [
          { transform: `translate(${dx}px, ${dy}px)`, width: `${previous.rect.width}px`, height: `${previous.rect.height}px` },
          { transform: 'translate(0, 0)', width: `${target.width}px`, height: `${target.height}px` }
        ];
        floatingCards.set(id, floating);
      } else {
        const zIndex = foreground ? '2' : getComputedStyle(card).zIndex;
        keyframes = [
          { transform: `translate(${dx}px, ${dy}px)`, zIndex },
          { transform: 'translate(0, 0)', zIndex }
        ];
      }
      const cleanup = () => {
        card.style.visibility = visibility;
        floating?.remove();
        active.delete(id);
        floatingCards.delete(id);
      };
      const animation = runner.animate(floating ?? card, keyframes, { onFinish: cleanup, onCancel: cleanup });
      if (animation.active) {
        active.set(id, { animation, element: card, floating, columnId, foreground });
      }
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
      const cleanup = () => { columnAnimations.delete(columnId); };
      const animation = runner.animate(previous.element, [
        { height: `${previous.height}px` }, { height: `${height}px` }
      ], { onFinish: cleanup, onCancel: cleanup });
      if (animation.active) {
        columnAnimations.set(columnId, animation);
      }
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
    window.addEventListener('resize', finishAll);
  });
  onBeforeUnmount(() => {
    window.removeEventListener('resize', finishAll);
    board?.removeEventListener('scroll', finishAll, true);
    finishAll();
  });

  return { floatingCards, setBoardRef, setLayerRef, capture, play, finishAll, prune };
}
