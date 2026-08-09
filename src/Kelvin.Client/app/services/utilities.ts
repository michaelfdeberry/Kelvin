import { events } from '../events.js';
import { TemperatureUnit } from '../models/preferences.js';

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

export function toPreferredUnit(temperatureUnit: TemperatureUnit, celsius?: number, fractionDigits: number = 1): string {
  if (celsius === undefined || celsius === null) return '';
  if (temperatureUnit === 'Celsius') return celsius.toFixed(fractionDigits);
  return ((celsius * 9) / 5 + 32).toFixed(fractionDigits);
}

export function fromPreferredUnit(temperatureUnit: TemperatureUnit, value: number): number {
  if (temperatureUnit === 'Celsius') return value;
  return ((value - 32) * 5) / 9;
}

export function getPreferredUnit(temperatureUnit: TemperatureUnit): string {
  return temperatureUnit === 'Fahrenheit' ? '°F' : '°C';
}
