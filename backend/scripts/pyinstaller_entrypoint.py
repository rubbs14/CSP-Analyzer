"""Entry point PyInstaller freezes into the `csp-backend` executable.

Equivalent to `python -m backend`, but PyInstaller needs a real script (not
a `-m` module invocation) as its Analysis entry point.
"""
import sys

from backend.__main__ import main

if __name__ == "__main__":
    sys.exit(main())
