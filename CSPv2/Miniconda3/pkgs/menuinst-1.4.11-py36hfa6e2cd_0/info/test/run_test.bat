



menuinst -h
IF %ERRORLEVEL% NEQ 0 exit 1
menuinst --version
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
