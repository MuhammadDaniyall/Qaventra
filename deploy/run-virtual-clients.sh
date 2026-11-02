#!/bin/zsh
# Virtual clients on production, started by launchd every 30 minutes through the working day (see deploy/README.md).
# Each run does only what is due now; several runs a day are expected.
# Uses every deploy/secrets/logins-*.txt that seed-demo.mjs produced; does nothing until one exists.
cd "${0:A:h}/.." || exit 1
logins=(deploy/secrets/logins-*.txt(N))
if (( ! ${#logins} )); then
  echo "$(date '+%F %T') no deploy/secrets/logins-*.txt yet; seed a company first."
  exit 0
fi
echo "$(date '+%F %T') starting with ${(j:, :)logins}"
exec /opt/homebrew/bin/node deploy/virtual-clients.mjs --logins "${(j:,:)logins}"
