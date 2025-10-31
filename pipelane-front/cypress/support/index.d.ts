/// <reference types="cypress" />

declare namespace Cypress {
  interface Chainable {
    visitApp(path: string, options?: Partial<VisitOptions>): Chainable<Window>;
  }
}
