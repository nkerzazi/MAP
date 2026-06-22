describe('Bascule de langue', () => {
  it('passe l’interface en arabe et en RTL', () => {
    cy.intercept('GET', '/api/v1/categories', []);
    cy.intercept('GET', '/api/v1/videos*', { items: [], total: 0, page: 1, pageSize: 20 });
    cy.visit('/');
    cy.contains('Connexion');
    cy.get('header button').first().click(); // bascule FR/AR
    cy.contains('تسجيل الدخول');
    cy.get('html').should('have.attr', 'dir', 'rtl');
  });
});
