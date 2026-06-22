describe('Parcours éditeur', () => {
  it('se connecte et voit son tableau de bord', () => {
    cy.intercept('POST', '/api/v1/auth/login', {
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'ed@map.ma', displayName: 'Éditeur', roles: ['Editeur']
    });
    cy.intercept('GET', '/api/v1/videos/mine*', { items: [], total: 0, page: 1, pageSize: 20 });

    cy.visit('/auth/login');
    cy.get('input[name=email]').type('ed@map.ma');
    cy.get('input[name=password]').type('p');
    cy.contains('Se connecter').click();
    cy.url().should('include', '/studio');
    cy.contains('Mes vidéos');
  });
});
