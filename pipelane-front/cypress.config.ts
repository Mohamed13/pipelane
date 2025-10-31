import { defineConfig } from 'cypress';

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    setupNodeEvents(on) {
      on('task', {
        log(message: unknown) {
          console.log(message);
          return null;
        },
      });
    },
  },
});
