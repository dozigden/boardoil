import { traceContours } from './contours';
import type { SurfaceRect, Neck, Viewport } from './types';

export function blendedOutline(cards: SurfaceRect[], necks: Neck[], viewport: Viewport) {
  const step = 1.5;
  const softness = 24;
  const pad = softness + step * 2;
  const originX = Math.floor(Math.max(viewport.left - pad, Math.min(...cards.map(r=>r.left))-pad)/step)*step;
  const originY = Math.floor(Math.max(viewport.top - pad, Math.min(...cards.map(r=>r.top))-pad)/step)*step;
  const nx = Math.ceil((Math.min(viewport.left+viewport.width+pad, Math.max(...cards.map(r=>r.left+r.width))+pad)-originX)/step)+1;
  const ny = Math.ceil((Math.min(viewport.top+viewport.height+pad, Math.max(...cards.map(r=>r.top+r.height))+pad)-originY)/step)+1;
  if (nx < 3 || ny < 3) return [];
  const distances = new Float32Array(nx*ny).fill(softness);

  function merge(bounds: Omit<SurfaceRect, 'column'>, distance: (x: number, y: number) => number, blendWidth = softness) {
    const x0 = Math.max(1,Math.floor((bounds.left-pad-originX)/step));
    const y0 = Math.max(1,Math.floor((bounds.top-pad-originY)/step));
    const x1 = Math.min(nx-2,Math.ceil((bounds.left+bounds.width+pad-originX)/step));
    const y1 = Math.min(ny-2,Math.ceil((bounds.top+bounds.height+pad-originY)/step));
    for (let y=y0;y<=y1;y++) {
      for (let x=x0;x<=x1;x++) {
        const i=y*nx+x;
        const d=distance(originX+x*step,originY+y*step);
        const old=distances[i];
        const blend=Math.max(0,1-Math.abs(old-d)/blendWidth);
        // Compact smooth union: remote surfaces cannot inflate a card.
        distances[i]=Math.min(old,d)-blendWidth*blend*blend/4;
      }
    }
  }

  for (const card of cards) {
    const radius=Math.min(20,card.width/2,card.height/2);
    const hw=card.width/2, hh=card.height/2;
    const cx=card.left+hw, cy=card.top+hh;
    merge(card,(x,y)=>{
      const qx=Math.abs(x-cx)-hw+radius, qy=Math.abs(y-cy)-hh+radius;
      const ox=Math.max(qx,0), oy=Math.max(qy,0);
      return Math.sqrt(ox*ox+oy*oy)+Math.min(Math.max(qx,qy),0)-radius;
    }, 6);
  }
  for (const neck of necks) {
    const dx=neck.x1-neck.x0, dy=neck.y1-neck.y0;
    const length=Math.hypot(dx,dy);
    const ux=dx/length, uy=dy/length;
    const cx=(neck.x0+neck.x1)/2, cy=(neck.y0+neck.y1)/2;
    const r=neck.radius;
    const rounding=Math.min(10,r,length/2);
    merge({left:Math.min(neck.x0,neck.x1)-r,top:Math.min(neck.y0,neck.y1)-r,width:Math.abs(dx)+r*2,height:Math.abs(dy)+r*2},(x,y)=>{
      // The neck ends within its cards. Circular end caps had extended into
      // neighbouring row gaps and split the reference's continuous channels.
      const px=x-cx, py=y-cy;
      const a=Math.abs(px*ux+py*uy)-length/2+rounding;
      const b=Math.abs(-px*uy+py*ux)-r+rounding;
      const ox=Math.max(a,0), oy=Math.max(b,0);
      return Math.sqrt(ox*ox+oy*oy)+Math.min(Math.max(a,b),0)-rounding;
    });
  }
  const field=new Float32Array(nx*ny);
  for(let i=0;i<distances.length;i++) field[i]=.5-distances[i];
  return traceContours(field,nx,ny,originX,originY,step);
}
