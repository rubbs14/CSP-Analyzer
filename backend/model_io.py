"""Safe (non-pickle) persistence for the fitted scaler/PCA/SVM pipeline.

`pickle.load` executes arbitrary code embedded in the file, and the original
`CSPv2/pickle_jar/*.pkl` artifacts are additionally stuck on scikit-learn
0.19.1's internal module layout: they no longer unpickle at all under a
current scikit-learn (see sub-project 4 / S4 spec,
`docs/superpowers/specs/2026-07-22-sub-project-4-model-handling-spec.md`).

This module stores the fitted parameters as plain numpy arrays (`.npz`,
`allow_pickle=False`) plus a small JSON sidecar of hyperparameters, and
reconstructs fresh estimator instances at load time. No pickle is involved
at runtime; only the one-off migration script
(`backend/scripts/migrate_legacy_pickles.py`) ever unpickles the legacy
files, and it does so once, offline, against a known trusted input.

SVC internals note: scikit-learn's binary-classification convention negates
`dual_coef_`/`intercept_` for the user-facing attributes relative to what
`predict`/`predict_proba` use internally (`_dual_coef_`/`_intercept_`).
`prediction_model.pkl` was trained under scikit-learn 0.19.1, which exposed
the *unnegated* (internal-convention) values directly as the public
`dual_coef_`/`intercept_` attributes. Verified empirically against a
scikit-learn 0.19.1 bridge environment (S4): reconstructing with
`_dual_coef_ = -dual_coef_` and `_intercept_ = -intercept_` reproduces the
original `predict_proba` output exactly; the unnegated mapping reproduces
the exact sign-flipped (wrong) decision function.
"""

import json

import numpy as np
from sklearn.decomposition import PCA
from sklearn.preprocessing import StandardScaler
from sklearn.svm import SVC


def save_scaler(scaler, path_prefix):
    np.savez(
        path_prefix + ".npz",
        mean_=scaler.mean_,
        scale_=scaler.scale_,
        var_=scaler.var_,
        n_samples_seen_=np.asarray(scaler.n_samples_seen_),
    )
    with open(path_prefix + ".json", "w") as f:
        json.dump({"with_mean": scaler.with_mean, "with_std": scaler.with_std}, f)


def load_scaler(path_prefix):
    arrs = np.load(path_prefix + ".npz", allow_pickle=False)
    with open(path_prefix + ".json") as f:
        params = json.load(f)
    scaler = StandardScaler(with_mean=params["with_mean"], with_std=params["with_std"])
    scaler.mean_ = arrs["mean_"]
    scaler.scale_ = arrs["scale_"]
    scaler.var_ = arrs["var_"]
    scaler.n_samples_seen_ = int(arrs["n_samples_seen_"])
    scaler.n_features_in_ = scaler.mean_.shape[0]
    return scaler


def save_pca(pca, path_prefix):
    np.savez(
        path_prefix + ".npz",
        components_=pca.components_,
        explained_variance_=pca.explained_variance_,
        explained_variance_ratio_=pca.explained_variance_ratio_,
        singular_values_=pca.singular_values_,
        mean_=pca.mean_,
        noise_variance_=np.asarray(pca.noise_variance_),
        n_samples_=np.asarray(pca.n_samples_),
    )
    with open(path_prefix + ".json", "w") as f:
        json.dump({"n_components": int(pca.n_components_)}, f)


def load_pca(path_prefix):
    arrs = np.load(path_prefix + ".npz", allow_pickle=False)
    with open(path_prefix + ".json") as f:
        params = json.load(f)
    pca = PCA(n_components=params["n_components"])
    pca.components_ = arrs["components_"]
    pca.explained_variance_ = arrs["explained_variance_"]
    pca.explained_variance_ratio_ = arrs["explained_variance_ratio_"]
    pca.singular_values_ = arrs["singular_values_"]
    pca.mean_ = arrs["mean_"]
    pca.noise_variance_ = float(arrs["noise_variance_"])
    pca.n_samples_ = int(arrs["n_samples_"])
    pca.n_features_in_ = pca.components_.shape[1]
    pca.n_components_ = params["n_components"]
    return pca


