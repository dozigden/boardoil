import { describe, expect, it } from 'vitest';
import { createBoardLayoutRegistry, type BoardLayoutConveyorConfig } from './boardLayoutRegistry';

function makeConfig(overrides?: Partial<BoardLayoutConveyorConfig>): BoardLayoutConveyorConfig {
  return {
    highlighted: false,
    leftLabel: null,
    leftAriaLabel: null,
    leftDisabled: false,
    rightLabel: null,
    rightAriaLabel: null,
    rightDisabled: false,
    onLeftClick: null,
    onRightClick: null,
    ...overrides
  };
}

describe('boardLayoutRegistry', () => {
  it('updates conveyor config for active registration', () => {
    const registry = createBoardLayoutRegistry();
    const registration = registry.registerConveyor(makeConfig({ rightLabel: 'Archive' }));

    registration.update(makeConfig({ highlighted: true, rightLabel: 'Archive (3)', rightDisabled: true }));

    expect(registry.conveyorConfig.value.highlighted).toBe(true);
    expect(registry.conveyorConfig.value.rightLabel).toBe('Archive (3)');
    expect(registry.conveyorConfig.value.rightDisabled).toBe(true);
  });

  it('resets conveyor config when registration is disposed', () => {
    const registry = createBoardLayoutRegistry();
    const registration = registry.registerConveyor(makeConfig({ leftLabel: 'Board' }));

    registration.dispose();

    expect(registry.conveyorConfig.value.leftLabel).toBeNull();
    expect(registry.conveyorConfig.value.rightLabel).toBeNull();
    expect(registry.conveyorConfig.value.highlighted).toBe(false);
  });

  it('prevents stale registrations from overwriting active config', () => {
    const registry = createBoardLayoutRegistry();
    const first = registry.registerConveyor(makeConfig({ leftLabel: 'Old' }));
    const second = registry.registerConveyor(makeConfig({ rightLabel: 'New' }));

    first.update(makeConfig({ leftLabel: 'Should not apply' }));
    second.update(makeConfig({ rightLabel: 'Still new' }));
    first.dispose();

    expect(registry.conveyorConfig.value.leftLabel).toBeNull();
    expect(registry.conveyorConfig.value.rightLabel).toBe('Still new');
  });
});
