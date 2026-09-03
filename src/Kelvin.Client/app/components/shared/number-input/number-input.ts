import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property, query } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';

import styles from './number-input.styles';
import sharedStyles from '../../../shared.styles';

const numberConverter = {
  fromAttribute: (value: string | null) => (value !== null ? Number(value) : undefined),
  toAttribute: (value: number | undefined) => (value !== undefined ? String(value) : null),
};

@customElement('app-number-input')
export class NumberInput extends LitElement {
  static override styles = [sharedStyles, styles];
  static readonly formAssociated = true;

  private readonly internals: ElementInternals = this.attachInternals();

  @query('input')
  private inputElement!: HTMLInputElement;

  @property({ type: Boolean, reflect: true })
  disabled = false;

  @property({ type: Boolean, reflect: true })
  readonly = false;

  @property({ type: Number })
  min = Number.MIN_SAFE_INTEGER;

  @property({ type: Number })
  max = Number.MAX_SAFE_INTEGER;

  @property({ type: Number })
  step = 0.5;

  @property({ type: Number, converter: numberConverter })
  value?: number;

  @property()
  for: string = '';

  @property({ type: String })
  name: string = '';

  @property({ type: String })
  placeholder: string = '';

  @property({ type: String })
  inputmode: string = 'decimal';

  @property({ type: String })
  pattern: string = '^[0-9]+(.[0-9]+)?$';

  private label: HTMLLabelElement | null = null;

  override connectedCallback(): void {
    super.connectedCallback();

    this.handleLabelClick = this.handleLabelClick.bind(this);

    this.syncFormValue();
    this.label = this.closest('label');
    this.label?.addEventListener('click', this.handleLabelClick);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.label?.removeEventListener('click', this.handleLabelClick);
  }

  private handleLabelClick = (): void => {
    this.inputElement.focus();
  };

  private handleIncrement(): void {
    const updatedValue = (this.value ?? 0) + this.step;
    if (updatedValue <= this.max) {
      this.value = updatedValue;
      this.syncFormValue();
    }
  }

  private handleDecrement(): void {
    const updatedValue = (this.value ?? 0) - this.step;
    if (updatedValue >= this.min) {
      this.value = updatedValue;
      this.syncFormValue();
    }
  }

  private handleInputChange(): void {
    const raw = this.inputElement.value;
    if (raw.trim() === '') {
      this.value = undefined;
      this.syncFormValue();
      return;
    }
    const parsed = this.inputElement.valueAsNumber;
    this.value = Number.isFinite(parsed) ? parsed : undefined;

    this.syncFormValue();
  }

  private syncFormValue(): void {
    if (this.disabled) {
      this.internals.setFormValue(null);
      return;
    }

    if (this.value === undefined || !Number.isFinite(this.value)) {
      this.internals.setFormValue(null);
      return;
    }

    this.internals.setFormValue(String(this.value));
  }

  override render(): TemplateResult {
    return html`
      <div class=${classMap({ 'number-input': true })}>
        <button
          class="number-input__button"
          type="button"
          aria-label="decrement"
          ?disabled=${this.disabled}
          @click=${this.handleDecrement}
        >
          -
        </button>
        <input
          id=${this.id}
          class="number-input__input"
          type="number"
          inputmode=${this.inputmode}
          pattern=${this.pattern}
          max=${this.max}
          min=${this.min}
          step=${this.step}
          placeholder=${this.placeholder}
          ?disabled=${this.disabled}
          ?readonly=${this.readonly}
          .value=${this.value !== undefined ? String(this.value) : ''}
          @input=${this.handleInputChange}
        />
        <button
          class="number-input__button"
          type="button"
          aria-label="increment"
          ?disabled=${this.disabled}
          @click=${this.handleIncrement}
        >
          +
        </button>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'app-number-input': NumberInput;
  }
}
