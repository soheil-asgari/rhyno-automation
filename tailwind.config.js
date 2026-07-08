/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './Views/**/*.cshtml',
    './Pages/**/*.cshtml',
    './Areas/**/*.cshtml'
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Vazirmatn', 'Segoe UI', 'Tahoma', 'sans-serif']
      },
      colors: {
        // Redefine slate with clean neutral Zinc tones for a premium aesthetic
        slate: {
          50: '#fafafa',
          100: '#f4f4f5',
          200: '#e4e4e7',
          300: '#d4d4d8',
          400: '#a1a1aa',
          500: '#71717a',
          600: '#52525b',
          700: '#3f3f46',
          800: '#27272a',
          900: '#18181b',
          950: '#09090b',
          955: '#09090b',
        },
        rhyno: {
          bg: '#fafafa',
          surface: '#ffffff',
          line: '#e4e4e7',
          ink: '#09090b',
          muted: '#71717a',
          primary: '#0d9488',
          accent: '#b45309'
        }
      },
      // Compress default spacing scale by ~30% for ultra-density
      spacing: {
        '0': '0px',
        '0.5': '0.08rem',
        '1': '0.18rem',
        '1.5': '0.25rem',
        '2': '0.35rem',
        '2.5': '0.45rem',
        '3': '0.52rem',
        '3.5': '0.62rem',
        '4': '0.7rem',
        '5': '0.88rem',
        '6': '1.05rem',
        '7': '1.2rem',
        '8': '1.4rem',
        '9': '1.6rem',
        '10': '1.75rem',
        '11': '1.9rem',
        '12': '2.1rem',
        '14': '2.45rem',
        '16': '2.8rem',
        '20': '3.5rem',
        '24': '4.2rem',
        '28': '4.9rem',
        '32': '5.6rem',
        '36': '6.3rem',
        '40': '7rem',
        '44': '7.7rem',
        '48': '8.4rem',
        '52': '9.1rem',
        '56': '9.8rem',
        '60': '10.5rem',
        '64': '11.2rem',
        '72': '12.6rem',
        '80': '14rem',
        '96': '16.8rem',
      },
      borderRadius: {
        rhyno: '5px',
        '3xl': '10px',
        '2xl': '8px',
        'xl': '6px',
        'lg': '5px',
        'md': '4px',
        'sm': '3px',
      },
      boxShadow: {
        rhyno: '0 4px 12px rgba(9, 9, 11, .03)',
        'rhyno-sm': '0 2px 6px rgba(9, 9, 11, .02)'
      }
    }
  },
  plugins: []
};

