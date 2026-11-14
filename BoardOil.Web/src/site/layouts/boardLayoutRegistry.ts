import { inject, provide, ref, type InjectionKey, type Ref } from 'vue';

export type BoardLayoutConveyorConfig = {
  highlighted: boolean;
  leftLabel: string | null;
  leftAriaLabel: string | null;
  leftDisabled: boolean;
  rightLabel: string | null;
  rightAriaLabel: string | null;
  rightDisabled: boolean;
  onLeftClick: (() => void | Promise<void>) | null;
  onRightClick: (() => void | Promise<void>) | null;
};

export type BoardLayoutConveyorRegistration = {
  update: (next: BoardLayoutConveyorConfig) => void;
  dispose: () => void;
};

export type BoardLayoutRegistry = {
  conveyorConfig: Readonly<Ref<BoardLayoutConveyorConfig>>;
  conveyorContentTargetId: string;
  registerConveyor: (initial: BoardLayoutConveyorConfig) => BoardLayoutConveyorRegistration;
};

const BOARD_LAYOUT_REGISTRY_KEY: InjectionKey<BoardLayoutRegistry> = Symbol('board-layout-registry');

const DEFAULT_CONVEYOR_CONFIG: BoardLayoutConveyorConfig = {
  highlighted: false,
  leftLabel: null,
  leftAriaLabel: null,
  leftDisabled: false,
  rightLabel: null,
  rightAriaLabel: null,
  rightDisabled: false,
  onLeftClick: null,
  onRightClick: null
};

let nextRegistryId = 1;

export function createBoardLayoutRegistry() {
  const conveyorConfig = ref<BoardLayoutConveyorConfig>({ ...DEFAULT_CONVEYOR_CONFIG });
  const conveyorContentTargetId = `board-layout-conveyor-content-${nextRegistryId++}`;
  let activeOwner: symbol | null = null;

  function registerConveyor(initial: BoardLayoutConveyorConfig): BoardLayoutConveyorRegistration {
    const owner = Symbol('board-layout-conveyor-owner');
    activeOwner = owner;
    conveyorConfig.value = { ...initial };

    return {
      update(next: BoardLayoutConveyorConfig) {
        if (activeOwner !== owner) {
          return;
        }

        conveyorConfig.value = { ...next };
      },
      dispose() {
        if (activeOwner !== owner) {
          return;
        }

        activeOwner = null;
        conveyorConfig.value = { ...DEFAULT_CONVEYOR_CONFIG };
      }
    };
  }

  const registry: BoardLayoutRegistry = {
    conveyorConfig,
    conveyorContentTargetId,
    registerConveyor
  };

  return registry;
}

export function provideBoardLayoutRegistry() {
  const registry = createBoardLayoutRegistry();
  provide(BOARD_LAYOUT_REGISTRY_KEY, registry);
  return registry;
}

export function useBoardLayoutRegistry(): BoardLayoutRegistry;
export function useBoardLayoutRegistry(required: true): BoardLayoutRegistry;
export function useBoardLayoutRegistry(required: false): BoardLayoutRegistry | null;
export function useBoardLayoutRegistry(required = true) {
  const registry = inject(BOARD_LAYOUT_REGISTRY_KEY, null);
  if (!registry && required) {
    throw new Error('Board layout registry is not available in this route layout.');
  }

  return registry;
}
