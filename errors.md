PS C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing> npm run dev

> pipelane-marketing@0.0.1 dev
> astro dev

17:29:16 [types] Generated 2ms

 astro  v4.16.19 ready in 1396 ms

┃ Local    http://localhost:4321/
┃ Network  use --host to expose

17:29:16 watching for file changes...
17:29:16 [ERROR] [vite] Error:   Failed to scan for dependencies from entries:
  C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/ConsentManager.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/CTA.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/DemoForm.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/FAQItem.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/FeatureCard.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/Footer.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/Icon.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/IntegrationCard.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/Navbar.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/NeonBadge.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/PricingCard.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/Section.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/StatCard.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/ThemeToggle.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/TimelineStep.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/layouts/Base.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/layouts/PostLayout.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/changelog.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/index.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/mentions-legales.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/prix.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/prospection-ia.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/relance-intelligente.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/securite-rgpd.astro
C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/pages/blog/index.astro

  X [ERROR] Expected ";" but found ":"

    script:C:/Users/moham/Documents/DATA/2025/Pipelane/pipelane-marketing/src/components/ConsentManager.astro?id=0:19:26:
      19 │         script.src = https://www.googletagmanager.com/gtag/js?id=;
         │                           ^
         ╵                           ;


    at failureErrorWithLog (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:1472:15)
    at C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:945:25
    at runOnEndCallbacks (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:1315:45)
    at buildResponseToResult (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:943:7)
    at C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:955:9
    at new Promise (<anonymous>)
    at requestCallbacks.on-end (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:954:54)        
    at handleRequest (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:647:17)
    at handleIncomingPacket (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:672:7)
    at Socket.readFromStdout (C:\Users\moham\Documents\DATA\2025\Pipelane\pipelane-marketing\node_modules\esbuild\lib\main.js:600:7)