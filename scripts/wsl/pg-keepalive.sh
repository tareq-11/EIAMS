#!/bin/bash
# Keeps the WSL VM alive so PostgreSQL stays reachable from Windows.
# The VM suspends after a period of inactivity, which makes port 5432 refuse
# connections and then crashes the API on its next database call.
while true; do
  sleep 30
  psql -h 127.0.0.1 -U postgres -d eiams -c "SELECT 1" >/dev/null 2>&1
done
