import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';

import infoBubbleStyles from './info-bubble.styles.js';
import infoIcon from '../../../../assets/icons/info.svg?raw';

@customElement('app-info-bubble')
export class InfoBubble extends LitElement {
  static override styles = [infoBubbleStyles];

  @property({ type: String })
  icon = infoIcon;

  @property({ type: String })
  label = 'More information';

  @query('button')
  private trigger!: HTMLButtonElement;

  @query('[popover]')
  private bubble!: HTMLDivElement;

  @state()
  private open = false;

  private pinned = false;
  private closeTimer?: ReturnType<typeof setTimeout>;

  override connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('resize', this.positionBubble);
    document.addEventListener('scroll', this.positionBubble, true);
  }

  override disconnectedCallback(): void {
    clearTimeout(this.closeTimer);
    window.removeEventListener('resize', this.positionBubble);
    document.removeEventListener('scroll', this.positionBubble, true);
    super.disconnectedCallback();
  }

  private positionBubble = (): void => {
    if (!this.bubble?.matches(':popover-open')) return;

    const gap = 8;
    const gutter = 16;
    const anchor = this.trigger.getBoundingClientRect();
    const panel = this.bubble.getBoundingClientRect();
    const left = Math.max(gutter, Math.min(anchor.left, window.innerWidth - panel.width - gutter));
    const below = anchor.bottom + gap;
    const top = below + panel.height <= window.innerHeight - gutter ? below : Math.max(gutter, anchor.top - panel.height - gap);

    this.bubble.style.left = `${left}px`;
    this.bubble.style.top = `${top}px`;
  };

  private show(): void {
    clearTimeout(this.closeTimer);
    this.bubble.showPopover();
    this.positionBubble();
  }

  private hide(): void {
    clearTimeout(this.closeTimer);
    this.pinned = false;
    this.bubble.hidePopover();
  }

  private handlePointerEnter(event: PointerEvent): void {
    if (event.pointerType === 'mouse') this.show();
  }

  private handlePointerLeave(): void {
    this.closeTimer = setTimeout(() => {
      if (!this.pinned && !this.matches(':focus-within')) this.hide();
    }, 150);
  }

  private handleClick(): void {
    if (this.pinned) {
      this.hide();
    } else {
      this.pinned = true;
      this.show();
    }
  }

  private handleFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget;
    if (next instanceof Node && (this.contains(next) || this.shadowRoot?.contains(next))) return;
    this.hide();
  }

  private handleToggle(event: ToggleEvent): void {
    this.open = event.newState === 'open';
    if (!this.open) this.pinned = false;
  }

  override render(): TemplateResult {
    return html`
      <span
        @pointerenter=${this.handlePointerEnter}
        @pointerleave=${this.handlePointerLeave}
        @focusout=${this.handleFocusOut}
      >
        <button
          type="button"
          aria-label=${this.label}
          aria-expanded=${this.open}
          aria-controls="content"
          aria-describedby="content"
          @focus=${this.show}
          @click=${this.handleClick}
        >
          <span aria-hidden="true">${unsafeSVG(this.icon)}</span>
        </button>
        <div
          id="content"
          popover="auto"
          role="tooltip"
          @toggle=${this.handleToggle}
        >
          <slot @slotchange=${this.positionBubble}></slot>
        </div>
      </span>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'app-info-bubble': InfoBubble;
  }
}
