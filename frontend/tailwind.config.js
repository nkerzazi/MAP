/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        ink: { DEFAULT: '#0f2742', 600: '#13355c', 500: '#1c3a5c', 300: '#a9bcd4' },
        'map-red': { DEFAULT: '#c1272d', 600: '#a81f25' },
        paper: '#f4f5f7',
        muted: '#8a93a3',
        line: '#e3e6ea'
      },
      fontFamily: {
        sans: ['"Inter"', 'system-ui', 'sans-serif'],
        arabic: ['"Noto Naskh Arabic"', 'serif']
      },
      borderRadius: { lg: '0.75rem' },
      boxShadow: { soft: '0 1px 4px rgba(20,40,80,.08)' }
    }
  },
  plugins: []
};
