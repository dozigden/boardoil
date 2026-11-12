const { drawOrganicGoo } = await import(`./organic.js?load=${Date.now()}`);
const playgroundUrl = new URL(window.location.href);
if (playgroundUrl.searchParams.has('revision')) {
  playgroundUrl.searchParams.delete('revision');
  window.history.replaceState(null, '', playgroundUrl);
}

const $ = id => document.getElementById(id);
const canvas = $('goo');
const ctx = canvas.getContext('2d');
const colors = ['#96d7a7', '#a5aff1'];
// Start inside the scrollable workspace, leaving room above the initial cards.
const SCROLL_ORIGIN = 480;
const state = { cards: [], columns: [], playing: false, phase: 0, lastTime: 0, raf: 0, dirty: true, timings: [], lastReadout: 0 };
const names = ['A little closer', 'Across the gap', 'Meet in the middle', 'Another current', 'Room to breathe', 'Stay connected'];
const controls = ['gap', 'offset', 'reach', 'neck', 'renderer', 'cards', 'bounds', 'dark'];

function invalidate() {
  state.dirty = true;
  if (!state.raf) state.raf = requestAnimationFrame(frame);
}

function stop() {
  state.playing = false;
  $('motion').textContent = 'Play motion';
  $('motion').setAttribute('aria-pressed', 'false');
}

function createScene() {
  stop();
  state.phase = 0;
  state.cards = [];
  state.columns = [];
  $('columns').replaceChildren();
  const scene = $('scene').value;
  if (scene === 'diagonal') {
    $('gap').value = '32';
    $('offset').value = '-45';
  }
  let count = 2;
  if (scene === 'single') count = 1;
  if (scene === 'junction' || scene === 'board') count = 3;
  for (let col = 0; col < count; col++) {
    const column = document.createElement('section');
    column.className = 'column';
    const title = document.createElement('h2');
    title.textContent = ['To do', 'In progress', 'Done'][col];
    const scroller = document.createElement('div');
    scroller.className = 'scroller';
    scroller.setAttribute('aria-label', `${title.textContent} column`);
    const content = document.createElement('div');
    content.className = 'column-content';
    let cardCount = 1;
    if (scene === 'board') cardCount = 30;
    if (scene === 'groups') cardCount = 2;
    content.style.height = `${SCROLL_ORIGIN + Math.max(1100, cardCount * 125 + 200)}px`;
    for (let row = 0; row < cardCount; row++) {
      let y = 120 + row * 190;
      if (scene === 'junction' && col === 1) y = 160;
      if (scene === 'board') y = 38 + row * 125;
      let group = 0;
      if (scene === 'groups') group = row;
      if (scene === 'board') group = Math.floor(row / 4) % 2;
      const el = document.createElement('button');
      el.type = 'button';
      el.className = 'card';
      el.dataset.group = String(group);
      const label = document.createElement('small');
      label.textContent = ['Slick A', 'Slick B'][group];
      const name = document.createElement('span');
      name.textContent = names[(col + row) % names.length];
      el.append(label, name);
      const card = { el, col, group, y, baseY: y, row };
      positionCard(card);
      state.cards.push(card);
      wireCard(card);
      content.append(el);
    }
    scroller.append(content);
    scroller.addEventListener('scroll', invalidate, { passive:true });
    column.append(title, scroller);
    $('columns').append(column);
    state.columns.push(scroller);
    scroller.scrollTop = SCROLL_ORIGIN;
  }
  invalidate();
}

function positionCard(card) {
  let offset = 0;
  if (card.col === 1) offset = Number($('offset').value);
  card.el.style.top = `${SCROLL_ORIGIN + card.y + offset}px`;
}

