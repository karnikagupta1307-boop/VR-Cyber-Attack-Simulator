#!/bin/sh
# Headless generation of scenes/prefabs/materials. Usage: UNITY=/path/to/Unity ./generate_assets.sh
# Do NOT add -quit (the generator exits the editor itself). Exit code 2 = XR packages were just installed, run again (handled below).
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity}"
code=0
for i in 1 2 3; do
  "$UNITY" -batchmode -projectPath "$(pwd)" -executeMethod CyberDefenseVR.EditorTools.CyberDefenseProjectBuilder.GenerateBatch -logFile "generate_$i.log"
  code=$?
  [ "$code" -ne 2 ] && break
done
echo "Generator exit code: $code (0 = success; see generate_*.log and ValidationReport.txt)"
exit $code
