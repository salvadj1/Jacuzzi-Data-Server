@echo off
rem =====================================================================
rem  detener_servicio.bat
rem  Detiene el servicio de Windows "JacuzziServer" y lo ELIMINA, quitando
rem  la regla de inicio automatico con Windows.
rem  - Se auto-eleva a administrador.
rem  Para volver a activarlo, ejecuta iniciar_servicio.bat.
rem =====================================================================
setlocal EnableExtensions

set "SERVICIO=JacuzziServer"

rem --- Auto-elevacion a administrador ---
net session >nul 2>&1
if errorlevel 1 goto :elevar
goto :comprobar

:elevar
echo Solicitando permisos de administrador...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:comprobar
sc query "%SERVICIO%" >nul 2>&1
if errorlevel 1 goto :no_existe

sc query "%SERVICIO%" | find "STOPPED" >nul
if not errorlevel 1 goto :eliminar

echo Deteniendo el servicio...
net stop "%SERVICIO%"
if errorlevel 1 goto :err_parar

:eliminar
echo Eliminando el servicio y su inicio automatico...
sc delete "%SERVICIO%" >nul
if errorlevel 1 goto :err_eliminar
echo.
echo [OK] Servicio "%SERVICIO%" detenido y eliminado. Ya no arrancara con Windows.
goto :fin

:no_existe
echo [INFO] El servicio "%SERVICIO%" no existe. No hay nada que hacer.
goto :fin

:err_parar
echo [ERROR] No se pudo detener el servicio.
goto :fin_error

:err_eliminar
echo [ERROR] No se pudo eliminar el servicio. Cierra services.msc si lo tienes abierto.
goto :fin_error

:fin_error
echo.
pause
exit /b 1

:fin
echo.
pause
exit /b 0
