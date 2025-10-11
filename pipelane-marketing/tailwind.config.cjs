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
        bg: "var(--bg)",
        surface: "var(--surface)",
        surfaceStrong: "var(--surface-strong)",
        surfaceOverlay: "var(--surface-overlay)",
        text: "var(--text)",
        textMuted: "var(--text-muted)",
        primary: "var(--primary)",
        primaryFg: "var(--primary-foreground)",
        secondary: "var(--secondary)",
        accent: "var(--accent)",
        line: "var(--line)",
        onSurface: "var(--on-surface)",
        onSurfaceStrong: "var(--on-surface-strong)",
        badgeBg: "var(--badge-bg)",
        badgeText: "var(--badge-text)"
      },
      backgroundImage: {
        "hero-gradient": "radial-gradient(circle at top left, rgba(125,249,255,0.28), transparent 55%), radial-gradient(circle at bottom right, rgba(181,168,255,0.32), transparent 60%)",
        "brand-gradient": "linear-gradient(135deg, #7DF9FF 0%, #B5A8FF 45%, #63FBA2 100%)"
      },
      boxShadow: {
        glow: "0 0 24px rgba(125,249,255,0.45)",
        soft: "0 24px 48px rgba(4,8,21,0.5)",
        raised: "0 32px 64px rgba(4, 12, 32, 0.55)"
      },
      borderRadius: {
        xl2: "1.75rem"
      },
      transitionDuration: {
        350: "350ms"
      }
    }
  },
  plugins: [
    plugin(function ({ addUtilities, addComponents, theme }) {
      addUtilities({
        ".glass": {
          backgroundColor: "var(--surface)",
          backdropFilter: "blur(22px)",
          border: `1px solid ${theme("colors.line")}`,
          boxShadow: theme("boxShadow.soft")
        },
        ".glass-strong": {
          backgroundColor: "var(--surface-strong)",
          backdropFilter: "blur(28px)",
          border: `1px solid ${theme("colors.line")}`,
          boxShadow: theme("boxShadow.raised")
        },
        ".on-surface": {
          color: theme("colors.onSurface")
        },
        ".on-surface-strong": {
          color: theme("colors.onSurfaceStrong")
        },
        ".text-elevated": {
          color: theme("colors.text")
        },
        ".scrim": {
          position: "relative",
          isolation: "isolate"
        },
        ".scrim::before": {
          content: '""',
          position: "absolute",
          inset: "0",
          background: "var(--scrim)",
          zIndex: "0",
          pointerEvents: "none"
        }
      });

      addComponents({
        ".btn-primary": {
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.5rem",
          padding: "0.75rem 1.75rem",
          borderRadius: "9999px",
          fontWeight: "600",
          backgroundColor: theme("colors.primary"),
          color: theme("colors.primaryFg"),
          transition: "transform 150ms ease, box-shadow 150ms ease, filter 150ms ease",
          boxShadow: "0 18px 36px rgba(125,249,255,0.35)"
        },
        ".btn-primary:hover": {
          transform: "translateY(-2px)",
          filter: "brightness(1.05)",
          boxShadow: "0 22px 44px rgba(125,249,255,0.45)"
        },
        ".btn-primary:focus-visible": {
          outline: `2px solid ${theme("colors.primary")}`,
          outlineOffset: "3px"
        },
        ".btn-ghost": {
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.5rem",
          padding: "0.75rem 1.75rem",
          borderRadius: "9999px",
          fontWeight: "600",
          border: `1px solid ${theme("colors.line")}`,
          backgroundColor: "var(--surface-overlay)",
          color: theme("colors.onSurfaceStrong"),
          transition: "border-color 150ms ease, background-color 150ms ease"
        },
        ".btn-ghost:hover": {
          borderColor: theme("colors.primary"),
          backgroundColor: "rgba(125,249,255,0.18)"
        },
        ".chip": {
          display: "inline-flex",
          alignItems: "center",
          gap: "0.375rem",
          borderRadius: "9999px",
          padding: "0.35rem 0.75rem",
          border: `1px solid ${theme("colors.line")}`,
          backgroundColor: theme("colors.badgeBg"),
          color: theme("colors.badgeText")
        }
      });
    })
  ]
};
