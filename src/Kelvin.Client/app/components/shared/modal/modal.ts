import { html, LitElement, nothing } from 'lit';
import { customElement, property, query } from 'lit/decorators.js';

import modalStyles from './modal.styles.js';
import { dispatchCustomEvent } from '../../../services/utilities.js';
import sharedStyles from '../../../shared.styles.js';

type ModalCloseReason = 'escape' | 'backdrop' | 'close-button';

const focusableSelector =
  'a[href],button:not([disabled]),textarea:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex]:not([tabindex="-1"])';

@customElement('app-modal')
export class Modal extends LitElement {
  static override styles = [sharedStyles, modalStyles];

  @property({ type: Boolean, reflect: true }) open = false;
  @property({ type: String }) heading = 'Dialog';
  @property({ type: String }) description = '';
  @property({ type: Boolean, reflect: true }) small = false;
  @property({ type: Boolean }) closeOnBackdropClick = true;

  @query('.modal__dialog')
  private dialogElement!: HTMLElement;

  private lastFocusedElement: HTMLElement | null = null;

  override connectedCallback() {
    super.connectedCallback();
    document.addEventListener('keydown', this.handleDocumentKeyDown, { capture: true });
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener('keydown', this.handleDocumentKeyDown, { capture: true });
    this.unlockBodyScroll();
  }

  override async updated(changedProperties: Map<string, unknown>) {
    if (!changedProperties.has('open')) return;

    if (this.open) {
      this.lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.lockBodyScroll();
      await this.updateComplete;
      this.focusInitialElement();
    } else {
      this.unlockBodyScroll();
      this.restoreFocus();
    }
  }

  show() {
    this.open = true;
  }

  hide(reason: ModalCloseReason = 'close-button') {
    if (!this.open) {
      return;
    }

    this.open = false;
    dispatchCustomEvent(this, 'modal-closed', { reason });
  }

  private lockBodyScroll() {
    document.body.style.overflow = 'hidden';
  }

  private unlockBodyScroll() {
    document.body.style.removeProperty('overflow');
  }

  private restoreFocus() {
    if (!this.lastFocusedElement?.isConnected) {
      return;
    }

    this.lastFocusedElement.focus();
    this.lastFocusedElement = null;
  }

  private requestClose(reason: ModalCloseReason) {
    const requestEvent = new CustomEvent('modal-request-close', {
      bubbles: true,
      composed: true,
      cancelable: true,
      detail: { reason },
    });

    if (this.dispatchEvent(requestEvent)) {
      this.hide(reason);
    }
  }

  private isFocusable(element: HTMLElement): boolean {
    if (element.hasAttribute('disabled') || element.hidden || element.getAttribute('aria-hidden') === 'true') {
      return false;
    }

    if (element.tabIndex < 0) {
      return false;
    }

    const styles = window.getComputedStyle(element);
    return styles.display !== 'none' && styles.visibility !== 'hidden';
  }

  private appendFocusableUnique(elements: HTMLElement[], candidate: HTMLElement) {
    if (!elements.includes(candidate)) {
      elements.push(candidate);
    }
  }

  private collectFocusableFrom(root: ParentNode, elements: HTMLElement[], skipSlotFallback = false) {
    for (const element of root.querySelectorAll<HTMLElement>(focusableSelector)) {
      if (skipSlotFallback && element.closest('slot')) {
        continue;
      }

      if (this.isFocusable(element)) {
        this.appendFocusableUnique(elements, element);
      }
    }
  }

  private getFocusableElements(): HTMLElement[] {
    const focusableElements: HTMLElement[] = [];

    this.collectFocusableFrom(this.renderRoot, focusableElements, true);

    const slots = this.renderRoot.querySelectorAll('slot');
    for (const slot of slots) {
      const assignedElements = slot.assignedElements({ flatten: true });
      for (const assignedElement of assignedElements) {
        if (!(assignedElement instanceof HTMLElement)) {
          continue;
        }

        if (assignedElement.matches(focusableSelector) && this.isFocusable(assignedElement)) {
          this.appendFocusableUnique(focusableElements, assignedElement);
        }

        this.collectFocusableFrom(assignedElement, focusableElements);
      }
    }

    return focusableElements;
  }

  private getDeepActiveElement(root: Document | ShadowRoot): HTMLElement | null {
    let activeElement = root.activeElement;

    while (activeElement?.shadowRoot?.activeElement) {
      activeElement = activeElement.shadowRoot.activeElement;
    }

    return activeElement instanceof HTMLElement ? activeElement : null;
  }

  private getActiveElementInModal(): HTMLElement | null {
    const shadowActiveElement = this.shadowRoot ? this.getDeepActiveElement(this.shadowRoot) : null;
    if (shadowActiveElement && this.renderRoot.contains(shadowActiveElement)) {
      return shadowActiveElement;
    }

    const documentActiveElement = this.getDeepActiveElement(document);
    if (documentActiveElement && this.contains(documentActiveElement)) {
      return documentActiveElement;
    }

    return null;
  }

  private focusInitialElement() {
    const focusableElements = this.getFocusableElements();
    const firstFocusable = focusableElements[0];

    if (firstFocusable) {
      firstFocusable.focus();
      return;
    }

    this.dialogElement?.focus();
  }

  private handleDocumentKeyDown = (event: KeyboardEvent) => {
    if (!this.open) {
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.requestClose('escape');
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const focusableElements = this.getFocusableElements();
    if (focusableElements.length === 0) {
      event.preventDefault();
      this.dialogElement?.focus();
      return;
    }

    const firstFocusable = focusableElements[0];
    const lastFocusable = focusableElements[focusableElements.length - 1];
    if (!firstFocusable || !lastFocusable) {
      event.preventDefault();
      this.dialogElement?.focus();
      return;
    }

    const activeElement = this.getActiveElementInModal();

    const currentIndex = activeElement ? focusableElements.indexOf(activeElement) : -1;
    const fallbackIndex = event.shiftKey ? focusableElements.length - 1 : 0;
    const nextIndex =
      currentIndex === -1
        ? fallbackIndex
        : event.shiftKey
          ? (currentIndex - 1 + focusableElements.length) % focusableElements.length
          : (currentIndex + 1) % focusableElements.length;

    event.preventDefault();
    focusableElements[nextIndex]?.focus();
  };

  private onOverlayClick(event: Event) {
    if (event.target !== event.currentTarget || !this.closeOnBackdropClick) {
      return;
    }

    this.requestClose('backdrop');
  }

  private onCloseClick() {
    this.requestClose('close-button');
  }

  override render() {
    if (!this.open) return nothing;

    return html`
      <div
        class="modal"
        @click=${this.onOverlayClick}
      >
        <section
          class="modal__dialog"
          role="dialog"
          aria-modal="true"
          aria-label=${this.heading}
          aria-description=${this.description}
          tabindex="-1"
          @click=${(event: Event) => event.stopPropagation()}
        >
          <header class="modal__header">
            <h2 class="modal__title">${this.heading}</h2>
            <button
              type="button"
              class="modal__close"
              @click=${this.onCloseClick}
              aria-label="Close dialog"
            >
              <span aria-hidden="true">✖</span>
            </button>
          </header>

          <div class="modal__body">
            <slot></slot>
          </div>

          <footer class="modal__actions">
            <slot name="actions">
              <button
                type="button"
                class="button button--secondary"
                @click=${this.onCloseClick}
              >
                Close
              </button>
            </slot>
          </footer>
        </section>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-modal': Modal;
  }
}
