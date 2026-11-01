#!/bin/zsh
# Daily virtual clients run on production, started by launchd (see deploy/README.md).
# Uses every deploy/secrets/logins-*.txt that seed-demo.mjs produced; does nothing until one exists.
cd "${0:A:h}/.." || exit 1
logins=(deploy/secrets/logins-*.txt(N))
if (( ! ${#logins} )); then
  echo "$(date '+%F %T') no deploy/secrets/logins-*.txt yet; seed a company first."
  exit 0
fi
echo "$(date '+%F %T') starting with ${(j:, :)logins}"
exec /opt/homebrew/bin/node deploy/virtual-clients.mjs --logins "${(j:,:)logins}"
