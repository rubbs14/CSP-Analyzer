#!/bin/bash

# Create the directory to hold the certificates.
mkdir -p "${PREFIX}/ssl"

if [[ $(uname) == Darwin ]]; then
  curl http://anduin.linuxfromscratch.org/BLFS/other/certdata.txt > certdata.txt
else
  wget -c http://anduin.linuxfromscratch.org/BLFS/other/certdata.txt
fi
mkdir -p /tmp/ca-cerificates-$$
bash ./make-ca.sh-20170514 --openssl /usr/bin/openssl --destdir /tmp/ca-cerificates-$$/ --ssldir ssl --localdir /tmp/no_such_dir
echo "RESULT $?"
find ${PREFIX} -name "*.pem" -exec rm {} \;
mv /tmp/ca-cerificates-$$/ssl/ca-bundle.crt ${PREFIX}/ssl/cacert.pem
ln -fs "${PREFIX}/ssl/cacert.pem" "${PREFIX}/ssl/cert.pem"
