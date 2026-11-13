@echo off
rem Build RaptorFlowGen.exe with the C# compiler that ships with .NET Framework 4.x
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Cannot find csc.exe - please install .NET Framework 4.x
    exit /b 1
)
"%CSC%" /nologo /target:exe /out:RaptorFlowGen.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\RaptorFlowGen.cs
if errorlevel 1 exit /b 1
echo Built RaptorFlowGen.exe
