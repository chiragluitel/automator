/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Hanken Grotesk"', "system-ui", "sans-serif"],
        mono: ['"IBM Plex Mono"', "ui-monospace", "monospace"],
      },
      colors: {
        brand: {
          50: "#eefcf6",
          100: "#d6f6e8",
          200: "#b0ecd4",
          300: "#7cdcba",
          400: "#46c39c",
          500: "#22a884",
          600: "#13876b",
          700: "#106c58",
          800: "#115547",
          900: "#10463b",
        },
        ink: {
          50: "#f6f7f8",
          100: "#eceef1",
          200: "#d6dae0",
          400: "#9aa3af",
          600: "#525a66",
          800: "#262b33",
          900: "#171a1f",
        },
      },
      boxShadow: {
        card: "0 1px 2px rgba(16,24,40,0.04), 0 1px 3px rgba(16,24,40,0.06)",
      },
    },
  },
  plugins: [],
};
