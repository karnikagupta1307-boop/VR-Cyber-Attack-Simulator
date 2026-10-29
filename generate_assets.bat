@echo off
rem Headless generation of scenes/prefabs/materials. Set UNITY to your Unity.exe, e.g.
rem set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if "%UNITY%"=="" set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
rem Do NOT add -quit. Exit code 2 = XR packages were just installed; the loop runs again.
set /a n=0
:again
set /a n+=1
%UNITY% -batchmode -projectPath "%CD%" -executeMethod CyberDefenseVR.EditorTools.CyberDefenseProjectBuilder.GenerateBatch -logFile generate_%n%.log
if %ERRORLEVEL%==2 if %n% LSS 3 goto again
echo Generator exit code: %ERRORLEVEL%  (0 = success; see generate_*.log and ValidationReport.txt)
exit /b %ERRORLEVEL%
