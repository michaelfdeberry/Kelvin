import { Task, TaskStatus } from '@lit/task';
import { html, LitElement } from 'lit';
import { customElement, query, state } from 'lit/decorators.js';

import { map } from 'lit/directives/map.js';
import { when } from 'lit/directives/when.js';
import { CurrentLocationResponse } from '../../models/current-location-response.js';
import { SearchLocationsResponse } from '../../models/search-locations-response.js';
import sharedStyles from '../../shared.styles.js';
import settingsLocationTabStyles from './settings-location.styles.js';

@customElement('app-settings-location')
export class SettingsLocationTab extends LitElement {
  static override styles = [sharedStyles, settingsLocationTabStyles];

  @query('.settings-location-tab__input') searchInput?: HTMLInputElement;

  @state()
  private savePending = false;

  @state()
  private selectedLocationId?: number;

  @state()
  private isEditing = false;

  private currentLocationTask = new Task(this, {
    task: async (_, { signal }) => {
      const response = await fetch('/api/locations/current', { signal });
      if (response.status === 412) {
        return undefined;
      }

      const result = await response.json();
      if (!response.ok) {
        if (result.message) {
          throw new Error(result.message);
        }

        throw new Error('Failed to load current location');
      }

      return result as CurrentLocationResponse;
    },
    args: () => [],
  });

  private searchLocationsTask = new Task(this, {
    autoRun: false,
    task: async (_, { signal }) => {
      const query = this.searchInput?.value.trim() ?? '';
      if (!query) {
        throw new Error('Enter a city or town to search.');
      }

      const response = await fetch(`/api/locations/search?query=${encodeURIComponent(query)}`, { signal });
      const data = await response.json();
      if (!response.ok) {
        if (data.message) {
          throw new Error(data.message);
        }

        throw new Error('Failed to search locations');
      }

      const locationsResponse = data as SearchLocationsResponse;
      if (locationsResponse.locations.length === 0) {
        throw new Error('No matching locations were found.');
      }

      return locationsResponse.locations;
    },
    args: () => [],
  });

  private onSearchSubmit(event: SubmitEvent) {
    event.preventDefault();
    this.searchLocationsTask.run();
  }

  private async setCurrentLocation(locationId?: number) {
    if (!locationId) {
      return;
    }

    this.savePending = true;

    try {
      const response = await fetch('/api/locations/current', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ locationId }),
      });

      const result = response.status === 204 ? null : await response.json();
      if (!response.ok) {
        if (result?.message) {
          throw new Error(result.message);
        }

        throw new Error('Failed to set the current location');
      }

      this.selectedLocationId = undefined;
      this.isEditing = false;
      this.currentLocationTask.run();
    } finally {
      this.savePending = false;
    }
  }

  private formatLocation(location: CurrentLocationResponse): string {
    const parts = [location.name, location.admin1, location.country].filter(part => !!part);
    return parts.join(', ');
  }

  private renderLocationEditor() {
    return html`
      <h2 class="settings-location-tab__title">Search and Set Location</h2>
      <form
        class="settings-location-tab__search"
        @submit=${(event: Event) => this.onSearchSubmit(event as SubmitEvent)}
      >
        <input
          class="input settings-location-tab__input"
          type="text" 
          placeholder="Search by city, town, or postal code"
          aria-label="Search location"
          autocomplete="off"
        />

        <button
          class="button settings-location-tab__button"
          type="submit"
          ?disabled=${this.searchLocationsTask.status === TaskStatus.PENDING}
        >
          ${when(
            this.searchLocationsTask.status === TaskStatus.PENDING,
            () => html`Searching...`,
            () => html`Search`,
          )}
        </button>
      </form> 
        ${this.searchLocationsTask.render({
          pending: () => html`<p class="settings-location-tab__hint">Searching for locations...</p>`,
          complete: searchResults => html`
            <div class="radios">
              ${map(
                searchResults,
                location => html`
                  <label class="radio__label">
                    <input
                      class="radio"
                      type="radio"
                      name="location"
                      .value=${location.id.toString()}
                      .checked=${this.selectedLocationId === location.id}
                      @change=${() => (this.selectedLocationId = location.id)}
                    />
                    <span>${this.formatLocation(location)}</span>
                  </label>
                `,
              )}
            </div>
          `,
          error: error => {
            const message = error instanceof Error ? error.message : 'Unknown error';
            return html`<p class="settings-location-tab__error">${message}</p>`;
          },
        })}
        <div class="settings-location-tab__actions">
          <button class="button" @click=${() => (this.isEditing = false)}>Cancel</button>
          <button
            class="button button--primary settings-location-tab__button"
            type="button"
            ?disabled=${!this.selectedLocationId || this.savePending}
            @click=${() => this.setCurrentLocation(this.selectedLocationId)}
          >
            ${this.savePending ? 'Saving...' : 'Set Current Location'}
          </button>
        </div>
      </div>
    `;
  }

  private renderCurrentLocation() {
    return html`
        <div class="settings-location-tab__card-content">
          ${this.currentLocationTask.render({
            pending: () => html`loading...`,
            complete: currentLocation => html`
              <div>
                <h2 class="settings-location-tab__title">Configured Location</h2>
                ${when(
                  currentLocation,
                  () => html`
                    <p class="settings-location-tab__meta">${this.formatLocation(currentLocation!)}</p>
                    <p class="settings-location-tab__hint">
                      Coordinates: ${currentLocation!.latitude.toFixed(4)}, ${currentLocation!.longitude.toFixed(4)}
                    </p>
                  `,
                )}
                ${when(!currentLocation, () => html`<p class="settings-location-tab__meta">No location configured yet.</p>`)}
              </div>
              <div>
                <button
                  class="button"
                  @click=${() => (this.isEditing = true)}
                >
                  Edit
                </button>
              </div>
            `,
            error: error => {
              const message = error instanceof Error ? error.message : 'Unknown error';
              return message;
            },
          })}
        </div>
      </article>
    `;
  }

  override render() {
    return html`
      <section
        class="settings-location-tab"
        role="tabpanel"
        aria-label="Current location settings"
      >
        <article class="settings-location-tab__card">
          ${when(!this.isEditing, () => this.renderCurrentLocation())} ${when(this.isEditing, () => this.renderLocationEditor())}
        </article>
      </section>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-settings-location-tab': SettingsLocationTab;
  }
}
