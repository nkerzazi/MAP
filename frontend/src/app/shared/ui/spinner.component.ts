import { Component } from '@angular/core';

@Component({
  selector: 'app-spinner',
  standalone: true,
  template: `<div class="inline-block w-6 h-6 border-2 border-line border-t-ink rounded-full animate-spin" role="status" aria-label="Chargement"></div>`
})
export class SpinnerComponent {}
