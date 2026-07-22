import os

import numpy as np

from backend.features import FEATURE_VECTOR_LENGTH, HIST_RANGE


def block_image(shifted=False, offset=None):
    # A grid of small blocks (with a few gaps) gives ORB enough corners to
    # detect >= N_ORB_KEYPOINTS keypoints, as a real 500x500 spectrum would.
    image = np.zeros((128, 128))
    if offset is None:
        offset = 2 if shifted else 0
    coords = [(y, x) for y in range(5, 120, 12) for x in range(5, 120, 12)]
    for i, (y, x) in enumerate(coords):
        if i % 3 == 0:
            continue
        image[y + offset : y + 6 + offset, x + offset : x + 6 + offset] = 1
    return image


def peaklist_from_image(image, intensity=50000.0):
    # Places one peak per "on" pixel at that histogram bin's center, so that
    # two_dimensional_hist(peaklist, bins=image.shape[0]) reproduces `image`
    # (post-binarization) exactly - lets tests drive the real io->features
    # pipeline with the same fixture images features.py's own tests use.
    n_bins = image.shape[0]
    dx = (HIST_RANGE[0][1] - HIST_RANGE[0][0]) / n_bins
    dy = (HIST_RANGE[1][1] - HIST_RANGE[1][0]) / n_bins

    peaks = []
    rows, cols = np.nonzero(image)
    for row, col in zip(rows, cols):
        f1 = HIST_RANGE[0][0] + (col + 0.5) * dx
        f2 = HIST_RANGE[1][0] + (row + 0.5) * dy
        peaks.append([f1, f2, intensity])
    return np.asarray(peaks)


def build_synthetic_pipeline():
    from sklearn.decomposition import PCA
    from sklearn.linear_model import LogisticRegression
    from sklearn.preprocessing import StandardScaler

    # classify.nmr_classification only needs the fit/transform/predict_proba
    # interface, so this fixture stands in for the real scaler/pca/svm trio
    # for wiring tests that don't care about the real model's numbers.
    # LogisticRegression is used instead of SVC(probability=True): the
    # latter's Platt-scaling CV is numerically borderline on these
    # out-of-distribution demo features and flipped sides of the 0.5
    # threshold between a bare interpreter and a pytest process
    # (BLAS-thread-count-sensitive), making a golden regression flaky.
    # LogisticRegression's lbfgs solver is deterministic. Returned as
    # in-memory objects, not files - since S4, `load_pipeline`'s own
    # file-loading is tested separately against `build_synthetic_model_dir`,
    # which mirrors the real (model_io npz/json) artifact format.
    rng = np.random.RandomState(0)
    half = 20
    class0 = rng.normal(loc=0.0, scale=1.0, size=(half, FEATURE_VECTOR_LENGTH))
    class1 = rng.normal(loc=3.0, scale=1.0, size=(half, FEATURE_VECTOR_LENGTH))
    x = np.vstack([class0, class1])
    y = np.array([0] * half + [1] * half)

    scaler = StandardScaler().fit(x)
    x_scaled = scaler.transform(x)

    pca = PCA(n_components=5, random_state=0).fit(x_scaled)
    x_pca = pca.transform(x_scaled)

    svm = LogisticRegression(random_state=0).fit(x_pca, y)

    return scaler, pca, svm


def build_synthetic_model_dir(model_dir):
    """A tiny *real* SVC-based pipeline, saved through `model_io.save_*` so
    it round-trips through `classify.load_pipeline` exactly like the real
    S4-migrated `model_artifacts/`. Used only to test `load_pipeline`'s file
    handling (success / missing / corrupt) - no golden values are pinned
    against it, so SVC's Platt-scaling-CV fit isn't a flakiness risk here."""
    from sklearn.decomposition import PCA
    from sklearn.preprocessing import StandardScaler
    from sklearn.svm import SVC

    from backend.model_io import save_pca, save_scaler, save_svc

    rng = np.random.RandomState(0)
    half = 10
    class0 = rng.normal(loc=0.0, scale=1.0, size=(half, FEATURE_VECTOR_LENGTH))
    class1 = rng.normal(loc=3.0, scale=1.0, size=(half, FEATURE_VECTOR_LENGTH))
    x = np.vstack([class0, class1])
    y = np.array([0] * half + [1] * half)

    scaler = StandardScaler().fit(x)
    x_scaled = scaler.transform(x)

    pca = PCA(n_components=5, random_state=0).fit(x_scaled)
    x_pca = pca.transform(x_scaled)

    # gamma="auto" matches the real pickle_jar model and is the only gamma
    # mode model_io resolves without the original training data (see
    # model_io.load_svc); the default gamma="scale" needs X's variance,
    # which isn't part of the persisted format.
    svm = SVC(gamma="auto", probability=True, random_state=0).fit(x_pca, y)

    os.makedirs(model_dir, exist_ok=True)
    save_scaler(scaler, os.path.join(model_dir, "scaler"))
    save_pca(pca, os.path.join(model_dir, "pca"))
    save_svc(svm, os.path.join(model_dir, "svm"))

    return model_dir


DEMO_BINS = 128  # matches block_image()'s fixed 128x128 shape


def build_demo_json(json_path, intensity=50000.0):
    reference_peaks = peaklist_from_image(block_image(offset=0), intensity)
    shifted_peaks = peaklist_from_image(block_image(offset=2), intensity)
    other_shifted_peaks = peaklist_from_image(block_image(offset=4), intensity)

    def _peak_records(peaklist):
        return [
            {"F1": f1, "F2": f2, "INTENSITY": inten} for f1, f2, inten in peaklist
        ]

    spectra = [
        {
            "JSON_Data": "Reference",
            "EXP_NUMBER": 0,
            "UserSelection": "Not set",
            "PEAKLIST": _peak_records(reference_peaks),
        },
        {
            "JSON_Data": "Experiment",
            "EXP_NUMBER": 1,
            "UserSelection": "Not set",
            "PEAKLIST": _peak_records(shifted_peaks),
        },
        {
            "JSON_Data": "Experiment",
            "EXP_NUMBER": 2,
            "UserSelection": "Not set",
            "PEAKLIST": _peak_records(other_shifted_peaks),
        },
    ]

    import json

    with open(json_path, "w") as f:
        json.dump(spectra, f)

    return json_path
