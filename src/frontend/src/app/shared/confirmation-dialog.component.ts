import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';

let nextDialogId = 0;

@Component({
  selector: 'app-confirmation-dialog',
  template: `
    <dialog #dialog (cancel)="onCancel($event)" [attr.aria-labelledby]="titleId">
      <h2 [id]="titleId">{{ title }}</h2>
      <p>{{ message }}</p>
      <div class="actions">
        <button type="button" (click)="respond(false)" autofocus>{{ cancelLabel }}</button>
        <button type="button" (click)="respond(true)">{{ confirmLabel }}</button>
      </div>
    </dialog>
  `,
})
export class ConfirmationDialogComponent {
  readonly titleId = `confirmation-title-${++nextDialogId}`;
  @Input() title = 'Confirmar ação';
  @Input() message = 'Deseja continuar?';
  @Input() confirmLabel = 'Confirmar';
  @Input() cancelLabel = 'Voltar';
  @Output() readonly answered = new EventEmitter<boolean>();
  @ViewChild('dialog', { static: true }) private readonly dialog?: ElementRef<HTMLDialogElement>;
  private returnFocus?: HTMLElement;

  present(): void {
    const element = this.dialog?.nativeElement;
    if (!element || element.open) return;
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
    element.showModal();
  }

  respond(answer: boolean): void {
    const element = this.dialog?.nativeElement;
    if (element?.open) element.close();
    this.answered.emit(answer);
    this.returnFocus?.focus();
    this.returnFocus = undefined;
  }

  onCancel(event: Event): void {
    event.preventDefault();
    this.respond(false);
  }
}