function wireCard(card) {
  let drag = null;
  card.el.addEventListener('pointerdown', event => {
    if (event.button !== 0) return;
    stop();
    drag = { pointer: event.pointerId, y:event.clientY, origin:card.y };
    card.el.setPointerCapture(event.pointerId);
    card.el.focus({ preventScroll:true });
  });
  card.el.addEventListener('pointermove', event => {
    if (!drag || drag.pointer !== event.pointerId) return;
    card.y = Math.max(-SCROLL_ORIGIN + 14, drag.origin + event.clientY - drag.y);
    invalidate();
  });
  const finish = () => { drag = null; };
  card.el.addEventListener('pointerup', finish);
  card.el.addEventListener('pointercancel', finish);
  card.el.addEventListener('lostpointercapture', finish);
  card.el.addEventListener('keydown', event => {
    if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
    event.preventDefault();
    stop();
    let amount = 5;
    if (event.shiftKey) amount = 20;
    if (event.key === 'ArrowUp') amount = -amount;
    card.y = Math.max(-SCROLL_ORIGIN + 14, card.y + amount);
    invalidate();
  });
}

function collectRects() {
  const view = $('viewport').getBoundingClientRect();
  const world = $('world').getBoundingClientRect();
  const groups = [[], []];
  for (const card of state.cards) {
    const box = card.el.getBoundingClientRect();
    const top = box.top - world.top - 14;
    const bottom = box.bottom - world.top + 14;
    if (bottom <= 56 || top >= 490 || box.right < view.left - 300 || box.left > view.right + 300) continue;
    groups[card.group].push({ x:box.left - view.left - 1 - 14, y:top, width:box.width + 28, height:bottom - top, column:card.col });
  }
  return groups;
}

function drawFields(groups, width) {
  ctx.clearRect(0, 0, width, 490);
  ctx.save();
  ctx.beginPath();
  ctx.rect(0, 56, width, 434);
  ctx.clip();
  let loopCount = 0;
  groups.forEach((rects, index) => {
    if (!rects.length) return;
    loopCount += drawOrganicGoo(ctx, rects, Number($('reach').value), Number($('neck').value), colors[index], $('bounds').checked, $('renderer').value === 'overlay');
  });
  ctx.restore();
  return loopCount;
}

function drawLegacy(groups) {
  $('legacy').replaceChildren();
  groups.forEach((rects, index) => {
    const group = document.createElement('div');
    group.className = 'legacy-group';
    const blobs = rects.map(rect => ({ ...rect, x:rect.x + 11, y:rect.y + 11, width:rect.width - 22, height:rect.height - 22 }));
    if ($('renderer').value === 'legacy' || $('renderer').value === 'overlay') {
      const sorted = [...blobs].sort((a,b) => a.x - b.x);
      for (let i = 0; i < sorted.length; i++) {
        const a = sorted[i];
        for (let j = i + 1; j < sorted.length; j++) {
          const b = sorted[j];
          const gap = b.x - a.x - a.width;
          const ay = a.y + a.height / 2, by = b.y + b.height / 2;
          if (gap > 0 && gap <= 180 && Math.abs(ay - by) <= 96) {
            const h = Math.max(8, Math.min(a.height, b.height) * .38);
            blobs.push({ x:a.x + a.width - 8, y:(ay + by) / 2 - h / 2, width:gap + 16, height:h });
          }
        }
      }
    }
    for (const rect of blobs) {
      const blob = document.createElement('div');
      blob.className = 'legacy-blob';
      Object.assign(blob.style, { left:`${rect.x}px`, top:`${rect.y}px`, width:`${rect.width}px`, height:`${rect.height}px`, background:colors[index] });
      group.append(blob);
    }
    $('legacy').append(group);
  });
}

