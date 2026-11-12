const response = await fetch('./development-snapshot.json');
if (!response.ok) throw new Error(`Could not load development snapshot (${response.status})`);
export const snapshot = await response.json();
// BoardOil's dark theme palette. This standalone study uses the export's
// canonical names and style values without importing the application's stores.
const presets = ['#59417f', '#8d5377', '#286b78', '#735d32', '#823b47', '#3b5c94', '#854b32', '#28694c', '#5f7730', '#6f6660', '#756b2d', '#414a57'];
function mix(a, b, weight) {
  const channels = [1, 3, 5].map(i => Math.round(parseInt(a.slice(i, i + 2), 16) * (1 - weight) + parseInt(b.slice(i, i + 2), 16) * weight));
  return `#${channels.map(c => c.toString(16).padStart(2, '0')).join('')}`;
}
function ink(colour) {
  const rgb = [1, 3, 5].map(i => parseInt(colour.slice(i, i + 2), 16) / 255).map(v => v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4);
  const luminance = .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
  return luminance > .179 ? '#171c24' : '#f0f3fa';
}
function applyStyle(el, definition) {
  const properties = JSON.parse(definition.stylePropertiesJson);
  let background = '#1e2633', sample = background;
  if (definition.styleName === 'presets') background = sample = presets[properties.presetIndex];
  if (definition.styleName === 'solid') background = sample = properties.backgroundColor;
  if (definition.styleName === 'gradient') {
    background = `linear-gradient(105deg, ${properties.leftColor}, ${properties.rightColor})`;
    sample = mix(properties.leftColor, properties.rightColor, .5);
  }
  el.style.background = background; el.style.color = ink(sample);
  el.style.borderColor = mix(sample, '#ffffff', .25);
}
export function slickColours(slick) {
  const properties = JSON.parse(slick.stylePropertiesJson);
  let colour = properties.backgroundColor;
  if (slick.styleName === 'presets') colour = mix(presets[properties.presetIndex], '#1e2633', .12);
  return { colour, stroke: mix(colour, '#ffffff', .25) };
}
export function populateSnapshotCard(el, card) {
  el.classList.add('snapshot-card');
  el.dataset.cardId = String(card.id);
  const type = snapshot.cardTypes.find(t => t.name === card.cardTypeName);
  applyStyle(el, type);
  const heading = document.createElement('div'); heading.className = 'snapshot-card-heading';
  const title = document.createElement('strong');
  title.textContent = `${type.emoji ? `${type.emoji} ` : ''}${card.title}`;
  const number = document.createElement('span'); number.className = 'snapshot-card-number'; number.textContent = `#${card.id}`;
  heading.append(title, number); el.replaceChildren(heading);
  if (card.tagNames.length) {
    const tags = document.createElement('div'); tags.className = 'snapshot-tags';
    for (const name of card.tagNames) {
      const definition = snapshot.tags.find(t => t.name === name);
      const tag = document.createElement('span'); tag.className = 'snapshot-tag';
      tag.textContent = `${definition.emoji ? `${definition.emoji} ` : ''}${name}`;
      applyStyle(tag, definition); tags.append(tag);
    }
    el.append(tags);
  }
  el.title = card.slickName ? `${type.name} · ${card.slickName}` : type.name;
  // Use actual wrapping at the board card width to size the physical obstacle.
  el.style.height = 'auto';
  card.height = Math.ceil(el.getBoundingClientRect().height);
  el.style.height = `${card.height}px`;
}