def _read_svc_attr(svm, name):
    """`n_support_`/`probA_`/`probB_` are read-only properties on current
    scikit-learn, backed by `_n_support`/`_probA`/`_probB` -- a freshly-fit
    SVC only has the private form. A *legacy unpickled* SVC is different: its
    old flat pickle state lands the plain public name straight into
    `__dict__` (unpickling bypasses the setter-less property), shadowed for
    normal attribute lookup by the class-level property but still readable
    via `__dict__`. Prefer that legacy value when present, else fall back to
    the normal (property) attribute access for a normally-fit SVC."""
    if name in svm.__dict__:
        return svm.__dict__[name]
    return getattr(svm, name)


def save_svc(svm, path_prefix):
    """`svm` may be either a *loaded legacy* SVC (see
    migrate_legacy_pickles.py) or a normally-fit current-scikit-learn SVC."""
    np.savez(
        path_prefix + ".npz",
        support_=svm.support_,
        support_vectors_=svm.support_vectors_,
        n_support_=np.asarray(_read_svc_attr(svm, "n_support_")).astype(np.int32),
        dual_coef_=svm.dual_coef_,
        intercept_=svm.intercept_,
        probA_=np.asarray(_read_svc_attr(svm, "probA_")),
        probB_=np.asarray(_read_svc_attr(svm, "probB_")),
        classes_=svm.classes_,
    )
    with open(path_prefix + ".json", "w") as f:
        json.dump(
            {
                "C": svm.C,
                "kernel": svm.kernel,
                "gamma": svm.gamma,
                "degree": svm.degree,
                "coef0": svm.coef0,
                "shrinking": svm.shrinking,
                "tol": svm.tol,
                "shape_fit_": list(svm.shape_fit_),
                "fit_status_": int(svm.fit_status_),
            },
            f,
        )


def load_svc(path_prefix):
    arrs = np.load(path_prefix + ".npz", allow_pickle=False)
    with open(path_prefix + ".json") as f:
        params = json.load(f)

    svm = SVC(
        C=params["C"],
        kernel=params["kernel"],
        gamma=params["gamma"],
        degree=params["degree"],
        coef0=params["coef0"],
        shrinking=params["shrinking"],
        tol=params["tol"],
        probability=True,
    )

    # `n_support_`/`probA_`/`probB_` are read-only properties on a fresh
    # SVC() (backed by `_n_support`/`_probA`/`_probB`, no setter) -- set only
    # the private backing attributes below, not these public names.
    svm.support_ = arrs["support_"]
    svm.support_vectors_ = arrs["support_vectors_"]
    svm.dual_coef_ = arrs["dual_coef_"]
    svm.intercept_ = arrs["intercept_"]
    svm.classes_ = arrs["classes_"]
    svm.shape_fit_ = tuple(params["shape_fit_"])
    svm.fit_status_ = params["fit_status_"]
    svm.n_features_in_ = svm.support_vectors_.shape[1]
    svm._sparse = False
    svm._effective_probability = True

    n_features = svm.support_vectors_.shape[1]
    if svm.gamma == "auto":
        svm._gamma = 1.0 / n_features
    elif isinstance(svm.gamma, str):
        raise ValueError(
            f"gamma={svm.gamma!r} isn't resolvable without the original "
            f"training data (only 'auto' is supported by this loader, since "
            f"it doesn't depend on X). Re-save with a numeric gamma instead."
        )
    else:
        svm._gamma = float(svm.gamma)
    svm._n_support = arrs["n_support_"]
    svm._probA = arrs["probA_"]
    svm._probB = arrs["probB_"]
    # See module docstring: legacy dual_coef_/intercept_ are already in the
    # public (flipped) convention, so the internal values predict/predict_proba
    # read are their negation.
    svm._dual_coef_ = -svm.dual_coef_
    svm._intercept_ = -svm.intercept_

    return svm
