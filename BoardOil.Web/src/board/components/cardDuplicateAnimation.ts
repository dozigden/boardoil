export function animateCardDuplicate(dialog: HTMLDialogElement): (() => void) | undefined {
  const surface = dialog.querySelector<HTMLElement>('.fixed-chrome-dialog__surface');
  if (!surface || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    return;
  }

  // Freeze the original visually while the live editor becomes the duplicate draft.
  const original = surface.cloneNode(true) as HTMLElement;
  original.classList.add('card-duplicate-original');
  original.inert = true;
  original.setAttribute('aria-hidden', 'true');
  original.querySelectorAll('[id]').forEach(element => element.removeAttribute('id'));

  const stage = document.createElement('div');
  stage.className = 'card-duplicate-stage';
  stage.setAttribute('aria-hidden', 'true');
  dialog.prepend(stage, original);
  const sourceElements = surface.querySelectorAll<HTMLElement>('*');
  original.querySelectorAll<HTMLElement>('*').forEach((element, index) => {
    element.scrollTop = sourceElements[index]!.scrollTop;
    element.scrollLeft = sourceElements[index]!.scrollLeft;
  });

  dialog.classList.add('card-editor-dialog--splitting');
  surface.inert = true;
  const duration = 1100;
  const easing = 'cubic-bezier(0.4, 0, 0.2, 1)';
  const exitDistance = -(dialog.getBoundingClientRect().left + dialog.clientWidth / 2 + 40);

  const originalAnimation = original.animate([
    { transform: 'translateX(0) scaleX(1)', offset: 0, easing },
    { transform: 'translateX(0) scaleX(0.5)', offset: 0.28 },
    { transform: 'translateX(0) scaleX(0.5)', offset: 0.44, easing },
    { transform: 'translateX(-8px) scaleX(0.49)', offset: 0.54, easing },
    { transform: `translateX(${exitDistance}px) scaleX(0.49)`, offset: 1 }
  ], { duration, fill: 'both' });

  const copyAnimation = surface.animate([
    { transform: 'translateX(50%) scaleX(0.5)', opacity: 0, offset: 0 },
    { transform: 'translateX(50%) scaleX(0.5)', opacity: 0, offset: 0.28, easing },
    { transform: 'translateX(50%) scaleX(0.5)', opacity: 1, offset: 0.44, easing },
    { transform: 'translateX(calc(50% + 8px)) scaleX(0.49)', opacity: 1, offset: 0.54, easing },
    { transform: 'translateX(0) scaleX(1)', opacity: 1, offset: 1 }
  ], { duration, fill: 'both' });

  const stageAnimation = stage.animate([
    { opacity: 1, offset: 0 },
    { opacity: 1, offset: 0.44 },
    { opacity: 0, offset: 0.54 },
    { opacity: 0, offset: 1 }
  ], { duration, fill: 'both' });

  function cleanup() {
    originalAnimation.cancel();
    copyAnimation.cancel();
    stageAnimation.cancel();
    original.remove();
    stage.remove();
    surface!.inert = false;
    dialog.classList.remove('card-editor-dialog--splitting');
  }

  copyAnimation.onfinish = cleanup;
  return cleanup;
}
