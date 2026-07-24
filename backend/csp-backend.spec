# PyInstaller spec for S14 packaging. Freezes backend/ (+ numpy/scipy/
# scikit-learn/scikit-image) into a standalone `csp-backend` executable.
# Run from the repo root: `pyinstaller backend/csp-backend.spec`
#
# The hiddenimports list below is a best-effort starting point for known
# PyInstaller/sklearn/skimage gotchas (both packages dynamically dispatch
# into compiled Cython extension modules PyInstaller's static analysis can
# miss). Task 4 runs the real freeze and fixes this list against whatever
# ModuleNotFoundError actually shows up - don't trust this list as final
# without that verification step.
from pathlib import Path

REPO_ROOT = Path(SPECPATH).parent

a = Analysis(
    ['scripts/pyinstaller_entrypoint.py'],
    pathex=[str(REPO_ROOT)],
    binaries=[],
    datas=[],
    hiddenimports=[
        'sklearn.utils._typedefs',
        'sklearn.utils._heap',
        'sklearn.utils._sorting',
        'sklearn.utils._vector_sentinel',
        'sklearn.neighbors._partition_nodes',
        'skimage.feature._orb_descriptor_positions',
        'scipy.special.cython_special',
    ],
    hookspath=[],
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='csp-backend',
    debug=False,
    strip=False,
    upx=False,
    console=True,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    name='csp-backend',
)
