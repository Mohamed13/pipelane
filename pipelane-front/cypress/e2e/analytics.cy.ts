describe('Analytics dashboard', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/assets/i18n/**').as('translations');
    cy.fixture('analytics.json').then((analytics) => {
      cy.intercept('GET', '**/analytics/delivery**', analytics).as('analyticsInitial');
    });
    cy.visitApp('/analytics');
    cy.wait('@translations');
    cy.wait('@analyticsInitial');
  });

  it('requests analytics again when switching date range', () => {
    const queries: Array<Record<string, string>> = [];
    cy.intercept('GET', '**/analytics/delivery**', (req) => {
      queries.push({ ...(req.query as Record<string, string>) });
      req.reply({ statusCode: 200, body: { totals: { queued: 0, sent: 5, delivered: 4, opened: 2, failed: 0, bounced: 1 }, byChannel: [], byTemplate: [] } });
    }).as('analyticsCall');

    cy.get('[data-cy="preset-7d"]', { timeout: 10000 }).should('be.visible');
    cy.get('[data-cy="preset-30d"]').should('not.be.disabled').click({ force: true });
    cy.wait('@analyticsCall');

    cy.wrap(null).then(() => {
      expect(queries.some((query) => !!query.from && !!query.to)).to.be.true;
    });

    cy.contains('Analytics dashboard').should('exist');
  });
});
