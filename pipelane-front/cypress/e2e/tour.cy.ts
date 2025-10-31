const interceptCommon = () => {
  cy.intercept('GET', '**/assets/i18n/**').as('translations');
  cy.intercept('GET', '**/analytics/delivery**', { fixture: 'analytics.json' }).as('analytics');
  cy.intercept('POST', '**/api/followups/preview', { statusCode: 200, body: { count: 42 } }).as('preview');
  cy.intercept('GET', '**/templates', { fixture: 'templates.json' }).as('templates');
};

describe('Guided tour', () => {
  beforeEach(() => {
    cy.clearLocalStorage('pipelane_tour_done');
    interceptCommon();
  });

  it('shows shepherd tour on first visit', () => {
    cy.visitApp('/');
    cy.wait('@translations');
    cy.wait('@analytics');
    cy.wait('@templates');
    cy.wait('@preview');

    cy.get('.shepherd-element', { timeout: 6000 }).should('contain.text', 'Connect your channels');
    cy.contains('.shepherd-element button', 'Skip tour').click();
    cy.get('.shepherd-element').should('not.exist');
    cy.window().then((win) => {
      expect(win.localStorage.getItem('pipelane_tour_done')).to.eq('true');
    });
  });
});
