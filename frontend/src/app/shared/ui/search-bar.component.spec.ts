import { TestBed } from '@angular/core/testing';
import { SearchBarComponent } from './search-bar.component';

describe('SearchBarComponent', () => {
  it('émet le terme recherché à la soumission', async () => {
    await TestBed.configureTestingModule({ imports: [SearchBarComponent] }).compileComponents();
    const fixture = TestBed.createComponent(SearchBarComponent);
    const cmp = fixture.componentInstance;
    let emitted = '';
    cmp.search.subscribe((v: string) => (emitted = v));
    fixture.detectChanges();
    cmp.value = 'sommet';
    cmp.submit();
    expect(emitted).toBe('sommet');
  });
});
