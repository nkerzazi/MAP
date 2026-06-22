describe('Catalogue public', () => {
  it('affiche le catalogue et ouvre une vidéo', () => {
    cy.intercept('GET', '/api/v1/categories', [{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    cy.intercept('GET', '/api/v1/videos*', {
      items: [{ id: 'v1', title: 'Sommet économique', slug: 'sommet', categoryName: 'Actualités', durationSeconds: null, publishedAt: null, status: 'Published' }],
      total: 1, page: 1, pageSize: 20
    });
    cy.intercept('GET', '/api/v1/videos/v1', {
      id: 'v1', title: 'Sommet économique', description: 'D', slug: 'sommet', status: 'Published',
      categoryName: 'Actualités', durationSeconds: 120, publishedAt: null, tags: [], renditions: []
    });
    cy.intercept('GET', '/api/v1/videos/v1/stream', { id: 'v1', manifestUrl: '/x.m3u8', renditions: [] });

    cy.visit('/');
    cy.contains('Sommet économique').click();
    cy.url().should('include', '/videos/v1');
    cy.contains('h1', 'Sommet économique');
  });
});
