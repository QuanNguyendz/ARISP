import sharedPreset from '../ARI.Shared/tailwind-preset.cjs'

/** @type {import('tailwindcss').Config} */
export default {
  presets: [sharedPreset],
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
    '../ARI.Shared/src/**/*.{js,ts,jsx,tsx}',
  ],
}
