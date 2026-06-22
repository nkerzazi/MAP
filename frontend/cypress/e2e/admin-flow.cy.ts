describe('Parcours admin', () => {
  it('se connecte et voit les statistiques', () => {
    cy.intercept('POST', '/api/v1/auth/login', {
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'admin@map.ma', displayName: 'Admin', roles: ['Admin']
    });
    cy.intercept('GET', '/api/v1/stats', {
      totalVideos: 248, videosByStatus: {}, totalUsers: 12, totalViews: 12480, totalWatchSeconds: 0, topVideos: []
    });

    cy.visit('/auth/login');
    cy.get('input[name=email]').type('admin@map.ma');
    cy.get('input[name=password]').type('p');
    cy.contains('Se connecter').click();
    cy.visit('/admin');
    cy.contains('Tableau de bord');
    cy.contains('248');
  });
});
