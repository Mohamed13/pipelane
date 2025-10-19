describe('Onboarding channel setup', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/assets/i18n/**', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/onboarding/channel-settings', { statusCode: 200 }).as('saveSettings');
    cy.intercept('POST', '**/messages/send', { statusCode: 200 }).as('sendMessage');
    cy.visit('/onboarding');
  });

  it('saves channel credentials and sends a test email', () => {
    cy.contains('.channel-card', 'WhatsApp Cloud').within(() => {
      cy.get('input[formcontrolname="phone_number_id"]').type('123456789');
      cy.get('input[formcontrolname="access_token"]').type('token-secret');
      cy.get('input[formcontrolname="verify_token"]').type('verify-me');
      cy.contains('button', 'Save credentials').click();
    });
    cy.wait('@saveSettings');

    cy.contains('.channel-card', 'Email ESP').within(() => {
      cy.get('input[formcontrolname="apiKey"]').type('resend_api_key');
      cy.get('input[formcontrolname="domain"]').type('ops@pipelane.dev');
      cy.contains('button', 'Save credentials').click();
    });
    cy.wait('@saveSettings');

    cy.contains('.channel-card', 'Email ESP')
      .find('.test-form')
      .within(() => {
        cy.get('input[formcontrolname="contactId"]').type('contact-1');
        cy.get('textarea[formcontrolname="message"]').clear().type('Test delivery from Cypress');
        cy.contains('button', 'Send test email').click();
      });
    cy.wait('@sendMessage');
  });
});
