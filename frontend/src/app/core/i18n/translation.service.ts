import { Injectable, computed, signal } from '@angular/core';
import { Lang, TRANSLATIONS } from './translations';

const LANG_KEY = 'map_lang';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private _lang = signal<Lang>(this.initial());
  lang = this._lang.asReadonly();
  dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));

  constructor() { this.apply(this._lang()); }

  t(key: string): string {
    return TRANSLATIONS[this._lang()][key] ?? key;
  }
  setLang(lang: Lang): void {
    this._lang.set(lang);
    localStorage.setItem(LANG_KEY, lang);
    this.apply(lang);
  }
  toggle(): void {
    this.setLang(this._lang() === 'fr' ? 'ar' : 'fr');
  }

  private initial(): Lang {
    return localStorage.getItem(LANG_KEY) === 'ar' ? 'ar' : 'fr';
  }
  private apply(lang: Lang): void {
    if (typeof document !== 'undefined') {
      document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
      document.documentElement.lang = lang;
    }
  }
}
