



if not exist %LIBRARY_INC%\\yaml.h exit 1
IF %ERRORLEVEL% NEQ 0 exit 1
if not exist %LIBRARY_LIB%\\yaml_static.lib exit 1
IF %ERRORLEVEL% NEQ 0 exit 1
if not exist %LIBRARY_LIB%\\yaml.lib exit 1
IF %ERRORLEVEL% NEQ 0 exit 1
if not exist %LIBRARY_BIN%\\yaml.dll exit 1
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
