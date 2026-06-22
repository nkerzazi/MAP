import { TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  it('calcule le nombre de pages et émet la navigation', async () => {
    await TestBed.configureTestingModule({ imports: [PaginationComponent] }).compileComponents();
    const fixture = TestBed.createComponent(PaginationComponent);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('total', 45);
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('page', 1);
    fixture.detectChanges();
    expect(cmp.totalPages()).toBe(3);
    let target = 0;
    cmp.pageChange.subscribe((p: number) => (target = p));
    cmp.go(2);
    expect(target).toBe(2);
  });
});
