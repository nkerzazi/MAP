export type Lang = 'fr' | 'ar';

export const TRANSLATIONS: Record<Lang, Record<string, string>> = {
  fr: {
    'brand.video': 'MAP Vidéo',
    'nav.login': 'Connexion',
    'nav.logout': 'Déconnexion',
    'nav.studio': 'Studio',
    'nav.admin': 'Admin',
    'footer.tagline': 'Maghreb Arabe Presse — plateforme vidéo interne',
    'catalog.title': 'Catalogue',
    'catalog.search': 'Rechercher une vidéo…',
    'catalog.searchBtn': 'Rechercher',
    'catalog.all': 'Toutes',
    'catalog.empty': 'Aucune vidéo trouvée.'
  },
  ar: {
    'brand.video': 'فيديو وم ع',
    'nav.login': 'تسجيل الدخول',
    'nav.logout': 'تسجيل الخروج',
    'nav.studio': 'الاستوديو',
    'nav.admin': 'الإدارة',
    'footer.tagline': 'وكالة المغرب العربي للأنباء — منصة الفيديو الداخلية',
    'catalog.title': 'الفهرس',
    'catalog.search': 'ابحث عن فيديو…',
    'catalog.searchBtn': 'بحث',
    'catalog.all': 'الكل',
    'catalog.empty': 'لا توجد مقاطع فيديو.'
  }
};
