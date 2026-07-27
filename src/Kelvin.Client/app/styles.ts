import { css } from "lit";

export default css`
  ::view-transition-old(main-content) {
    animation: 250ms ease-out both fade-out;
  }

  ::view-transition-new(main-content) {
    animation: 300ms ease-in both fade-in;
  }

  @keyframes fade-in {
    from { opacity: 0; transform: translateY(8px); }
    to { opacity: 1; transform: translateY(0); }
  }

  @keyframes fade-out {
    from { opacity: 1; }
    to { opacity: 0; transform: translateY(-8px); }
  }
`