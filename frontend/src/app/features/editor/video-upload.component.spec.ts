import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { VideoUploadComponent } from './video-upload.component';
import { EditorVideoService } from '../../core/api/editor-video.service';

describe('VideoUploadComponent', () => {
  const api = {
    create: jest.fn(() => of({ id: 'v1' })),
    uploadChunk: jest.fn(() => of({ received: 1 })),
    complete: jest.fn(() => of({ id: 'v1', status: 'Processing' }))
  };
  beforeEach(async () => {
    Object.values(api).forEach((m: jest.Mock) => m.mockClear());
    await TestBed.configureTestingModule({
      imports: [VideoUploadComponent],
      providers: [provideRouter([]), { provide: EditorVideoService, useValue: api }]
    }).compileComponents();
  });

  it('crée, téléverse les chunks puis finalise, et redirige', async () => {
    const nav = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(VideoUploadComponent);
    const cmp = fixture.componentInstance;
    cmp.title = 'Mon sujet';
    // fichier ~12 Mo → 3 chunks de 5 Mo
    cmp.file = new File([new Uint8Array(12 * 1024 * 1024)], 'v.mp4', { type: 'video/mp4' });
    await cmp.submit();
    expect(api.create).toHaveBeenCalledWith({ title: 'Mon sujet', description: undefined, categoryId: undefined });
    expect(api.uploadChunk).toHaveBeenCalledTimes(3);
    expect(api.complete).toHaveBeenCalledWith('v1', 3);
    expect(nav).toHaveBeenCalledWith(['/studio/videos', 'v1', 'edit']);
  });

  it('le label « Choisir » est associé à l’input fichier (ouvre l’explorateur nativement)', () => {
    const fixture = TestBed.createComponent(VideoUploadComponent);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=file]');
    const label = Array.from(fixture.nativeElement.querySelectorAll('label'))
      .find((el) => (el as HTMLElement).textContent?.includes('Choisir')) as HTMLLabelElement;
    expect(input.id).toBeTruthy();
    expect(label.getAttribute('for')).toBe(input.id);
  });
});
