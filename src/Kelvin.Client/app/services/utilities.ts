import { events } from '../events.js';

import type { AlertType } from '../models/alert-type.js';
import type { ToastDetail } from '../models/toast-detail.js';

type ToastOptions = Omit<ToastDetail, 'type' | 'message'>;

export function dispatchCustomEvent<T>(element: Element | Document | Window, eventName: string, detail?: T, init?: CustomEventInit<T>): boolean {
  return element.dispatchEvent(
    new CustomEvent<T>(eventName, {
      bubbles: true,
      composed: true,
      ...init,
      detail,
    }),
  );
}

export function dispatchToast(element: Element | Document | Window, detail: ToastDetail): boolean;
export function dispatchToast(element: Element | Document | Window, type: AlertType, message: string, options?: ToastOptions): boolean;
export function dispatchToast(
  element: Element | Document | Window,
  typeOrDetail: AlertType | ToastDetail,
  message?: string,
  options?: ToastOptions,
): boolean {
  const detail: ToastDetail =
    typeof typeOrDetail === 'string'
      ? {
          type: typeOrDetail,
          message: message ?? '',
          duration: options?.duration ?? 3000,
          ...options,
        }
      : typeOrDetail;

  return dispatchCustomEvent<ToastDetail>(element, events.toast, detail);
}
