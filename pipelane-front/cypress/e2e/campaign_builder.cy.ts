const interceptCommon = () => {
  cy.intercept('GET', '**/assets/i18n/**').as('translations');
  cy.intercept('GET', '**/templates', { fixture: 'templates.json' }).as('templates');
  cy.intercept('POST', '**/api/followups/preview', { statusCode: 200, body: { count: 128 } }).as('preview');
};

describe('Campaign builder wizard', () => {
  beforeEach(() => {
    interceptCommon();
    cy.visitApp('/campaigns');
    cy.wait('@translations');
    cy.wait('@templates');
  });

  it('walks through the guided builder to the review step', () => {
    cy.contains('.panel', 'Segment builder').within(() => {
      cy.contains('VIP').click();
      cy.contains('Trial').click();
      cy.contains('WhatsApp').click();
      cy.contains('Email').click();
      cy.contains('Respect consent-only contacts').click();
    });

    cy.contains('Next · Message').click();

    cy.contains('.panel', 'Message design')
      .should('exist')
      .within(() => {
        cy.get('input[formcontrolname="name"]').clear().type('Q4 Launch');
      });

    cy.get('mat-select[formcontrolname="primaryChannel"]').click();
    cy.get('body')
      .find('mat-option')
      .contains('Email')
      .click({ force: true });

    cy.get('mat-select[formcontrolname="fallbackOrder"]').click();
    cy.get('body')
      .find('mat-option')
      .contains('SMS')
      .click({ force: true });
    cy.get('body').click();

    cy.get('mat-select[formcontrolname="templateId"]').click();
    cy.get('body')
      .find('mat-option')
      .contains('welcome_series')
      .click({ force: true });

    cy.contains('Next · Schedule').click();

    cy.wait('@preview');

    cy.contains('.panel', 'Scheduling').within(() => {
      cy.contains('Throttle delivery rate').click();
      cy.get('input[formcontrolname="throttleRate"]').clear().type('150');
    });

    cy.contains('Review & launch').click();

    cy.contains('Launch campaign')
      .scrollIntoView()
      .should('be.visible')
      .should('not.be.disabled');
  });
});
