



conda activate base
IF %ERRORLEVEL% NEQ 0 exit 1
FOR /F "delims=" %%i IN ('python -c "import sys; print(sys.version_info[0])"') DO set "PYTHON_MAJOR_VERSION=%%i"
IF %ERRORLEVEL% NEQ 0 exit 1
set TEST_PLATFORM=win
IF %ERRORLEVEL% NEQ 0 exit 1
FOR /F "delims=" %%i IN ('python -c "import random as r; print(r.randint(0,4294967296))"') DO set "PYTHONHASHSEED=%%i"
IF %ERRORLEVEL% NEQ 0 exit 1
set
IF %ERRORLEVEL% NEQ 0 exit 1
conda info
IF %ERRORLEVEL% NEQ 0 exit 1
py.test tests -m "not integration and not installed" -vv
IF %ERRORLEVEL% NEQ 0 exit 1
conda create -y -p .\built-conda-test-env
IF %ERRORLEVEL% NEQ 0 exit 1
conda activate .\built-conda-test-env
IF %ERRORLEVEL% NEQ 0 exit 1
echo %CONDA_PREFIX%
IF %ERRORLEVEL% NEQ 0 exit 1
IF NOT "%CONDA_PREFIX%"=="%CD%\built-conda-test-env" EXIT /B 1
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
