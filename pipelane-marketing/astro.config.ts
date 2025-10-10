import { defineConfig } from "astro/config";
import tailwind from "@astrojs/tailwind";
import icon from "@astrojs/icon";

export default defineConfig({
  site: "https://www.pipelane.app",
  output: "hybrid",
  integrations: [
    tailwind({
      applyBaseStyles: false
    }),
    icon()
  ]
});
