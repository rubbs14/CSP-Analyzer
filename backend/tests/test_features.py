import warnings

import numpy as np
import pytest

from backend.features import (
    FEATURE_VECTOR_LENGTH,
    comparator_function,
    compute_reference_features,
    reference_spectrum,
    spectrum_process,
    two_dimensional_hist,
)
from backend.tests.helpers import block_image as _block_image


def _peaklist(*peaks):
    return np.asarray(peaks, dtype=float)


def test_two_dimensional_hist_places_log10_weight_in_transposed_bin():
    # bins=5 over range [[95,135],[4,12]] -> x-width 8, y-width 1.6.
    # F1=100 falls in x-bin 0; F2=8 falls in y-bin 2.
    peaklist = _peaklist([100.0, 8.0, 100.0])

    result = two_dimensional_hist(peaklist, bins_per_array_dimension=5)

    assert result.shape == (5, 5)
    expected = np.zeros((5, 5))
    expected[2, 0] = 2.0  # log10(100) == 2, transposed to [y_bin, x_bin]
    np.testing.assert_array_almost_equal(result, expected)


def test_two_dimensional_hist_excludes_peaks_outside_range():
    peaklist = _peaklist([200.0, 8.0, 100.0])

    result = two_dimensional_hist(peaklist, bins_per_array_dimension=5)

    np.testing.assert_array_equal(result, np.zeros((5, 5)))


def test_two_dimensional_hist_has_no_nan_or_inf_for_zero_intensity_peak():
    peaklist = _peaklist([100.0, 8.0, 0.0])  # log10(0) == -inf

    result = two_dimensional_hist(peaklist, bins_per_array_dimension=5)

    assert np.isfinite(result).all()


def test_reference_spectrum_returns_fixed_threshold():
    peaklist = _peaklist([100.0, 8.0, 100.0])

    _hist, threshold = reference_spectrum(peaklist, bins_per_array_dimension=5)

    assert threshold == 0.01


def test_reference_spectrum_binarizes_values_above_threshold():
    peaklist = _peaklist([100.0, 8.0, 100.0])  # log10(100) == 2 > 0.01

    hist, _threshold = reference_spectrum(peaklist, bins_per_array_dimension=5)

    assert set(np.unique(hist)) <= {0.0, 1.0}
    assert hist[2, 0] == 1.0


def test_spectrum_process_binarizes_value_exactly_at_threshold():
    # log10(100000) == 5, exactly the reference_threshold: >= includes it.
    peaklist = _peaklist([100.0, 8.0, 100000.0])

    hist = spectrum_process(peaklist, bins_per_array_dimension=5, reference_threshold=5)

    assert hist[2, 0] == 1.0


def test_spectrum_process_zeroes_values_below_one():
    peaklist = _peaklist([100.0, 8.0, 2.0])  # log10(2) ~= 0.301 < 1

    hist = spectrum_process(peaklist, bins_per_array_dimension=5, reference_threshold=5)

    assert hist[2, 0] == 0.0


def test_spectrum_process_leaves_values_between_one_and_threshold_unbinarized():
    # Original behaviour (preserved for pickled-model parity): only the ">="
    # branch sets 1, and only "< 1" sets 0, so values in [1, reference_threshold)
    # pass through unchanged instead of being binarized to 0.
    peaklist = _peaklist([100.0, 8.0, 100.0])  # log10(100) == 2

    hist = spectrum_process(peaklist, bins_per_array_dimension=5, reference_threshold=5)

    assert hist[2, 0] == 2.0


def test_comparator_function_returns_fixed_length_vector():
    reference_features = compute_reference_features(_block_image())

    result = comparator_function(_block_image(shifted=True), reference_features)

    assert result.shape == (FEATURE_VECTOR_LENGTH,)
    assert result.dtype == np.float64


def test_comparator_function_self_comparison_is_a_zero_distance_fixed_point():
    image = _block_image()
    reference_features = compute_reference_features(image)

    with warnings.catch_warnings():
        # identical images drive PSNR's error term to zero -> log10(x/0) == inf,
        # matching the original implementation's behaviour on exact matches.
        warnings.simplefilter("ignore", RuntimeWarning)
        result = comparator_function(image, reference_features)

    assert result[0] == pytest.approx(1.0)  # ssim
    assert result[2] == pytest.approx(0.0)  # hog descriptor distance
    assert result[3] == pytest.approx(0.0)  # nrmse
    assert np.isinf(result[6])  # psnr diverges for an exact match
    assert result[7] == pytest.approx(0.0)  # entropy diff
    assert result[8] == pytest.approx(0.0)  # hu moments distance
    assert result[9] == pytest.approx(0.0)  # summed-intensity diff
    assert result[10] == pytest.approx(0.0)  # median orb keypoint distance
