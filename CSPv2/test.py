#!/usr/bin/env python

import json
import sys
import time

#o = "Hello world"
print (type(sys.argv[1]))

with open(sys.argv[1]) as jsonin:
	data = json.loads(jsonin.readlines()[0])
	with open(sys.argv[2], 'w') as outfile:  
	    json.dump(data, outfile)
	
time.sleep(10)