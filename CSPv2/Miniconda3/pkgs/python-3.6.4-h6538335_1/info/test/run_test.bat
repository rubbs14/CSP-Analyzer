



python -V
IF %ERRORLEVEL% NEQ 0 exit 1
2to3 -h
IF %ERRORLEVEL% NEQ 0 exit 1
pydoc -h
IF %ERRORLEVEL% NEQ 0 exit 1
python -c "import sysconfig; print(sysconfig.get_config_var('CC'))"
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
