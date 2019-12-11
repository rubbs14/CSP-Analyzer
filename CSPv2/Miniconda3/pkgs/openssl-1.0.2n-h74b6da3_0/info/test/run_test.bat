



copy NUL checksum.txt
IF %ERRORLEVEL% NEQ 0 exit 1
openssl sha256 checksum.txt
IF %ERRORLEVEL% NEQ 0 exit 1
exit 0
