@echo off
rem Compile GtaPcWatch.exe avec le compilateur C# livre avec Windows (.NET Framework 4.x).
rem GtaPcWatch.cs = moteur de releve ; EyeWindow.cs = la fenetre (oeil, podium, langues).
rem
rem Deux passes : la premiere produit le programme, qui dessine alors sa propre icone (eye.ico) ;
rem la seconde recompile en incorporant cette icone dans le fichier .exe.
setlocal
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set OPTS=/nologo /codepage:65001 /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Web.Extensions.dll

%CSC% %OPTS% /out:"%~dp0GtaPcWatch.exe" "%~dp0GtaPcWatch.cs" "%~dp0EyeWindow.cs" || exit /b 1
start "" /wait "%~dp0GtaPcWatch.exe" --makeicon="%~dp0eye.ico"
if not exist "%~dp0eye.ico" exit /b 0
%CSC% %OPTS% /win32icon:"%~dp0eye.ico" /out:"%~dp0GtaPcWatch.exe" "%~dp0GtaPcWatch.cs" "%~dp0EyeWindow.cs" || exit /b 1
