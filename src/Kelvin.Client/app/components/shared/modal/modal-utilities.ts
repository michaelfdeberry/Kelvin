import './modal.js';

import { html, render } from 'lit';

import { Modal } from './modal.js';

export type ConfirmModalOptions = {
  confirmMessage?: string;
  rejectMessage?: string;
};

// slotted content only picks up shared button/form styles when it lives in kelvin-app's shadow root
function getAppRenderRoot(): ParentNode {
  return document.querySelector('kelvin-app')?.shadowRoot ?? document.body;
}

/** Shows a confirmation modal and resolves true/false based on the user's choice. */
export function confirmModal(message: string, options: ConfirmModalOptions = {}): Promise<boolean> {
  const { confirmMessage = 'Confirm', rejectMessage = 'Cancel' } = options;

  return new Promise<boolean>(resolve => {
    const container = document.createElement('div');
    let confirmed = false;

    const handleClosed = () => {
      modal.removeEventListener('modal-closed', handleClosed);
      container.remove();
      resolve(confirmed);
    };

    const handleReject = () => modal.hide('close-button');
    const handleConfirm = () => {
      confirmed = true;
      modal.hide('close-button');
    };

    render(
      html`
        <app-modal
          open
          heading="Confirm"
          small
          .closeOnBackdropClick=${false}
          style="--modal-backdrop-background: transparent"
          @modal-closed=${handleClosed}
        >
          <p>${message}</p>
          <div slot="actions">
            <button
              type="button"
              class="button button--secondary button--small"
              @click=${handleReject}
            >
              ${rejectMessage}
            </button>
            <button
              type="button"
              class="button button--primary button--small"
              @click=${handleConfirm}
            >
              ${confirmMessage}
            </button>
          </div>
        </app-modal>
      `,
      container,
    );

    const modal = container.querySelector('app-modal') as Modal;
    getAppRenderRoot().append(container);
  });
}
