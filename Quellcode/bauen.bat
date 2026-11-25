@echo off
rem Baut Johnny.exe aus dem Quellcode. Benötigt nur Windows (der C#-Compiler von .NET Framework 4 ist eingebaut).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Der C#-Compiler wurde nicht gefunden. Ist .NET Framework 4 installiert?
  pause
  exit /b 1
)
cd /d "%~dp0"
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /platform:anycpu /out:"..\Johnny.exe" /win32icon:johnny.ico /win32manifest:app.manifest /resource:lulu.png,lulu.png /r:System.Windows.Forms.dll /r:System.Drawing.dll *.cs
if errorlevel 1 (
  echo Fehler beim Bauen.
  pause
  exit /b 1
)
echo Fertig: ..\Johnny.exe
