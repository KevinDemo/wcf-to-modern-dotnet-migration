#!/usr/bin/env bash
# Records the golden files from the real .NET Framework service. Only rerun this if the legacy service
# changes: the files are the frozen contract, and the compatibility tests compare the new host against them.
# Windows only. Needs no admin rights, see legacy/Harbour.Legacy.Service/App.config.
set -euo pipefail
cd "$(dirname "$0")/.."

URL="http://localhost:80/Temporary_Listen_Addresses/Harbour/ConsignmentService.svc"
GOLDEN="modern/tests/Harbour.Consignments.Tests/Golden"
SERVICE="legacy/Harbour.Legacy.Service/bin/Release/net48/Harbour.Legacy.Service.exe"
CLIENT="legacy/Harbour.Legacy.Client/bin/Release/net48/Harbour.Legacy.Client.exe"

dotnet build legacy/Harbour.Legacy.Service/Harbour.Legacy.Service.csproj -c Release -v q
dotnet build legacy/Harbour.Legacy.Client/Harbour.Legacy.Client.csproj -c Release -v q
dotnet build tools/GoldenCapture/GoldenCapture.csproj -c Release -v q

# First fresh service: raw SOAP traffic and WSDL.
"$SERVICE" > /dev/null &
PID=$!
sleep 3
dotnet tools/GoldenCapture/bin/Release/net10.0/GoldenCapture.dll "$URL" "$GOLDEN"
kill "$PID"
wait "$PID" 2>/dev/null || true
sleep 1

# Second fresh service: what the unmodified legacy client prints.
"$SERVICE" > /dev/null &
PID=$!
sleep 3
"$CLIENT" | tr -d '\r' > "$GOLDEN/legacy-client-transcript.txt"
kill "$PID"
wait "$PID" 2>/dev/null || true

cat "$GOLDEN/legacy-client-transcript.txt"
