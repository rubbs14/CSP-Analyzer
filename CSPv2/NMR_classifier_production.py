# coding: utf-8

import csv
import multiprocessing as mp
import os

import matplotlib

matplotlib.use('Agg')

import sys

from skimage.measure import compare_ssim as ssim

from skimage.measure import shannon_entropy, compare_nrmse
from skimage.feature import hog, register_translation
from scipy.spatial.distance import euclidean, cdist


import numpy as np
from skimage.measure import compare_psnr
from skimage.measure import moments_hu
from skimage.feature import ORB

import json

import pickle


def json_parser(json_file, intensity_threshold, debug_mode=False):
    id_to_peaklist = {'Reference': [], 'Experiment': {}}
    id_to_activity = {}
    with open(json_file, 'r') as jsp:
        js_str = jsp.read()
        try:
            js = json.loads(js_str)
        except:
            js_str = '[{}]'.format(js_str)
            js = json.loads(js_str)
        finally:
            if 'js' not in locals():
                print('JSON loading failed')
                return None

        for spectrum in js:
            if debug_mode:
                activity = spectrum["UserSelection"]

                if activity == "ACTIVE (MAN)":
                    activity = 1
                else:
                    activity = 0
                id_to_activity[int(spectrum['EXP_NUMBER'])] = activity

            peak_list_type = spectrum['JSON_Data']
            peaklist = []
            for peak in spectrum['PEAKLIST']:
                if float(peak['INTENSITY']) >= intensity_threshold:
                    formatted_peak = [float(v) for k, v in peak.items() if k in ['F1', 'F2', 'INTENSITY']]
                    peaklist.append(formatted_peak)

            if peaklist:
                peaklist = np.asarray(peaklist)

                if peak_list_type == 'Reference':
                    id_to_peaklist['Reference'] = peaklist
                else:
                    id_to_peaklist['Experiment'][int(spectrum['EXP_NUMBER'])] = peaklist

    if not debug_mode:
        return id_to_peaklist
    else:
        return id_to_peaklist, id_to_activity


def class_id_dict_reader(class_information_location):
    id_to_activity_dict = {}
    with open(class_information_location) as id_to_act_map:
        csvr = csv.reader(id_to_act_map)
        for row in csvr:
            identifier, activity = row
            if int(activity) == 1:
                pass
            if int(activity) == 2:
                activity = 1
            id_to_activity_dict[int(identifier[:-1])] = int(activity)
    return id_to_activity_dict


def spectra_processor(json_location, peak_threshold, bins_per_array_dimension, debug_mode=False):
    if debug_mode:
        ref_exp_dict, id_to_activity_dict = json_parser(json_location, peak_threshold, debug_mode)

    else:
        ref_exp_dict = json_parser(json_location, peak_threshold, debug_mode)

    if 'Reference' not in ref_exp_dict:
        print('No reference spectrum found')
        return None

    reference_hist, spectral_threshold = reference_spectrum(ref_exp_dict['Reference'],
                                                            bins_per_array_dimension)

    processor_budget = (mp.cpu_count() * 6) - 3
    pool = mp.Pool(processes=processor_budget)

    sorted_spectra_keys = sorted(list(ref_exp_dict['Experiment'].keys()))
    spectra_array = [ref_exp_dict['Experiment'][identifier] for identifier in sorted_spectra_keys]

    processed_spectra = np.asarray(
        list(filter(lambda x: x is not None, list(pool.map(spectrum_processing_and_rep_generation,
                                                           [(spectrum, spectral_threshold, bins_per_array_dimension,
                                                             reference_hist) for
                                                            spectrum in spectra_array])))))
    pool.close(), pool.join()

    if debug_mode:
        return processed_spectra, sorted_spectra_keys, id_to_activity_dict
    else:
        return processed_spectra, sorted_spectra_keys


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


def two_dimensional_hist(spectrum, bins_per_array_dimension):
    # try:
    twod_hist = np.histogram2d(spectrum[:, 0], spectrum[:, 1],
                               #                                    range=[[80, 160],
                               #                                    [2, 20]],
                               weights=np.log10(spectrum[:, 2]),
                               normed=False,
                               range=[[95, 135],
                                      [4, 12]],
                               bins=bins_per_array_dimension)[0]

    twod_hist = np.nan_to_num(twod_hist.T)
    return twod_hist


def spectrum_processing_and_rep_generation(args):
    spectrum, spectral_threshold, bins_per_array_dimension, reference_hist = args
    processed_hist = spectrum_process(spectrum, bins_per_array_dimension, spectral_threshold)
    description = comparator_function([processed_hist, reference_hist])
    return description


