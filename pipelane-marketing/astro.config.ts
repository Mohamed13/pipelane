import { defineConfig } from "astro/config";
import tailwind from "@astrojs/tailwind";

export default defineConfig({
  site: "https://www.pipelane.app",
  output: "static",
  integrations: [
    tailwind({
      applyBaseStyles: false
    })
  ]
});
