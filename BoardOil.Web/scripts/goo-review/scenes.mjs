export const scenes = [
  ['single', [{}]],
  ['stack', [{}, { centreY: 276 }]],
  ['diagonal', [{}, { centreX: 520, centreY: 225, column: 1 }]],
  ['junction', [{}, { centreY: 276 }, { centreX: 520, centreY: 215, column: 1 }]],
  ['competing', [{}, { centreY: 284, group: 2 }, { centreY: 412, group: 3 },
    { centreX: 520, centreY: 245, column: 1 }]],
  ['unassigned', [{}, { centreY: 284, group: null }, { centreY: 412 }]],
  ['clipped', [{ centreX: -90, centreY: -100 }, { centreX: 240, centreY: 0, column: 1 },
    { centreX: 240, centreY: 780, column: 1 }]],
  ['long-board', Array.from({ length: 24 }, (_, i) => ({ column: i % 2,
    centreX: 190 + (i % 2) * 330, centreY: 160 + Math.floor(i / 2) * 180, group: 1 + i % 3 }))]
].map(([name, cards]) => ({ name, cards: cards.map((card, index) => ({
  id: index + 1, group: 1, column: 0, centreX: 190, centreY: 160, width: 244, height: 100, radius: 12, ...card
})) }));

export const stages = [
  { name: 'initial', left: 0, top: 0, width: 800, height: 600, steps: 0 },
  { name: 'moving', left: 0, top: 0, width: 800, height: 600, steps: 24 },
  { name: 'vertical-pan', left: 0, top: 180, width: 800, height: 600, steps: 4 },
  { name: 'small-reversal', left: 0, top: 160, width: 800, height: 600, steps: 4 },
  { name: 'distant', left: 0, top: 1600, width: 800, height: 600, steps: 4 },
  { name: 'return', left: 0, top: 0, width: 800, height: 600, steps: 4 },
  { name: 'fractional-clip', left: 170.25, top: 130.5, width: 400, height: 300, steps: 4 }
];
