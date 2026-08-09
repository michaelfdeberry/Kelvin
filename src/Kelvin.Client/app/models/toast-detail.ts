import { AlertType } from './alert-type';

import type { TemplateResult } from 'lit';

export type ToastDetail = {
  type: AlertType;
  message: string | TemplateResult;
  heading?: string;
  dismissible?: boolean;
  duration?: number;
};
