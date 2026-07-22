import numpy as np

from backend.model_io import load_pca, load_scaler, load_svc, save_pca, save_scaler, save_svc

REAL_MODEL_DIR = "backend/model_artifacts"


def test_real_svm_reproduces_pinned_probabilities_for_fixed_support_vectors():
    # Regression pin for the S4-migrated real model (backend/model_io.py +
    # backend/scripts/migrate_legacy_pickles.py): fidelity against the
    # original scikit-learn 0.19.1 pickle was verified during S4 against a
    # scikit-learn 0.19.1 "bridge" environment (predict_proba/decision_function
    # matched to float noise, ~1e-14, across both classes) - that bridge env
    # isn't part of this repo's toolchain, so this test instead pins fixed,
    # deterministic outputs (indexed support vectors, not random data) to
    # catch any future regression in model_io.py or the committed artifacts.
    svm = load_svc(f"{REAL_MODEL_DIR}/svm")
    sv = svm.support_vectors_
    idx = [0, sv.shape[0] // 2, sv.shape[0] - 1]

    proba = svm.predict_proba(sv[idx])[:, 1]

    np.testing.assert_allclose(
        proba,
        [0.8787025554676899, 0.4830487754006973, 0.863668689396668],
        rtol=1e-9,
    )


def test_svc_round_trip_preserves_predict_proba(tmp_path):
    from sklearn.svm import SVC

    rng = np.random.RandomState(0)
    half = 10
    x = np.vstack(
        [
            rng.normal(loc=0.0, scale=1.0, size=(half, 5)),
            rng.normal(loc=3.0, scale=1.0, size=(half, 5)),
        ]
    )
    y = np.array([0] * half + [1] * half)
    svm = SVC(gamma="auto", probability=True, random_state=0).fit(x, y)
    expected = svm.predict_proba(x)[:, 1]

    save_svc(svm, str(tmp_path / "svm"))
    reloaded = load_svc(str(tmp_path / "svm"))

    np.testing.assert_allclose(reloaded.predict_proba(x)[:, 1], expected, atol=1e-8)


def test_scaler_and_pca_round_trip_preserve_transform(tmp_path):
    from sklearn.decomposition import PCA
    from sklearn.preprocessing import StandardScaler

    rng = np.random.RandomState(0)
    x = rng.normal(size=(30, 8))

    scaler = StandardScaler().fit(x)
    pca = PCA(n_components=4, random_state=0).fit(scaler.transform(x))
    expected = pca.transform(scaler.transform(x))

    save_scaler(scaler, str(tmp_path / "scaler"))
    save_pca(pca, str(tmp_path / "pca"))
    reloaded_scaler = load_scaler(str(tmp_path / "scaler"))
    reloaded_pca = load_pca(str(tmp_path / "pca"))

    actual = reloaded_pca.transform(reloaded_scaler.transform(x))
    np.testing.assert_allclose(actual, expected, atol=1e-10)
