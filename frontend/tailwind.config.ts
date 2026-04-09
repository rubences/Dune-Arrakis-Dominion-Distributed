import type { Config } from 'tailwindcss';

const config: Config = {
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        sand: {
          950: '#080604',
          900: '#100d08',
          800: '#1a1410',
          700: '#2a2016',
          600: '#3d2e1a',
          500: '#7a5c30',
          400: '#c4963a',
          300: '#e0b860',
          200: '#f0d090',
          100: '#f8ecd4',
        },
        gold:    '#ffd700',
        holo:    '#00b4d8',
        success: '#44ff88',
        danger:  '#ff4455',
        warning: '#ffcc44',
      },
      fontFamily: {
        cinzel:   ['Cinzel', 'serif'],
        rajdhani: ['Rajdhani', 'sans-serif'],
      },
      animation: {
        'fade-in-up':  'fadeInUp 0.4s ease both',
        'pulse-holo':  'pulseHolo 2s ease-in-out infinite',
        'shimmer':     'shimmer 3s linear infinite',
      },
      keyframes: {
        fadeInUp: {
          from: { opacity: '0', transform: 'translateY(16px)' },
          to:   { opacity: '1', transform: 'translateY(0)' },
        },
        pulseHolo: {
          '0%, 100%': { boxShadow: '0 0 6px rgba(0,180,216,0.3)' },
          '50%':      { boxShadow: '0 0 20px rgba(0,180,216,0.7)' },
        },
        shimmer: {
          from: { backgroundPosition: '-200% center' },
          to:   { backgroundPosition: '200% center' },
        },
      },
    },
  },
  plugins: [],
};

export default config;
