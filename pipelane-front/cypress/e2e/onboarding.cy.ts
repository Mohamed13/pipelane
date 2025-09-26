describe('Onboarding flow', () => {
  it('loads and saves WA creds', () => {
    // seed token
    cy.window().then(w => w.localStorage.setItem('pl_token', 'header.payload.sig'));
    cy.visit('/onboarding');
    cy.contains('Onboarding');
    cy.intercept('POST', '**/onboarding/channel-settings', { statusCode: 200 }).as('save');
    cy.get('input[placeholder="PhoneNumberId"]').type('123');
    cy.get('input[placeholder="AccessToken"]').type('token');
    cy.contains('Save').first().click();
    cy.wait('@save');
  });
});
