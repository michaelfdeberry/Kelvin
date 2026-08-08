import { ApiResourcePath, ApiRouteParams } from './api-resources';
import { dispatchToast } from './utilities';

export type ApiFetchOptions = Omit<RequestInit, 'method' | 'body'> & {
  allowStatuses?: number[];
  routeParams?: ApiRouteParams;
  queryParams?: ApiQueryParams;
};

export type ApiBodyFetchOptions = { body: unknown } & ApiFetchOptions;

type ApiQueryParamValue = string | number | boolean | null | undefined;
type ApiQueryParams = Record<string, ApiQueryParamValue | ApiQueryParamValue[]>;

type ApiErrorPayload = {
  code?: string;
  message?: string;
};

const routeParamRegex = /\{([^}:]+)(?::[^}]+)?\}/g;

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

function appendQueryParams(path: string, queryParams?: ApiQueryParams): string {
  if (!queryParams) {
    return path;
  }

  const url = new URL(path, window.location.origin);
  for (const [key, value] of Object.entries(queryParams)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        if (item !== undefined && item !== null) {
          url.searchParams.append(key, String(item));
        }
      }
      continue;
    }

    if (value !== undefined && value !== null) {
      url.searchParams.set(key, String(value));
    }
  }

  return `${url.pathname}${url.search}`;
}

function resolvePathTemplate(resourcePath: ApiResourcePath, routeParams?: ApiRouteParams): string {
  return resourcePath.replace(routeParamRegex, (_, rawParamName: string) => {
    const value = routeParams?.[rawParamName];
    if (value === undefined || value === null) {
      throw new Error(`Missing route parameter "${rawParamName}" for API resource path: ${resourcePath}`);
    }

    return encodeURIComponent(String(value));
  });
}

async function apiFetch<T>(resourcePath: ApiResourcePath, init?: RequestInit, options?: ApiFetchOptions): Promise<T> {
  const { allowStatuses, routeParams, queryParams, ...requestOptions } = options ?? {};
  const requestInit: RequestInit = {
    ...requestOptions,
    ...(init ?? {}),
  };

  if (requestInit.method !== 'GET' && requestInit.method !== 'DELETE') {
    requestInit.headers = { ...(requestInit.headers ?? {}), 'Content-Type': 'application/json' };
  }

  const path = resolvePathTemplate(resourcePath, routeParams);
  const requestPath = appendQueryParams(path, queryParams);
  const response = await fetch(requestPath, requestInit);
  if (response.ok || allowStatuses?.includes(response.status)) {
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
    message: error?.message ?? `An error occurred while processing the request to ${requestPath}. (Status: ${response.status})`,
  });
  throw new Error(error?.code ?? 'Unable to process API request');
}

export function apiGet<T>(resourcePath: ApiResourcePath, options?: ApiFetchOptions): Promise<T> {
  return apiFetch<T>(resourcePath, { method: 'GET' }, options);
}

export function apiDelete<T>(resourcePath: ApiResourcePath, options?: ApiFetchOptions): Promise<T> {
  return apiFetch<T>(resourcePath, { method: 'DELETE' }, options);
}

export function apiPost<T>(resourcePath: ApiResourcePath, options: ApiBodyFetchOptions): Promise<T> {
  const { body, ...fetchOptions } = options;
  return apiFetch<T>(resourcePath, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }, fetchOptions);
}

export function apiPut<T>(resourcePath: ApiResourcePath, options: ApiBodyFetchOptions): Promise<T> {
  const { body, ...fetchOptions } = options;
  return apiFetch<T>(resourcePath, { method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }, fetchOptions);
}

export function apiPatch<T>(resourcePath: ApiResourcePath, options: ApiBodyFetchOptions): Promise<T> {
  const { body, ...fetchOptions } = options;
  return apiFetch<T>(resourcePath, { method: 'PATCH', body: body === undefined ? undefined : JSON.stringify(body) }, fetchOptions);
}
