const storageKey = 'kelvin.kiosk.mac';
const macAddressParam = 'mac';

export function normalizeMacAddress(value?: string | null): string {
  return (value ?? '').toLowerCase().replace(/[^0-9a-f]/g, '');
}

function isValidMacAddress(value: string): boolean {
  return value.length === 12;
}

function readStoredMacAddress(): string | null {
  try {
    const stored = normalizeMacAddress(window.localStorage.getItem(storageKey));
    return isValidMacAddress(stored) ? stored : null;
  } catch {
    return null;
  }
}

function storeMacAddress(macAddress: string): void {
  try {
    window.localStorage.setItem(storageKey, macAddress);
  } catch {
    // Private/blocked storage just means kiosk mode lasts for this page load only.
  }
}

function resolveKioskMacAddress(): string | null {
  const fromUrl = normalizeMacAddress(new URLSearchParams(window.location.search).get(macAddressParam));
  if (!isValidMacAddress(fromUrl)) return readStoredMacAddress();

  storeMacAddress(fromUrl);
  return fromUrl;
}

// Resolved once at module load: the kiosk identity cannot change without a reload.
export const kioskMacAddress = resolveKioskMacAddress();

export function isKioskMode(): boolean {
  return kioskMacAddress !== null;
}

document.documentElement.toggleAttribute('kiosk', isKioskMode());
