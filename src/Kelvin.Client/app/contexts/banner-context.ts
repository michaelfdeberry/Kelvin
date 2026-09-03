import { createContext } from '@lit/context';

import { ToastDetail } from '../models/toast-detail';

export type BannerContext = ToastDetail | undefined;

export const bannerContext = createContext<BannerContext>('banner');

export const defaultBanner: BannerContext = undefined;
