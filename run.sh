#!/bin/bash

# Kill any existing instance on our ports
lsof -ti:5178 -ti:7002 2>/dev/null | xargs -r kill -9

echo "Starting PIGv4..."

# Open browser after a short delay (gives server time to start)
(sleep 3 && vivaldi http://localhost:5178) &

dotnet run --project PIGv4
