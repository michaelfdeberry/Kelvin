import { dispatchToast } from './utilities';

type ApiFetchOptions = {
  allowStatuses?: number[];
};

type ApiErrorPayload = {
  code?: string;
  message?: string;
};

function isApiErrorPayload(value: unknown): value is ApiErrorPayload {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const payload = value as Record<string, unknown>;
  return typeof payload.code === 'string' && typeof payload.message === 'string';
}

async function getErrorMessage(response: Response): Promise<ApiErrorPayload | undefined> {
  try {
    const payload = (await response.clone().json()) as unknown;
    if (isApiErrorPayload(payload)) {
      return payload;
    }
  } catch {
    return undefined;
  }

  return undefined;
}

export async function apiFetch<T>(path: string, init?: RequestInit, options?: ApiFetchOptions): Promise<T> {
  if (init && init?.method !== 'GET' && init?.method !== 'DELETE') {
    init.headers = { ...(init?.headers ?? {}), 'Content-Type': 'application/json' };
  }

  const normalizedPath = path.startsWith('/') ? path.slice(1) : path;
  const response = await fetch(`/api/${normalizedPath}`, init);
  if (response.ok || options?.allowStatuses?.includes(response.status)) {
    if (response.status === 204) {
      return undefined as T;
    }

    const data = await response.json();
    return data as T;
  }

  const error = await getErrorMessage(response);
  dispatchToast(document, {
    type: 'error',
    duration: 30000,
    dismissible: true,
    message: error?.message ?? `An error occurred while processing the request to /api/${path}. (Status: ${response.status})`,
  });
  throw new Error(error?.code ?? 'Unable to process API request');
}