function frame(time) {
  state.raf = 0;
  if (state.playing) {
    if (state.lastTime) state.phase += Math.min(time - state.lastTime, 50) / 1000;
    $('gap').value = String(Math.round(66 - 54 * Math.cos(state.phase * .7)));
    $('offset').value = String(Math.round(95 * Math.sin(state.phase * .55)));
    state.dirty = true;
  }
  state.lastTime = time;
  if (state.dirty) {
    const start = performance.now();
    state.dirty = false;
    const gap = Number($('gap').value);
    $('world').style.width = `${80 + state.columns.length * 234 + (state.columns.length - 1) * gap}px`;
    $('columns').style.setProperty('--gap', `${gap}px`);
    for (const key of ['gap', 'offset', 'reach', 'neck']) $(`${key}-value`).textContent = `${$(key).value} px`;
    for (const card of state.cards) positionCard(card);
    const width = $('viewport').clientWidth;
    const dpr = window.devicePixelRatio || 1;
    if (canvas.width !== Math.round(width * dpr) || canvas.height !== Math.round(490 * dpr)) {
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(490 * dpr);
      canvas.style.width = `${width}px`;
      canvas.style.height = '490px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
    for (const el of [canvas, $('legacy')]) {
      el.style.left = `${$('viewport').scrollLeft}px`;
      el.style.width = `${width}px`;
    }
    const overlay = $('renderer').value === 'overlay';
    const field = $('renderer').value === 'field' || overlay;
    $('world').classList.toggle('overlay-outlines', overlay);
    $('outline-legend').hidden = !overlay;
    canvas.hidden = !field;
    $('legacy').hidden = field && !overlay;
    const groups = collectRects();
    let loops = 0;
    if (field) loops = drawFields(groups, width);
    if (!field || overlay) drawLegacy(groups);
    const elapsed = performance.now() - start;
    state.timings.push(elapsed);
    if (state.timings.length > 30) state.timings.shift();
    if (time - state.lastReadout > 250 || !state.playing) {
      const avg = state.timings.reduce((a,b) => a + b,0) / state.timings.length;
      const visible = groups.reduce((sum, items) => sum + items.length, 0);
      $('readout').textContent = `${visible} visible cards · ${avg.toFixed(1)} ms JS/draw`;
      if (field) $('readout').textContent += ` · ${loops} connections`;
      state.lastReadout = time;
    }
    // Read-only diagnostics for browser checks; no application data.
    window.gooDiagnostics = { renderer:$('renderer').value, visible:groups.flat().length, loops, elapsed };
  }
  if (state.playing) state.raf = requestAnimationFrame(frame);
}

for (const id of controls) $(id).addEventListener('input', () => {
  if (id === 'gap' || id === 'offset') stop();
  document.body.classList.toggle('light', !$('dark').checked);
  $('world').classList.toggle('hide-cards', !$('cards').checked);
  const field = $('renderer').value === 'field' || $('renderer').value === 'overlay';
  $('reach').disabled = !field;
  $('neck').disabled = !field;
  $('bounds').disabled = !field;
  let description = 'Card surrounds and bounded necks blend into one surface, leaving pockets between connections.';
  if ($('renderer').value === 'legacy') description = 'The current recipe: extra bridge rectangles, followed by blur and an alpha threshold.';
  if ($('renderer').value === 'overlay') description = 'Solid blue traces the original filter; dashed coral traces the experiment. Both follow the same cards.';
  if ($('renderer').value === 'bare') description = 'The current blur and alpha threshold, with artificial bridge rectangles removed.';
  $('description').textContent = description;
  state.timings = [];
  invalidate();
});
$('scene').addEventListener('change', createScene);
$('reset').addEventListener('click', () => {
  $('gap').value = '32';
  $('offset').value = '0';
  $('reach').value = '36';
  $('neck').value = '44';
  $('viewport').scrollLeft = 0;
  createScene();
});
$('motion').addEventListener('click', () => {
  if (state.playing) stop();
  else {
    state.playing = true;
    state.lastTime = 0;
    $('motion').textContent = 'Pause motion';
    $('motion').setAttribute('aria-pressed', 'true');
    invalidate();
  }
});
$('viewport').addEventListener('scroll', invalidate, { passive:true });
window.addEventListener('resize', invalidate);
document.addEventListener('visibilitychange', () => { if (document.hidden) stop(); });
$('gap').value = '32';
$('reach').value = '36';
const requestedScene = new URLSearchParams(window.location.search).get('scene');
if ([...$('scene').options].some(option => option.value === requestedScene)) $('scene').value = requestedScene;
createScene();
