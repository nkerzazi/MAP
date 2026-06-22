import { TestBed } from '@angular/core/testing';
import { TranslationService } from './translation.service';

describe('TranslationService', () => {
  let svc: TranslationService;
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.dir = 'ltr';
    TestBed.configureTestingModule({ providers: [TranslationService] });
    svc = TestBed.inject(TranslationService);
  });

  it('traduit en français par défaut', () => {
    expect(svc.lang()).toBe('fr');
    expect(svc.t('nav.login')).toBe('Connexion');
    expect(svc.dir()).toBe('ltr');
  });

  it('bascule en arabe : traduction, direction RTL et persistance', () => {
    svc.setLang('ar');
    expect(svc.t('nav.login')).toBe('تسجيل الدخول');
    expect(svc.dir()).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('map_lang')).toBe('ar');
  });

  it('toggle alterne fr/ar', () => {
    svc.toggle();
    expect(svc.lang()).toBe('ar');
    svc.toggle();
    expect(svc.lang()).toBe('fr');
  });

  it('renvoie la clé si traduction absente', () => {
    expect(svc.t('clé.inconnue')).toBe('clé.inconnue');
  });
});
