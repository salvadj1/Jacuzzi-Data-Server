@echo off
rem =====================================================================
rem  iniciar_servicio.bat
rem  Instala (si no existe) y arranca el servicio de Windows "JacuzziServer".
rem  - Se auto-eleva a administrador.
rem  - Crea el servicio con inicio AUTOMATICO: arranca solo con Windows,
rem    sin necesidad de iniciar sesion.
rem  - Configura reinicio automatico si el servicio se cae.
rem  Debe estar en la misma carpeta que JacuzziServer.exe (carpeta publicada).
rem =====================================================================
setlocal EnableExtensions

set "SERVICIO=JacuzziServer"
set "EXE=%~dp0JacuzziServer.exe"
rem El puerto debe coincidir con "Urls" de appsettings.json
set "PUERTO=8000"

rem --- Auto-elevacion a administrador ---
net session >nul 2>&1
if errorlevel 1 goto :elevar
goto :comprobar

:elevar
echo Solicitando permisos de administrador...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:comprobar
if not exist "%EXE%" goto :err_exe

rem --- Si el servicio ya existe, solo se asegura el autoarranque y se inicia ---
sc query "%SERVICIO%" >nul 2>&1
if not errorlevel 1 goto :ya_existe

rem --- Servicio nuevo: el puerto debe estar libre ---
netstat -ano | findstr /R /C:":%PUERTO% .*LISTENING" >nul
if not errorlevel 1 goto :err_puerto

echo Creando el servicio "%SERVICIO%"...
sc create "%SERVICIO%" binPath= "\"%EXE%\"" start= auto DisplayName= "Jacuzzi Server" >nul
if errorlevel 1 goto :err_crear
sc description "%SERVICIO%" "Servidor de historico del jacuzzi (ASP.NET)" >nul
sc failure "%SERVICIO%" reset= 86400 actions= restart/5000/restart/5000/restart/30000 >nul
goto :arrancar

:ya_existe
echo El servicio "%SERVICIO%" ya existe. Se asegura el inicio automatico...
sc config "%SERVICIO%" start= auto >nul

:arrancar
sc query "%SERVICIO%" | find "RUNNING" >nul
if not errorlevel 1 goto :ya_corriendo
echo Iniciando el servicio...
net start "%SERVICIO%"
if errorlevel 1 goto :err_arrancar
echo.
echo [OK] Servicio "%SERVICIO%" iniciado y configurado para arrancar con Windows.
echo      Comprueba: http://localhost:%PUERTO%/api/health
goto :fin

:ya_corriendo
echo [OK] El servicio "%SERVICIO%" ya estaba en marcha.
goto :fin

:err_exe
echo [ERROR] No se encuentra "%EXE%"
echo         Coloca este .bat junto a JacuzziServer.exe (carpeta de publicacion).
goto :fin_error

:err_puerto
echo [ERROR] El puerto %PUERTO% ya esta en uso por otro programa.
echo         Cierralo o cambia "Urls" en appsettings.json y PUERTO en este .bat.
goto :fin_error

:err_crear
echo [ERROR] No se pudo crear el servicio. Revisa que ejecutas como administrador.
goto :fin_error

:err_arrancar
echo [ERROR] El servicio se creo pero no pudo iniciarse.
echo         Revisa el Visor de eventos de Windows y appsettings.json.
goto :fin_error

:fin_error
echo.
pause
exit /b 1

:fin
echo.
pause
exit /b 0
