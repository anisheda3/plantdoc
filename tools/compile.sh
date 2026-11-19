#!/bin/bash
# Headless compile check: prints distinct compiler errors.
LOG="E:\plant\Logs\compile.log"
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -quit -projectPath "E:\plant" -logFile "$LOG" "$@"
echo exit=$?
grep -E "error CS" /e/plant/Logs/compile.log | sed 's/^.*Assets/Assets/' | sort -u | head -${MAXERR:-80}
echo "errors: $(grep -E 'error CS' /e/plant/Logs/compile.log | sort -u | wc -l)"
