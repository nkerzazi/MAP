import { Component, Input, computed, signal } from '@angular/core';

const LABELS: Record<string, { fr: string; cls: string }> = {
  Draft: { fr: 'Brouillon', cls: 'bg-line text-ink' },
  Processing: { fr: 'En traitement', cls: 'bg-amber-100 text-amber-800' },
  Ready: { fr: 'Prête', cls: 'bg-sky-100 text-sky-800' },
  Published: { fr: 'Publiée', cls: 'bg-emerald-100 text-emerald-800' },
  Archived: { fr: 'Archivée', cls: 'bg-line text-muted' },
  Failed: { fr: 'Échec', cls: 'bg-red-100 text-map-red' }
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="inline-block px-2 py-0.5 rounded-full text-xs font-semibold" [class]="view().cls">{{ view().fr }}</span>`
})
export class StatusBadgeComponent {
  private _status = signal('Draft');
  @Input({ required: true }) set status(v: string) { this._status.set(v); }
  view = computed(() => LABELS[this._status()] ?? { fr: this._status(), cls: 'bg-line text-ink' });
}
