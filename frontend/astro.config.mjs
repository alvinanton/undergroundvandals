// @ts-check

import { defineConfig } from 'astro/config';

import tailwindcss from '@tailwindcss/vite';

import vercel from '@astrojs/vercel';

import node from '@astrojs/node';

// @ts-ignore
const isStandalone = process.env.OUTPUT_ADAPTER === 'node';

// https://astro.build/config
export default defineConfig({
  devToolbar: {
    enabled: false
  },
  
  output: 'server',

  vite: {
    plugins: [tailwindcss()]
  },

  adapter: isStandalone
    ? node({ mode: 'standalone' })
    : vercel(),
});