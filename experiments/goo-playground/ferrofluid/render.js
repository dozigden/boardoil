const { cardDistance, WIDTH, HEIGHT } = await import(`./physics.js?load=${Date.now()}`);
const { traceContours } = await import('../baseline/contours.js');
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const gridStep = 3, originY = -36;
const nx = WIDTH / gridStep + 1, ny = Math.ceil((HEIGHT - originY * 2) / gridStep) + 1;
const field = new Float32Array(nx * ny), renderRadius = 17;

function addCardCoating(cards) {
  // This is a rendering floor, not extra simulated liquid. Smooth union joins
  // the minimum coating to the particle surface without a separate outline seam.
  const padding = 8, falloff = 12, blendWidth = .25;
  for (const card of cards) {
    const x0 = Math.max(0, Math.floor((card.x - card.width / 2 - 20) / gridStep));
    const x1 = Math.min(nx - 1, Math.ceil((card.x + card.width / 2 + 20) / gridStep));
    const y0 = Math.max(0, Math.floor((card.y - card.height / 2 - 20 - originY) / gridStep));
    const y1 = Math.min(ny - 1, Math.ceil((card.y + card.height / 2 + 20 - originY) / gridStep));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const distance = cardDistance(x * gridStep, y * gridStep + originY, card).distance;
      const coating = clamp(.5 + (padding - distance) / falloff, 0, 1);
      if (coating === 0) continue;
      const i = y * nx + x;
      const blend = Math.max(0, 1 - Math.abs(field[i] - coating) / blendWidth);
      field[i] = Math.max(field[i], coating) + blendWidth * blend * blend / 4;
    }
  }
}
export function drawLiquid(ctx, liquid, { coverage, particles, colour, stroke }) {
  const visibleCards = liquid.cards.some(card => card.y + card.height / 2 + 24 >= originY && card.y - card.height / 2 - 24 <= HEIGHT - originY);
  const visibleParticles = liquid.particles.some(p => p.y >= originY - renderRadius && p.y <= HEIGHT - originY + renderRadius);
  if (!visibleParticles && (!coverage || !visibleCards)) return 0;
  field.fill(0);
  for (const p of liquid.particles) {
    if (p.y < originY - renderRadius || p.y > HEIGHT - originY + renderRadius) continue;
    const x0 = Math.max(0, Math.floor((p.x - renderRadius) / gridStep)), x1 = Math.min(nx - 1, Math.ceil((p.x + renderRadius) / gridStep));
    const y0 = Math.max(0, Math.floor((p.y - renderRadius - originY) / gridStep)), y1 = Math.min(ny - 1, Math.ceil((p.y + renderRadius - originY) / gridStep));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const q = 1 - ((x * gridStep - p.x) ** 2 + (y * gridStep + originY - p.y) ** 2) / renderRadius ** 2;
      if (q > 0) field[y * nx + x] += q ** 3 * .7;
    }
  }
  // Keep each card's rounded corners in the visible coating. The solver uses
  // smooth stack bodies independently, so these indents cannot trap particles.
  if (coverage) addCardCoating(liquid.cards);
  // Close contours outside the visible area, including surfaces cut by scroll.
  // Clipping the field at the viewport itself would discard open outlines.
  field.fill(0, 0, nx); field.fill(0, (ny - 1) * nx);
  const loops = traceContours(field, nx, ny, 0, originY, gridStep);
  ctx.beginPath();
  for (const loop of loops) {
    ctx.moveTo(...loop[0]); for (let i = 1; i < loop.length; i++) ctx.lineTo(...loop[i]); ctx.closePath();
  }
  ctx.fillStyle = colour; ctx.fill('evenodd'); ctx.strokeStyle = stroke; ctx.lineWidth = .65; ctx.stroke();
  if (particles) {
    ctx.fillStyle = '#163c2c'; ctx.beginPath();
    for (const p of liquid.particles) { ctx.moveTo(p.x + 1.5, p.y); ctx.arc(p.x, p.y, 1.5, 0, Math.PI * 2); }
    ctx.fill();
  }
  return loops.length;
}
