const { Liquid, WIDTH, HEIGHT, DT } = await import(`./physics.js?load=${Date.now()}`);
const { drawLiquid } = await import(`./render.js?load=${Date.now()}`);
const { drawSmooth } = await import(`./smooth.js?load=${Date.now()}`);
const { Settling } = await import(`./settling.js?load=${Date.now()}`);
const { snapshot, slickColours, populateSnapshotCard } = await import(`./snapshot.js?load=${Date.now()}`);
const $ = id => document.getElementById(id);
const stage = $('stage'), ctx = $('liquid').getContext('2d');
const SCROLL_ORIGIN = 480, HEADER = 56;
const demoColours = [{ colour: '#95d4aa', stroke: '#bbe8c7' }, { colour: '#ddbc83', stroke: '#f2d7a8' }];
let centres = [208, 480, 752], columnWidth = 250, cardWidth = 190, contentHeights = [], colours = demoColours;
const settings = {};
for (const name of ['attraction', 'viscosity', 'cohesion']) {
  const input = $(name), output = $(`${name}Value`);
  const update = () => { settings[name] = Number(input.value); output.value = Number(input.value).toFixed(2); };
  input.addEventListener('input', () => { update(); wakeAll(); }); update();
}
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
let cards = [], liquids = [], scrollers = [], scrollPositions = [], cardElements = [];
let slickIndicators = [];
let paused = false, moving = false, clock = 0, last = 0, accumulator = 0, drag;
let board = true, development = false, frameNumber = 0, dirty = true;
let renderer = 'liquid', renderCount = 0, lastWorkMs = 0;
let rafId = 0, inFrame = false, ready = false;
const settling = new Settling();
function requestFrame() {
  if (!ready || rafId || inFrame || document.hidden) return;
  last = 0; rafId = requestAnimationFrame(frame);
}
function invalidate() { dirty = true; requestFrame(); }
function wakeAll() {
  for (const liquid of liquids) settling.wake(liquid);
  invalidate();
}
function visible(liquid) {
  return liquid.cards.some(c => c.y + c.height / 2 >= HEADER - 120 && c.y - c.height / 2 <= HEIGHT + 120);
}
function shouldAnimate() {
  if (paused || document.hidden) return false;
  if (moving) return true;
  return renderer === 'liquid' && liquids.some(l => !$('sleep').checked || !settling.sleeping(l));
}
function stopMotion() { moving = false; $('motion').setAttribute('aria-pressed', 'false'); invalidate(); }
function positionCards() {
  cards.forEach((card, i) => {
    let x = card.x - card.width / 2, y = card.y - card.height / 2;
    if (board) {
      x = (columnWidth - card.width) / 2; y = SCROLL_ORIGIN + card.top;
      card.y = HEADER + y - scrollPositions[card.column] + card.height / 2;
    }
    cardElements[i].style.left = `${x}px`; cardElements[i].style.top = `${y}px`;
  });
  invalidate();
}
function scrollWeight(x, column) {
  if (x <= centres[0]) return Number(column === 0);
  if (x >= centres[2]) return Number(column === 2);
  let left = 0;
  if (x > centres[1]) left = 1;
  const distance = centres[left + 1] - centres[left];
  const t = (x - centres[left]) / distance;
  // Transport rigidly beside cards, blending only across the column gap.
  const edge = (cardWidth / 2 + 8) / distance;
  const blend = clamp((t - edge) / (1 - edge * 2), 0, 1);
  const weight = blend * blend * (3 - 2 * blend);
  if (column === left) return 1 - weight;
  if (column === left + 1) return weight;
  return 0;
}
function syncScroll() {
  if (!board) return;
  const deltas = scrollers.map((el, i) => el.scrollTop - scrollPositions[i]);
  if (!deltas.some(delta => delta !== 0)) return;
  scrollPositions = scrollers.map(el => el.scrollTop);
  for (const liquid of liquids) {
    let transported = false;
    for (const p of liquid.particles) {
      let dy = 0;
      for (let c = 0; c < deltas.length; c++) dy -= deltas[c] * scrollWeight(p.x, c);
      p.y += dy;
      if (dy !== 0) transported = true;
      // Scrolling changes the view, not velocity. Never reseed on scroll.
      if (p.py !== undefined) p.py += dy;
    }
    // Other visible slicks can meet obstacles from the moving column too.
    if (transported || visible(liquid) || liquid.cards.some(c => deltas[c.column])) settling.wake(liquid);
  }
  positionCards();
}
function moveCard(i, x, y) {
  const card = cards[i];
  if (board) {
    const others = cards.filter(c => c.column === card.column && c !== card);
    const previous = others.filter(c => c.row < card.row).at(-1);
    const next = others.find(c => c.row > card.row);
    let low = -SCROLL_ORIGIN + 12, high = contentHeights[card.column] - SCROLL_ORIGIN - card.height - 12;
    const gap = development ? 8 : 18;
    if (previous) low = previous.top + previous.height + gap;
    if (next) high = next.top - card.height - gap;
    card.top = clamp(y - HEADER + scrollPositions[card.column] - SCROLL_ORIGIN - card.height / 2, low, high);
  } else {
    const other = cards[1 - i];
    x = clamp(x, 130, WIDTH - 130); y = clamp(y, 145, HEIGHT - 85);
    if (i === 0) x = Math.min(x, other.x - 188);
    else x = Math.max(x, other.x + 188);
    card.x = x; card.y = y;
  }
  for (const liquid of liquids) {
    if (visible(liquid) || liquid.cards.includes(card)) settling.wake(liquid);
  }
  positionCards();
}
function makeCard(card, i, parent) {
  const el = document.createElement('button');
  el.className = 'card'; el.id = `card${i}`;
  el.style.width = `${card.width}px`; el.style.height = `${card.height}px`;
  el.dataset.group = String(card.group);
  el.setAttribute('aria-label', `${card.title}. Drag or use arrow keys to move`);
  const small = document.createElement('small'); small.textContent = card.group === 0 ? 'SLICK A' : 'SLICK B';
  const title = document.createElement('strong'); title.textContent = card.title;
  const hint = document.createElement('span'); hint.textContent = board ? 'Drag up or down · scroll this column' : 'Drag me · or use arrow keys';
  el.append(small, title, hint);
  el.classList.toggle('hidden', !$('cards').checked);
  parent.append(el); cardElements.push(el);
  if (development) populateSnapshotCard(el, card);
  el.addEventListener('pointerdown', event => {
    if (event.button !== 0) return;
    stopMotion(); syncScroll();
    drag = { i, x: event.clientX, y: event.clientY, cx: card.x, cy: card.y, scroll: scrollPositions[card.column] ?? 0 };
    el.setPointerCapture(event.pointerId);
  });
  el.addEventListener('pointermove', event => {
    if (drag?.i !== i) return;
    let dy = event.clientY - drag.y;
    if (board) dy -= scrollPositions[card.column] - drag.scroll;
    moveCard(i, drag.cx + event.clientX - drag.x, drag.cy + dy);
  });
  el.addEventListener('lostpointercapture', () => { drag = undefined; });
  el.addEventListener('keydown', event => {
    const deltas = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
    const delta = deltas[event.key]; if (!delta) return;
    event.preventDefault(); stopMotion(); syncScroll();
    const amount = event.shiftKey ? 20 : 5;
    moveCard(i, card.x + delta[0] * amount, card.y + delta[1] * amount);
  });
}
function buildScene() {
  stopMotion(); clock = 0; accumulator = 0; drag = undefined;
  stage.querySelectorAll('.column, .card').forEach(el => el.remove());
  cards = []; liquids = []; scrollers = []; cardElements = [];
  development = $('scene').value === 'development';
  board = $('scene').value !== 'pair';
  centres = development ? [174, 480, 786] : [208, 480, 752];
  columnWidth = development ? 280 : 250;
  cardWidth = development ? 244 : 190;
  contentHeights = [1500, 1500, 1500]; colours = demoColours;
  stage.classList.toggle('board-scene', board);
  stage.classList.toggle('development-scene', development);
  $('board-title').hidden = !development;
  $('slick-jump-label').hidden = !development;
  $('slick-jump').value = '';
  $('intro').textContent = board
    ? 'Scroll each column independently, in either direction. Drag cards up or down to explore the connections.'
    : 'Drag a card slowly, then pull it away. Compare how the two goo models respond.';
  $('motion').textContent = board ? 'Scroll for me' : 'Move for me';
  $('scene-hint').textContent = board
    ? 'Green and amber are separate slicks. Scroll up to move cards down from their starting positions; scroll down to reveal the next row.'
    : 'A finite pool, attracted to the card edges. The optional minimum coating follows each card immediately.';
  if (development) {
    buildDevelopment();
  } else if (board) {
    scrollPositions = [SCROLL_ORIGIN, SCROLL_ORIGIN, SCROLL_ORIGIN];
    const titles = [['A little attraction', 'A separate pool', 'Further down'], ['Across the gap', 'Room to flow', 'Back into view'], ['One more neighbour', 'A different shape', 'Still connected']];
    const heights = [[88, 112, 96], [124, 88, 112], [100, 132, 88]];
    for (let column = 0; column < 3; column++) {
      const el = document.createElement('section'); el.className = 'column'; el.style.left = `${centres[column] - 125}px`;
      const heading = document.createElement('h2'); heading.textContent = ['TO DO', 'IN PROGRESS', 'DONE'][column];
      const scroller = document.createElement('div'); scroller.className = 'scroller';
      scroller.setAttribute('aria-label', `${heading.textContent} column`); scroller.tabIndex = 0;
      const content = document.createElement('div'); content.className = 'scroll-content';
      scroller.append(content); el.append(heading, scroller); stage.append(el); scrollers.push(scroller);
      for (let row = 0; row < 3; row++) {
        const card = { id: `${column}-${row}`, column, row, group: row % 2, x: centres[column], y: 0, width: 190, height: heights[column][row], top: [95, 305, 575][row] + [15, -20, 0][column], title: titles[column][row] };
        cards.push(card); makeCard(card, cards.length - 1, content);
      }
      scroller.scrollTop = SCROLL_ORIGIN;
      scroller.addEventListener('scroll', syncScroll, { passive: true });
      scroller.addEventListener('wheel', stopMotion, { passive: true });
    }
    positionCards();
    for (let group = 0; group < 2; group++) {
      liquids.push(new Liquid(cards.filter(card => card.group === group), {
        board: true, obstacles: cards,
        bounds: { left: 18, right: WIDTH - 18, top: -3000, bottom: 3000 },
        activeBand: { top: HEADER - 120, bottom: HEIGHT + 120 },
      }));
    }
  } else {
    scrollPositions = [];
    cards = [
      { x: 360, y: 285, width: 180, height: 96, group: 0, title: 'A little attraction' },
      { x: 598, y: 250, width: 180, height: 96, group: 0, title: 'Follow the field' },
    ];
    cards.forEach((card, i) => makeCard(card, i, stage));
    positionCards(); liquids = [new Liquid(cards)];
  }
  slickIndicators = liquids.map((liquid, index) => {
    const group = liquid.group ?? index;
    const name = development ? snapshot.slicks[group].name : `Slick ${String.fromCharCode(65 + group)}`;
    const dot = document.createElement('span');
    dot.className = 'slick-indicator'; dot.setAttribute('role', 'img');
    dot.style.setProperty('--slick-colour', colours[group].colour);
    return { dot, name };
  });
  $('slick-activity').replaceChildren(...slickIndicators.map(({ dot }) => dot));
  invalidate();
}
function buildDevelopment() {
  scrollPositions = snapshot.columns.map(() => SCROLL_ORIGIN);
  colours = snapshot.slicks.map(slickColours);
  $('board-title').textContent = `${snapshot.name} · ${snapshot.columns.reduce((n, c) => n + c.cards.length, 0)} cards`;
  $('intro').textContent = 'Scroll each column independently; use “Jump to slick” to explore the development board’s groups.';
  $('scene-hint').textContent = `Local snapshot from ${snapshot.source.file}. Original card order, titles, types, tags and slick membership; card heights follow the rendered text. Reset restores this snapshot.`;
  snapshot.columns.forEach((source, column) => {
    const el = document.createElement('section'); el.className = 'column'; el.style.left = `${centres[column] - columnWidth / 2}px`;
    const heading = document.createElement('h2'); heading.textContent = `${source.title} · ${source.cards.length}`;
    const scroller = document.createElement('div'); scroller.className = 'scroller'; scroller.tabIndex = 0;
    scroller.setAttribute('aria-label', `${source.title} column`);
    const content = document.createElement('div'); content.className = 'scroll-content';
    scroller.append(content); el.append(heading, scroller); stage.append(el); scrollers.push(scroller);
    let top = 12, previous = null;
    source.cards.forEach((item, row) => {
      if (row === 0 && item.slickName) top += 8;
      if (previous && previous.slickName !== item.slickName) {
        top += previous.slickName && item.slickName ? 16 : 8;
      }
      const group = snapshot.slicks.findIndex(slick => slick.name === item.slickName);
      const card = { ...item, column, row, group, x: centres[column], y: 0, width: cardWidth, height: 80, radius: 12, top };
      cards.push(card); makeCard(card, cards.length - 1, content);
      top += card.height + 8; previous = item;
    });
    contentHeights[column] = Math.max(1500, SCROLL_ORIGIN + top + 160);
    content.style.height = `${contentHeights[column]}px`;
    scroller.scrollTop = SCROLL_ORIGIN;
    scroller.addEventListener('scroll', syncScroll, { passive: true });
    scroller.addEventListener('wheel', stopMotion, { passive: true });
  });
  positionCards();
  snapshot.slicks.forEach((slick, group) => {
    const members = cards.filter(card => card.group === group);
    if (!members.length) return;
    const liquid = new Liquid(members, {
      board: true, obstacles: cards,
      bounds: { left: 18, right: WIDTH - 18, top: -20000, bottom: 20000 },
      activeBand: { top: HEADER - 120, bottom: HEIGHT + 120 },
    });
    liquid.group = group; liquids.push(liquid);
  });
}
for (const slick of snapshot.slicks) {
  if (!snapshot.columns.some(c => c.cards.some(card => card.slickName === slick.name))) continue;
  const option = document.createElement('option'); option.value = slick.name; option.textContent = slick.name; $('slick-jump').append(option);
}
$('slick-jump').addEventListener('change', event => {
  if (!event.target.value) return;
  stopMotion();
  scrollers.forEach((scroller, column) => {
    const card = cards.find(c => c.column === column && c.slickName === event.target.value);
    if (card) scroller.scrollTop = SCROLL_ORIGIN + card.top - 24;
  });
  syncScroll();
});
$('scene').addEventListener('change', buildScene);
$('reset').addEventListener('click', buildScene);
$('pause').addEventListener('click', () => {
  paused = !paused; $('pause').textContent = paused ? 'Resume' : 'Pause'; $('pause').setAttribute('aria-pressed', String(paused)); invalidate();
});
$('motion').addEventListener('click', () => { moving = !moving; $('motion').setAttribute('aria-pressed', String(moving)); invalidate(); });
$('cards').addEventListener('change', event => {
  cardElements.forEach(el => el.classList.toggle('hidden', !event.target.checked)); invalidate();
});
$('coverage').addEventListener('change', wakeAll);
$('particles').addEventListener('change', invalidate);
$('sleep').addEventListener('change', wakeAll);
window.addEventListener('resize', wakeAll);
document.addEventListener('visibilitychange', () => {
  if (document.hidden) { cancelAnimationFrame(rafId); rafId = 0; last = 0; }
  else invalidate();
});
function changeRenderer() {
  syncScroll();
  renderer = $('renderer').value;
  const smooth = renderer === 'smooth';
  $('liquid-controls').disabled = smooth;
  $('coverage').disabled = smooth; $('particles').disabled = smooth; $('sleep').disabled = smooth;
  $('renderer-note').textContent = smooth
    ? 'Smooth follows the cards immediately. Liquid is paused and will resume when you switch back.'
    : 'Liquid stretches and settles over time. Pause to compare a frozen shape with Smooth.';
  // Keep the DOM, scroll positions, controls and particle state. Switching only
  // changes which engine draws and whether the particle solver advances.
  accumulator = 0; invalidate();
}
$('renderer').addEventListener('change', changeRenderer);
function draw() {
  ctx.clearRect(0, 0, WIDTH, HEIGHT);
  ctx.save(); ctx.beginPath(); ctx.rect(0, HEADER, WIDTH, HEIGHT - HEADER); ctx.clip();
  let loops = 0;
  liquids.forEach((liquid, group) => {
    const palette = colours[liquid.group ?? group];
    if (renderer === 'smooth') {
      loops += drawSmooth(ctx, liquid.cards, palette.colour);
    } else {
      loops += drawLiquid(ctx, liquid, {
        coverage: $('coverage').checked, particles: $('particles').checked, ...palette,
        reuseSurface: settling.sleeping(liquid),
        onSurface: contours => {
          if ($('sleep').checked) settling.observe(liquid, contours, liquid.steps * DT);
        },
      });
    }
  });
  ctx.restore(); dirty = false; renderCount++;
  return loops;
}
let loops = 0;
function frame(now) {
  rafId = 0; inFrame = true; frameNumber++;
  const runningElapsed = (now - (last || now)) / 1000;
  const elapsed = Math.min(runningElapsed, .05); last = now;
  const start = performance.now(); syncScroll();
  if (shouldAnimate()) {
    if (renderer === 'liquid' && $('sleep').checked) {
      for (const liquid of liquids) settling.advance(liquid, runningElapsed);
    }
    // Bound catch-up work: slow devices slow the experiment, not grow a queue.
    accumulator = Math.min(accumulator + elapsed, DT * 4);
    while (accumulator >= DT) {
      clock += DT;
      if (moving) {
        if (board) {
          scrollers[0].scrollTop = SCROLL_ORIGIN + 95 * Math.sin(clock * .6);
          scrollers[1].scrollTop = SCROLL_ORIGIN + 175 * Math.sin(clock * .48);
          scrollers[2].scrollTop = SCROLL_ORIGIN - 120 * Math.sin(clock * .55);
          syncScroll();
        } else moveCard(1, 580 + 90 * (1 - Math.cos(clock * .65)), 265 - 60 * Math.sin(clock * .8));
      }
      if (renderer === 'liquid') {
        for (const liquid of liquids) {
          if (!$('sleep').checked || !settling.sleeping(liquid)) liquid.step(settings);
        }
        dirty = true;
      }
      accumulator -= DT;
    }
  } else accumulator = 0;
  const updated = dirty;
  if (dirty) loops = draw();
  const ms = performance.now() - start;
  if (updated) lastWorkMs = ms;
  const particles = liquids.reduce((n, liquid) => n + liquid.particles.length, 0);
  let active = 0;
  if (renderer === 'liquid' && !paused) active = liquids.reduce((n, liquid) => {
    if (settling.sleeping(liquid)) return n;
    return n + liquid.activeParticles().length;
  }, 0);
  const sleeping = liquids.filter(l => settling.sleeping(l)).length, awake = liquids.length - sleeping;
  const running = shouldAnimate();
  window.liquidDiagnostics = {
    scene: $('scene').value, renderer, renderCount, cards: cards.length, particles, active, steps: liquids[0].steps, loops, ms, paused, coverage: $('coverage').checked,
    frameCount: frameNumber, awake, sleeping, sleepEnabled: $('sleep').checked, animationScheduled: running,
    stepsBySlick: liquids.map(l => l.steps), sleepingBySlick: liquids.map(l => settling.sleeping(l)),
    limitedBySlick: liquids.map(l => settling.limited(l)),
    scroll: [...scrollPositions], groups: liquids.map(l => l.particles.length),
    finite: liquids.every(l => l.particles.every(p => Number.isFinite(p.x + p.y + p.vx + p.vy))),
  };
  if (frameNumber % 30 === 0 || updated) {
    let detail = 'Smooth';
    if (renderer === 'liquid') detail = `Liquid · ${active} active / ${particles} particles`;
    $('status').textContent = `${cards.length} cards · ${detail} · ${lastWorkMs.toFixed(1)} ms last update`;
  }
  let sleepStatus = `${awake} awake · ${sleeping} sleeping`;
  if (renderer === 'smooth' || paused) sleepStatus = `Liquid paused · ${sleepStatus}`;
  if ($('sleep-status').textContent !== sleepStatus) $('sleep-status').textContent = sleepStatus;
  slickIndicators.forEach(({ dot, name }, index) => {
    let state = 'active';
    if (settling.sleeping(liquids[index])) state = 'sleeping';
    if (paused || renderer === 'smooth') state = 'paused';
    if (dot.dataset.state === state) return;
    dot.dataset.state = state;
    dot.title = `${name} · ${state}`;
    if (state === 'sleeping' && settling.limited(liquids[index])) dot.title += ' · 10s limit';
    dot.setAttribute('aria-label', dot.title);
  });
  inFrame = false;
  if (running) rafId = requestAnimationFrame(frame);
  else last = 0;
}
await document.fonts.load('600 14px Montserrat');
buildScene(); ready = true; changeRenderer(); requestFrame();
