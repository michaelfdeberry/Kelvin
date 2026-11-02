import { UnsafeSVGDirective } from 'lit/directives/unsafe-svg.js';

import type { DirectiveResult } from 'lit/directive.js';

export type NavItem = {
  href: string;
  label: string;
  icon: string | DirectiveResult<typeof UnsafeSVGDirective>;
};
