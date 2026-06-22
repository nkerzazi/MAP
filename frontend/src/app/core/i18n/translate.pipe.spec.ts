import { TestBed } from '@angular/core/testing';
import { TranslatePipe } from './translate.pipe';
import { TranslationService } from './translation.service';

describe('TranslatePipe', () => {
  let pipe: TranslatePipe;
  let svc: TranslationService;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [TranslationService] });
    svc = TestBed.inject(TranslationService);
    pipe = TestBed.runInInjectionContext(() => new TranslatePipe());
  });

  it('traduit la clé selon la langue active', () => {
    expect(pipe.transform('nav.login')).toBe('Connexion');
    svc.setLang('ar');
    expect(pipe.transform('nav.login')).toBe('تسجيل الدخول');
  });
});
