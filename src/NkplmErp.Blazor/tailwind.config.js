/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './**/*.razor',
        './**/*.cshtml',
        './**/*.cs',
        './Pages/**/*.{razor,cshtml}',
        './Shared/**/*.{razor,cshtml}',
        './Components/**/*.{razor,cshtml}'
    ],
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                'glass-bg': 'rgba(255, 255, 255, 0.05)',
                'glass-border': 'rgba(255, 255, 255, 0.1)',
                'primary-glow': '#38bdf8',
                'bg-dark': '#0f172a',
                'text-muted': '#94a3b8',
                'primary': '#00396A', // 👈 your color
                'navy': '#002d54',     // text-navy (date picker / dashboards)
                'teal': '#0d9488',     // text-teal accent
            },
            backdropBlur: {
                glass: '12px',
            },
            fontFamily: {
                sans: ['Poppins', 'Inter', 'system-ui', '-apple-system', 'sans-serif'],
            },
            fontSize: {
                11: '11px', // 👈 optional shortcut (text-11)
            },
        },
    },
    plugins: [],
};
