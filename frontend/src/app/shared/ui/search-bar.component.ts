import { Component, EventEmitter, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form (ngSubmit)="submit()" class="flex items-center gap-2">
      <input [(ngModel)]="value" name="q" type="search"
             placeholder="Rechercher une vidéo…"
             class="flex-1 px-3 py-2 rounded-lg border border-line bg-white focus:outline-none focus:ring-2 focus:ring-ink/30" />
      <button type="submit" class="px-3 py-2 rounded-lg bg-ink text-white text-sm font-semibold">Rechercher</button>
    </form>
  `
})
export class SearchBarComponent {
  value = '';
  @Output() search = new EventEmitter<string>();
  submit() { this.search.emit(this.value.trim()); }
}
