import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

export interface ConfirmOptions {
  title?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

export interface PromptOptions extends ConfirmOptions {
  placeholder?: string;
  required?: boolean;
  initialValue?: string;
}

export interface ConfirmState {
  message: string;
  title: string;
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
  resolve: (result: boolean) => void;
}

export interface PromptState {
  message: string;
  title: string;
  placeholder: string;
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
  required: boolean;
  initialValue: string;
  resolve: (result: string | null) => void;
}

/**
 * App-wide replacement for native alert()/confirm()/prompt():
 * - success/error/info -> toasts (top-right stack, auto-dismiss; errors linger longer)
 * - confirm(message)   -> Promise<boolean> styled modal (like window.confirm)
 * - prompt(message)    -> Promise<string | null> styled modal with one input
 *   (resolves null on cancel, the entered string — possibly '' — on OK, matching window.prompt)
 * Rendered by NotificationHostComponent, mounted once in AppComponent so it works
 * inside MainLayout and on standalone pages (login/apply/status/print) alike.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextToastId = 1;

  toasts = signal<Toast[]>([]);
  confirmState = signal<ConfirmState | null>(null);
  promptState = signal<PromptState | null>(null);

  success(message: string): void { this.pushToast('success', message, 4000); }
  info(message: string): void { this.pushToast('info', message, 4000); }
  // Errors stick around longer so they aren't missed.
  error(message: string): void { this.pushToast('error', message, 8000); }

  dismissToast(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }

  confirm(message: string, options: ConfirmOptions = {}): Promise<boolean> {
    this.confirmState()?.resolve(false);  // a newer dialog supersedes an unresolved one
    return new Promise<boolean>(resolve => {
      this.confirmState.set({
        message,
        title: options.title ?? 'Please confirm',
        confirmLabel: options.confirmLabel ?? 'Confirm',
        cancelLabel: options.cancelLabel ?? 'Cancel',
        danger: options.danger ?? false,
        resolve
      });
    });
  }

  /** Called by the host component when the user acts on the confirm dialog. */
  resolveConfirm(result: boolean): void {
    const state = this.confirmState();
    this.confirmState.set(null);
    state?.resolve(result);
  }

  prompt(message: string, options: PromptOptions = {}): Promise<string | null> {
    this.promptState()?.resolve(null);
    return new Promise<string | null>(resolve => {
      this.promptState.set({
        message,
        title: options.title ?? 'Input required',
        placeholder: options.placeholder ?? '',
        confirmLabel: options.confirmLabel ?? 'OK',
        cancelLabel: options.cancelLabel ?? 'Cancel',
        danger: options.danger ?? false,
        required: options.required ?? false,
        initialValue: options.initialValue ?? '',
        resolve
      });
    });
  }

  /** Called by the host component when the user acts on the prompt dialog (null = cancelled). */
  resolvePrompt(result: string | null): void {
    const state = this.promptState();
    this.promptState.set(null);
    state?.resolve(result);
  }

  private pushToast(kind: ToastKind, message: string, ttlMs: number): void {
    const id = this.nextToastId++;
    this.toasts.update(list => [...list, { id, kind, message }]);
    setTimeout(() => this.dismissToast(id), ttlMs);
  }
}
