const defaultTheme = require("tailwindcss/defaultTheme");
const plugin = require("tailwindcss/plugin");

module.exports = {
  darkMode: ["class"],
  content: ["./src/**/*.{astro,html,js,jsx,md,mdx,svelte,ts,tsx,vue}", "./public/**/*.svg"],
  theme: {
    container: {
      center: true,
      padding: {
        DEFAULT: "1.5rem",
        lg: "2.5rem"
      }
    },
    extend: {
      fontFamily: {
        sans: ["'Inter'", ...defaultTheme.fontFamily.sans]
      },
      colors: {
        base: "#0b0f17",
        surface: "rgba(255,255,255,0.06)",
        line: "rgba(255,255,255,0.08)",
        primary: "#75F0FF",
        secondary: "#9B8CFF",
        accent: "#60F7A3"
      },
      backgroundImage: {
        "hero-gradient": "radial-gradient(circle at top left, rgba(117,240,255,0.25), transparent 55%), radial-gradient(circle at bottom right, rgba(155,140,255,0.3), transparent 60%)",
        "brand-gradient": "linear-gradient(135deg, #75F0FF 0%, #9B8CFF 45%, #60F7A3 100%)"
      },
      boxShadow: {
        glow: "0 0 30px rgba(117,240,255,0.35)",
        soft: "0 20px 45px rgba(0,0,0,0.45)"
      },
      blur: {
        12: "12px"
      }
    }
  },
  plugins: [
    plugin(function ({ addUtilities, addComponents, theme }) {
      addUtilities({
        ".glass": {
          backgroundColor: "rgba(255,255,255,0.07)",
          backdropFilter: "blur(12px)",
          border: `1px solid ${theme("colors.line")}`,
          boxShadow: theme("boxShadow.soft")
        },
        ".neon": {
          boxShadow: `0 0 12px ${theme("colors.primary")}`,
          border: `1px solid ${theme("colors.primary")}`
        },
        ".chip": {
          display: "inline-flex",
          alignItems: "center",
          gap: "0.375rem",
          borderRadius: "9999px",
          padding: "0.35rem 0.75rem",
          border: `1px solid ${theme("colors.line")}`,
          backgroundColor: "rgba(255,255,255,0.06)"
        }
      });

      addComponents({
        ".btn-primary": {
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.5rem",
          padding: "0.75rem 1.5rem",
          borderRadius: "9999px",
          fontWeight: "600",
          backgroundImage: theme("backgroundImage.brand-gradient"),
          color: "#020205",
          transition: "transform 150ms ease, box-shadow 150ms ease",
          boxShadow: `0 10px 30px rgba(117,240,255,0.35)`
        },
        ".btn-primary:hover": {
          transform: "translateY(-1px)",
          boxShadow: `0 12px 40px rgba(117,240,255,0.45)`
        },
        ".btn-ghost": {
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.5rem",
          padding: "0.75rem 1.5rem",
          borderRadius: "9999px",
          fontWeight: "600",
          border: `1px solid ${theme("colors.line")}`,
          backgroundColor: "rgba(255,255,255,0.04)",
          color: "#f7f9ff",
          transition: "border-color 150ms ease, background-color 150ms ease"
        },
        ".btn-ghost:hover": {
          borderColor: theme("colors.primary"),
          backgroundColor: "rgba(117,240,255,0.1)"
        }
      });
    })
  ]
};
