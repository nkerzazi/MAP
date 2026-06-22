import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminConfigComponent } from './admin-config.component';

describe('AdminConfigComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminConfigComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge la configuration et enregistre une clé', () => {
    const fixture = TestBed.createComponent(AdminConfigComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/config').flush({ siteName: 'MAP' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('siteName');

    fixture.componentInstance.save('siteName', 'MAP TV');
    const put = http.expectOne('/api/v1/config');
    expect(put.request.body).toEqual({ key: 'siteName', value: 'MAP TV' });
    put.flush(null);
  });
});
