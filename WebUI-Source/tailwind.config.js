/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{js,jsx,ts,tsx}',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
      colors: {
        brand: '#4A6FFF',
      },
      borderRadius: {
        '4xl': '2rem',
        '5xl': '2.5rem',
      },
      keyframes: {
        'scan-line': {
          '0%, 100%': { transform: 'translateY(-120px)', opacity: '0.4' },
          '50%':       { transform: 'translateY(120px)',  opacity: '1'   },
        },
        'fade-in-up': {
          '0%':   { opacity: '0', transform: 'translateY(16px)' },
          '100%': { opacity: '1', transform: 'translateY(0)'    },
        },
        'corner-pulse': {
          '0%, 100%': { opacity: '1'   },
          '50%':      { opacity: '0.4' },
        },
      },
      animation: {
        'scan-line':    'scan-line 2.2s ease-in-out infinite',
        'fade-in-up':   'fade-in-up 0.35s ease-out forwards',
        'corner-pulse': 'corner-pulse 1.8s ease-in-out infinite',
      },
    },
  },
  plugins: [],
}
