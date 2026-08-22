import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import toggleStyles from './toggle.styles';
import sharedStyles from '../../../shared.styles';

@customElement('app-toggle')
export class Toggle extends LitElement {
  static override styles = [sharedStyles, toggleStyles];
  static readonly formAssociated = true;

  private readonly internals: ElementInternals = this.attachInternals();

  @property({ type: Boolean, reflect: true })
  checked: boolean = false;

  @property({ type: String, reflect: true })
  name: string = '';

  @property({ type: String })
  value: string = 'on';

  @property({ type: Boolean, reflect: true })
  disabled: boolean = false;

  override connectedCallback(): void {
    super.connectedCallback();
    this.syncFormValue();
  }

  private syncFormValue(): void {
    // Native checkbox behavior: include field only when checked.
    this.internals.setFormValue(this.checked ? this.value : null);
  }

  private onInputChange(event: Event): void {
    const input: HTMLInputElement = event.currentTarget as HTMLInputElement;
    this.checked = input.checked;
    this.syncFormValue();

    this.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
  }

  override updated(changedProperties: Map<string, unknown>): void {
    if (changedProperties.has('checked') || changedProperties.has('value')) {
      this.syncFormValue();
    }
  }

  override render(): TemplateResult {
    return html`
      <label
        class="${classMap({
          toggle: true,
          'toggle--checked': this.checked,
        })}"
      >
        <input
          class="toggle__input"
          type="checkbox"
          .checked=${this.checked}
          ?disabled=${this.disabled}
          @change=${this.onInputChange}
        />
        <span class="toggle__slider">
          <span class="toggle__icon"> ${this.checked ? '✔' : '✖'} </span>
        </span>
        ${when(this.hasChildNodes(), () => html`<span class="toggle__label"><slot></slot></span>`)}
      </label>
    `;
  }
}
