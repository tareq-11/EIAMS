#!/bin/bash
# Prints "online" when the PostgreSQL cluster accepts connections, else "offline".
if pg_isready -h 127.0.0.1 -p 5432 >/dev/null 2>&1; then
  echo online
else
  echo offline
fi
