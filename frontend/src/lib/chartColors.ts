import { useEffect, useState } from 'react';

/** Validated chart palette (light/dark steps chosen separately, checked for CVD separation and contrast). */
const LIGHT = {
  series1: '#6366f1',
  series2: '#d97706',
  grid: '#e4e4e7',
  axis: '#71717a',
  surface: '#ffffff',
  cursor: 'rgba(24,24,27,0.05)',
};
const DARK = {
  series1: '#7c83f5',
  series2: '#d97706',
  grid: '#27272a',
  axis: '#a1a1aa',
  surface: '#18181b',
  cursor: 'rgba(255,255,255,0.06)',
};

export type ChartColors = typeof LIGHT;

function isDark() {
  return document.documentElement.classList.contains('dark');
}

/** Chart colors that follow the `dark` class on <html>. */
export function useChartColors(): ChartColors {
  const [dark, setDark] = useState(isDark);
  useEffect(() => {
    const obs = new MutationObserver(() => setDark(isDark()));
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    return () => obs.disconnect();
  }, []);
  return dark ? DARK : LIGHT;
}
