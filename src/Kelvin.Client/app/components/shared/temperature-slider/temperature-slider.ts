import { consume } from '@lit/context';
import { html, nothing, TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { styleMap } from 'lit/directives/style-map.js';

import temperatureSliderStyles from './temperature-slider.styles';
import { preferencesContext } from '../../../contexts/preferences-context';
import { Preferences } from '../../../models/preferences';
import { getPreferredUnit } from '../../../services/utilities';
import sharedStyles from '../../../shared.styles';
import { Slider } from '../slider/slider';
import sliderStyles from '../slider/slider.styles';

const CELSIUS_MIN = 10;
const CELSIUS_MAX = 32;
const CELSIUS_STEP = 0.5;
const FAHRENHEIT_MIN = 50;
const FAHRENHEIT_MAX = 90;
const FAHRENHEIT_STEP = 1;

@customElement('app-temperature-slider')
export class TemperatureSlider extends Slider {
  static override styles = [sharedStyles, sliderStyles, temperatureSliderStyles];

  @consume({ context: preferencesContext, subscribe: true })
  @property({ attribute: false })
  preferences?: Preferences;

  @property({ type: Boolean, reflect: true })
  cooling = false;

  @property({ type: Boolean, reflect: true })
  heating = false;

  @property({ type: Number, attribute: 'current-temperature' })
  currentTemperature?: number;

  private previousDefaults = { min: 0, max: 100, step: 1 };

  override formatValue = (value: number): string => {
    const unit = this.preferences?.temperatureUnit;
    const formatted = unit === 'Celsius' ? value.toFixed(1) : String(Math.round(value));
    return `${formatted}${getPreferredUnit(unit ?? 'Fahrenheit')}`;
  };

  override willUpdate(changedProperties: Map<string, unknown>): void {
    if (this.min === this.previousDefaults.min && this.max === this.previousDefaults.max && this.step === this.previousDefaults.step) {
      const isCelsius = this.preferences?.temperatureUnit === 'Celsius';
      const defaults = {
        min: isCelsius ? CELSIUS_MIN : FAHRENHEIT_MIN,
        max: isCelsius ? CELSIUS_MAX : FAHRENHEIT_MAX,
        step: isCelsius ? CELSIUS_STEP : FAHRENHEIT_STEP,
      };
      if (this.min !== defaults.min || this.max !== defaults.max || this.step !== defaults.step) {
        this.min = defaults.min;
        this.max = defaults.max;
        this.step = defaults.step;
        changedProperties.set('min', undefined);
        changedProperties.set('max', undefined);
        changedProperties.set('step', undefined);
      }
      this.previousDefaults = defaults;
    }

    super.willUpdate(changedProperties);
  }

  override render(): TemplateResult {
    const hasCurrent = this.currentTemperature !== undefined && Number.isFinite(this.currentTemperature);
    const span = this.max - this.min;
    const currentPosition = hasCurrent && span > 0 ? Math.min(100, Math.max(0, ((this.currentTemperature! - this.min) / span) * 100)) : 0;

    return html`
      <div
        class="temperature-slider__readout"
        aria-live="off"
      >
        ${
          this.range
            ? html`
                <span class="temperature-slider__heating">Heat ${this.formatValue(this.valueLow)}</span>
                <span class="temperature-slider__cooling">Cool ${this.formatValue(this.valueHigh)}</span>
              `
            : html`<span
                class=${classMap({ 'temperature-slider__heating': this.heating, 'temperature-slider__cooling': this.cooling && !this.heating })}
              >
                ${this.heating ? 'Heat' : this.cooling ? 'Cool' : 'Target'} ${this.formatValue(this.value)}
              </span>`
        }
      </div>
      <div
        class=${classMap({ 'temperature-slider__scale': true, 'temperature-slider__scale--with-current': hasCurrent })}
        style=${styleMap({ '--current-position': `${currentPosition}%` })}
      >
        ${
          hasCurrent
            ? html`
                <span class="temperature-slider__current-label"
                  >Now ${this.currentTemperature!.toFixed(1)}${getPreferredUnit(this.preferences?.temperatureUnit ?? 'Fahrenheit')}</span
                >
                <span
                  class="temperature-slider__current-marker"
                  aria-hidden="true"
                ></span>
              `
            : nothing
        }
        ${super.render()}
        <div
          class="temperature-slider__limits"
          aria-hidden="true"
        >
          <span>${this.formatValue(this.min)}</span>
          <span>${this.formatValue(this.max)}</span>
        </div>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-temperature-slider': TemperatureSlider;
  }
}