def comparator_function(args):
    target, reference = args

    ref_ent = shannon_entropy(reference)
    ref_moments = moments_hu(reference)
    ref_hog = hog(reference, block_norm='L2-Hys')
    ref_orb_extractor = ORB()
    ref_orb_extractor.detect(reference)
    ref_orb = ref_orb_extractor.keypoints

    ent_diff = np.abs(ref_ent - shannon_entropy(target))

    target_hog = hog(target, block_norm='L2-Hys')
    desc_dist = euclidean(ref_hog, target_hog)

    target_moments = moments_hu(target)
    moments_dist = euclidean(ref_moments, target_moments)
    n_key = 20

    targ_orb_extractor = ORB()
    targ_orb_extractor.detect(target)
    targ_orb = targ_orb_extractor.keypoints

    orb_dist = cdist(ref_orb, targ_orb, 'euclidean')
    orb_dist = np.sort(np.min(orb_dist, axis=1))[::-1]
    orb_dist_med = np.median(orb_dist)

    ssim_score, ssim_grad = ssim(target, reference, multichannel=True, gradient=True)

    shifts, error, phase_diff = register_translation(target, reference,
                                                     space='real')

    scalars = np.zeros(11 + n_key)
    scalars[0], scalars[1] = ssim_score, 0
    scalars[2] = desc_dist
    scalars[3] = compare_nrmse(reference, target)
    scalars[4] = error
    scalars[5] = phase_diff
    # scalars[5] = shape_diff
    scalars[6] = compare_psnr(reference, target)
    scalars[7] = ent_diff
    scalars[8] = moments_dist
    scalars[9] = np.abs(np.sum(target)-np.sum(reference))
    scalars[10] = orb_dist_med
    scalars[11:] = orb_dist[:n_key]

    results = scalars

    return results


def proba_ranker(proba_set, labels):
    labels = np.asarray(labels)
    sorted_proba_indices = np.argsort(proba_set)[::-1]
    proba_set = proba_set[sorted_proba_indices]
    labels = labels[sorted_proba_indices]

    return proba_set, labels

def nmr_classification(data):

    with open('pickle_jar/fit_transform.pkl', 'rb') as scalerfile:
        scaler = pickle.load(scalerfile)

    with open('pickle_jar/prediction_model.pkl', 'rb') as svmfile:
        nmr_classifier = pickle.load(svmfile)

    with open('pickle_jar/pca_transform.pkl', 'rb') as pcafile:
        pca = pickle.load(pcafile)

    scaled_data = scaler.transform(data)

    scaled_data = pca.transform(scaled_data)

    result = nmr_classifier.predict_proba(scaled_data)[:, 1]
    binarised_result = np.zeros_like(result)
    binarised_result[result > 0.5] = 1

    return scaled_data, result, binarised_result

def json_constructor(probas, labels, dump_location):
    return_obj = []
    for idx, proba in enumerate(probas):
        exp_result = float(proba) >= 0.5,
        return_obj.extend([{'EXP_NUMBER': int(labels[idx]), 'isActive': exp_result,
                            'activePseudoprobability': float(proba)}])
        #return_obj.append([{'EXP_NUMBER': int(labels[idx]), 'isActive': exp_result,
        #                         'activePseudoprobability': float(proba)}])
    with open(dump_location, 'w') as outfile:
        json.dump(return_obj, outfile)

if __name__ == '__main__':
    overall_id_act_dict = {}
    overall_spectra = []
    overall_sorted_keys = []
    overall_labels = []
    overall_true_labels = []

    json_location = sys.argv[1]

    if len(sys.argv) > 2:
        output_location = sys.argv[2]
    else:
        output_location = os.path.dirname(json_location)

    jid = os.path.basename(json_location).replace('.json', '')

    ref_hog = None
    ref_ent = None
    ref_orb = None
    ref_moments = None

    processed_spectra, sorted_keys, id_act_dict = spectra_processor(json_location, 20000, 500, True)

    print('Spectra processed')

    scaled_spectra, probas, predicted_labels = nmr_classification(processed_spectra)
    overall_spectra.extend(np.ndarray.tolist(scaled_spectra))
    overall_labels.extend(np.ndarray.tolist(predicted_labels))

    probas, labels = proba_ranker(probas, sorted_keys)

    json_constructor(probas, labels, '{}/processed_spectra.json'.format(output_location))
    print ('Predictions made, saved to file in {}'.format(output_location))