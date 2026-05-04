import { reactive, readonly } from 'vue';

type ConfirmRequest = {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
};

type PendingConfirm = {
  request: ConfirmRequest;
  resolve: (accepted: boolean) => void;
};

const queue: PendingConfirm[] = [];
let active: PendingConfirm | null = null;

const state = reactive({
  open: false,
  title: '',
  message: '',
  confirmLabel: 'Confirm',
  cancelLabel: 'Cancel',
  danger: false
});

function showNext() {
  if (active !== null || queue.length === 0) {
    return;
  }

  active = queue.shift() ?? null;
  if (!active) {
    return;
  }

  state.title = active.request.title;
  state.message = active.request.message;
  state.confirmLabel = active.request.confirmLabel;
  state.cancelLabel = active.request.cancelLabel;
  state.danger = active.request.danger;
  state.open = true;
}

function closeWith(value: boolean) {
  if (!active) {
    return;
  }

  active.resolve(value);
  active = null;
  state.open = false;
  showNext();
}

export function useConfirm() {
  async function confirm(options: {
    message: string;
    title?: string;
    confirmLabel?: string;
    cancelLabel?: string;
    danger?: boolean;
  }) {
    const request: ConfirmRequest = {
      title: options.title ?? 'Confirm action',
      message: options.message,
      confirmLabel: options.confirmLabel ?? 'Confirm',
      cancelLabel: options.cancelLabel ?? 'Cancel',
      danger: options.danger ?? false
    };

    return await new Promise<boolean>(resolve => {
      queue.push({ request, resolve });
      showNext();
    });
  }

  return { confirm };
}

export function useConfirmDialogState() {
  return {
    state: readonly(state),
    accept: () => closeWith(true),
    cancel: () => closeWith(false)
  };
}
