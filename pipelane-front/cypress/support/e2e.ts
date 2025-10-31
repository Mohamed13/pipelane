// Cypress support file – intentionally minimal for this project.
declare global {
  namespace Cypress {
    interface Chainable {
      visitApp(path: string, options?: Partial<Cypress.VisitOptions>): Chainable<Window>;
    }
  }
}

const DEMO_TOKEN =
  'eyJhbGciOiJub25lIn0.eyJ0aWQiOiJ0ZW5hbnQtMTIzIiwiZW1haWwiOiJkZW1vQHBpcGVsYW5lLmFwcCJ9.signature';

Cypress.Commands.add('visitApp', (path: string, options?: Partial<Cypress.VisitOptions>) => {
  const userBeforeLoad = options?.onBeforeLoad;
  const visitOptions: Partial<Cypress.VisitOptions> = {
    failOnStatusCode: false,
    ...options,
    onBeforeLoad: (win) => {
      try {
        win.localStorage.setItem('pl_token', DEMO_TOKEN);
      } catch {
        /* ignore storage issues */
      }
      userBeforeLoad?.(win);
    },
  };

  cy.visit(path, visitOptions);
});

export {};
