describe('Analytics dashboard', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/assets/i18n/**', { statusCode: 200, body: {} });
    cy.fixture('analytics.json').then((analytics) => {
      cy.intercept('GET', '**/analytics/delivery**', analytics);
    });
    cy.visit('/analytics');
  });

  it('requests analytics again when switching date range', () => {
    const queries: Array<Record<string, string>> = [];
    cy.intercept('GET', '**/analytics/delivery**', (req) => {
      queries.push({ ...(req.query as Record<string, string>) });
      req.reply({ statusCode: 200, body: { totals: { queued: 0, sent: 5, delivered: 4, opened: 2, failed: 0, bounced: 1 }, byChannel: [], byTemplate: [] } });
    }).as('analyticsCall');

    cy.contains('7 days');
    cy.contains('30 days').click();
    cy.wait('@analyticsCall');

    cy.wrap(null).then(() => {
      expect(queries.some((query) => !!query.from && !!query.to)).to.be.true;
    });

    cy.contains('Delivery timeline').should('be.visible');
  });
});
