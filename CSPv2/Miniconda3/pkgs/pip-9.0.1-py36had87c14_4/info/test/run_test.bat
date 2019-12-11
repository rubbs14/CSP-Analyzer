



pip -h
IF %ERRORLEVEL% NEQ 0 exit 1
pip list
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
