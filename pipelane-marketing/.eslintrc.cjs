/** @type {import("eslint").Linter.Config} */
module.exports = {
  root: true,
  parser: "@typescript-eslint/parser",
  parserOptions: {
    sourceType: "module",
    ecmaVersion: "latest"
  },
  env: {
    browser: true,
    es2022: true
  },
  plugins: ["@typescript-eslint", "tailwindcss"],
  extends: [
    "eslint:recommended",
    "plugin:@typescript-eslint/recommended",
    "plugin:astro/recommended",
    "plugin:astro/jsx-a11y-recommended",
    "plugin:tailwindcss/recommended",
    "prettier"
  ],
  settings: {
    tailwindcss: {
      config: "tailwind.config.cjs"
    }
  },
  overrides: [
    {
      files: ["*.astro"],
      parser: "astro-eslint-parser",
      parserOptions: {
        parser: "@typescript-eslint/parser",
        extraFileExtensions: [".astro"],
        sourceType: "module"
      }
    },
    {
      files: ["src/pages/api/**/*.ts"],
      env: {
        node: true
      }
    }
  ],
  rules: {
    "@typescript-eslint/no-unused-vars": [
      "warn",
      {
        argsIgnorePattern: "^_",
        varsIgnorePattern: "^_"
      }
    ],
    "tailwindcss/no-custom-classname": "off"
  }
};
