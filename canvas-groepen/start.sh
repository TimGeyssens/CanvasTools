#!/bin/bash
# Start de Canvas groepentool met de instellingen uit canvas-groepen.env
cd "$(dirname "$0")" || exit 1
set -a
[ -f canvas-groepen.env ] && . ./canvas-groepen.env
set +a
exec python3 server.py
