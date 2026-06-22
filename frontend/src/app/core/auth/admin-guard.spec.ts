import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { adminGuard } from './auth.guard';
import { AuthStore } from './auth.store';

describe('adminGuard', () => {
  let store: AuthStore;
  const router = { navigate: jest.fn() };
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthStore, provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    });
    store = TestBed.inject(AuthStore);
    router.navigate.mockClear();
  });

  it('autorise un Admin', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Admin'] });
    expect(TestBed.runInInjectionContext(() => adminGuard(null as never, null as never))).toBe(true);
  });

  it('refuse un Editeur', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Editeur'] });
    expect(TestBed.runInInjectionContext(() => adminGuard(null as never, null as never))).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
