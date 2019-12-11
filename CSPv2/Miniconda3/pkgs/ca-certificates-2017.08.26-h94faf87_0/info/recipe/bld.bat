copy %RECIPE_DIR%\build.sh .

FOR /F "delims=" %%i IN ('cygpath.exe -u "%LIBRARY_PREFIX%"') DO set "LIBRARY_PREFIX=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%PREFIX%"') DO set "PREFIX=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%PYTHON%"') DO set "PYTHON=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%RECIPE_DIR%"') DO set "RECIPE_DIR=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%SP_DIR%"') DO set "SP_DIR=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%SRC_DIR%"') DO set "SRC_DIR=%%i"
FOR /F "delims=" %%i IN ('cygpath.exe -u "%STDLIB_DIR%"') DO set "STDLIB_DIR=%%i"

bash build.sh

if errorlevel 1 exit 1
copy /y "%PREFIX%"\ssl\cacert.pem "%LIBRARY_PREFIX%"\ssl\cacert.pem
copy /y "%LIBRARY_PREFIX%"\ssl\cacert.pem "%LIBRARY_PREFIX%"\ssl\cert.pem
del /y "%PREFIX%"\ssl\cert.pem
del /y "%PREFIX%"\ssl\cacert.pem
exit 0
