const interceptCommon = () => {
  cy.intercept('GET', '**/assets/i18n/**', { statusCode: 200, body: {} });
  cy.intercept('GET', '**/templates', { fixture: 'templates.json' }).as('templates');
  cy.intercept('POST', '**/api/followups/preview', { statusCode: 200, body: { count: 128 } }).as('preview');
};

describe('Campaign builder wizard', () => {
  beforeEach(() => {
    interceptCommon();
    cy.visit('/campaigns');
    cy.wait('@templates');
  });

  it('walks through the guided builder and submits a campaign', () => {
    cy.contains('.panel', 'Segment builder').within(() => {
      cy.contains('VIP').click();
      cy.contains('Trial').click();
      cy.contains('WhatsApp').click();
      cy.contains('Email').click();
      cy.contains('Respect consent-only contacts').click();
    });

    cy.contains('Next · Message').click();

    cy.contains('.panel', 'Message design').within(() => {
      cy.get('input[formcontrolname="name"]').clear().type('Q4 Launch');
      cy.get('mat-select[formcontrolname="primaryChannel"]').click();
      cy.get('mat-option').contains('Email').click();

      cy.get('mat-select[formcontrolname="fallbackOrder"]').click();
      cy.get('mat-option').contains('SMS').click();
      cy.get('body').click(); // close panel

      cy.get('mat-select[formcontrolname="templateId"]').click();
      cy.get('mat-option').contains('welcome_series').click();
    });

    cy.contains('Next · Schedule').click();

    cy.contains('.panel', 'Scheduling').within(() => {
      cy.get('input[formcontrolname="scheduledDate"]').type('2025-12-24');
      cy.get('input[formcontrolname="scheduledTime"]').clear().type('10:30');
      cy.contains('Throttle delivery rate').click();
      cy.get('input[formcontrolname="throttleRate"]').clear().type('150');
    });

    cy.contains('Review & launch').click();

    cy.intercept('POST', '**/campaigns', { statusCode: 200, body: { id: 'cmp-001' } }).as('createCampaign');

    cy.contains('Launch campaign').click();
    cy.wait('@createCampaign')
      .its('request.body')
      .should('deep.include', {
        name: 'Q4 Launch',
        primaryChannel: 'email',
        batchSize: null,
      });
  });
});

