if [ "${USER:-}" = "KELVIN_USER" ] && [ -z "${DISPLAY:-}" ] && [ "$(tty)" = "/dev/tty1" ]; then
  exec startx -- :0 vt1
fi