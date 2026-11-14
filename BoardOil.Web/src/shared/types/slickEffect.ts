export type SlickEffect = 'none' | 'bridges' | 'liquid';

export const slickEffectOptions: { value: SlickEffect; label: string }[] = [
  { value: 'none', label: 'None' },
  { value: 'bridges', label: 'Bridges' },
  { value: 'liquid', label: 'Liquid' }
];
