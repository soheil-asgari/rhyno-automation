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
        rhyno: {
          bg: '#f3f5f7',
          surface: '#ffffff',
          line: '#d9e1e8',
          ink: '#111820',
          muted: '#5d6976',
          primary: '#176c74',
          accent: '#9d6418'
        }
      },
      borderRadius: {
        rhyno: '8px'
      },
      boxShadow: {
        rhyno: '0 14px 38px rgba(15, 23, 42, .08)',
        'rhyno-sm': '0 5px 16px rgba(15, 23, 42, .055)'
      }
    }
  },
  plugins: []
};
