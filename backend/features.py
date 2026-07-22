from dataclasses import dataclass

import numpy as np
from scipy.spatial.distance import cdist, euclidean
from skimage.feature import ORB, hog
from skimage.measure import moments_hu, shannon_entropy
from skimage.metrics import (
    normalized_root_mse,
    peak_signal_noise_ratio,
    structural_similarity,
)
from skimage.registration import phase_cross_correlation

HIST_RANGE = [[95, 135], [4, 12]]
N_ORB_KEYPOINTS = 20
FEATURE_VECTOR_LENGTH = 11 + N_ORB_KEYPOINTS

# skimage's pre-1.0 auto data_range guess for float64 images used the fixed
# dtype_range of (-1, 1), i.e. a span of 2. structural_similarity now requires
# this to be passed explicitly for float input; hardcoding it here reproduces
# the value the pickled models were trained against.
SSIM_DATA_RANGE = 2.0


def two_dimensional_hist(spectrum, bins_per_array_dimension):
    twod_hist = np.histogram2d(
        spectrum[:, 0],
        spectrum[:, 1],
        weights=np.log10(spectrum[:, 2]),
        density=False,
        range=HIST_RANGE,
        bins=bins_per_array_dimension,
    )[0]

    return np.nan_to_num(twod_hist.T)


def reference_spectrum(peaklist, bins_per_array_dimension):
    twod_hist = two_dimensional_hist(peaklist, bins_per_array_dimension)
    threshold = 0.01
    twod_hist[twod_hist > threshold] = 1
    twod_hist[twod_hist < 1] = 0

    return twod_hist, threshold


def spectrum_process(peaklist, bins_per_array_dimension, reference_threshold):
    twod_hist = two_dimensional_hist(peaklist, bins_per_array_dimension)
    twod_hist[twod_hist >= reference_threshold] = 1
    twod_hist[twod_hist < 1] = 0
    return twod_hist


@dataclass
class ReferenceFeatures:
    hist: np.ndarray
    entropy: float
    moments: np.ndarray
    hog: np.ndarray
    orb_keypoints: np.ndarray


def compute_reference_features(reference_hist):
    orb = ORB()
    orb.detect(reference_hist)

    return ReferenceFeatures(
        hist=reference_hist,
        entropy=shannon_entropy(reference_hist),
        moments=moments_hu(reference_hist),
        hog=hog(reference_hist, block_norm="L2-Hys"),
        orb_keypoints=orb.keypoints,
    )


def comparator_function(target, reference_features):
    reference = reference_features.hist

    ent_diff = np.abs(reference_features.entropy - shannon_entropy(target))

    target_hog = hog(target, block_norm="L2-Hys")
    desc_dist = euclidean(reference_features.hog, target_hog)

    target_moments = moments_hu(target)
    moments_dist = euclidean(reference_features.moments, target_moments)

    target_orb = ORB()
    target_orb.detect(target)

    orb_dist = cdist(reference_features.orb_keypoints, target_orb.keypoints, "euclidean")
    orb_dist = np.sort(np.min(orb_dist, axis=1))[::-1]
    orb_dist_med = np.median(orb_dist)

    ssim_score = structural_similarity(
        target, reference, channel_axis=-1, data_range=SSIM_DATA_RANGE
    )

    # positional order (target, reference) preserved from the original
    # register_translation(target, reference, ...) call for numeric parity.
    _shifts, error, phase_diff = phase_cross_correlation(target, reference, space="real")

    scalars = np.zeros(FEATURE_VECTOR_LENGTH)
    scalars[0] = ssim_score
    scalars[1] = 0
    scalars[2] = desc_dist
    scalars[3] = normalized_root_mse(reference, target)
    scalars[4] = error
    scalars[5] = phase_diff
    scalars[6] = peak_signal_noise_ratio(reference, target)
    scalars[7] = ent_diff
    scalars[8] = moments_dist
    scalars[9] = np.abs(np.sum(target) - np.sum(reference))
    scalars[10] = orb_dist_med
    scalars[11:] = orb_dist[:N_ORB_KEYPOINTS]

    return scalars
