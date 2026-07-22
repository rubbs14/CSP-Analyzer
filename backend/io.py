import csv
import json

import numpy as np


def json_parser(json_file, intensity_threshold, debug_mode=False):
    id_to_peaklist = {"Reference": [], "Experiment": {}}
    id_to_activity = {}

    with open(json_file, "r") as jsp:
        js = json.load(jsp)

    if isinstance(js, dict):
        js = [js]

    for spectrum in js:
        if debug_mode:
            activity = spectrum["UserSelection"]
            activity = 1 if activity == "ACTIVE (MAN)" else 0
            id_to_activity[int(spectrum["EXP_NUMBER"])] = activity

        peak_list_type = spectrum["JSON_Data"]
        peaklist = []
        for peak in spectrum["PEAKLIST"]:
            if float(peak["INTENSITY"]) >= intensity_threshold:
                formatted_peak = [
                    float(v) for k, v in peak.items() if k in ["F1", "F2", "INTENSITY"]
                ]
                peaklist.append(formatted_peak)

        if peaklist:
            peaklist = np.asarray(peaklist)
            if peak_list_type == "Reference":
                id_to_peaklist["Reference"] = peaklist
            else:
                id_to_peaklist["Experiment"][int(spectrum["EXP_NUMBER"])] = peaklist

    if debug_mode:
        return id_to_peaklist, id_to_activity
    return id_to_peaklist


def class_id_dict_reader(class_information_location):
    id_to_activity_dict = {}
    with open(class_information_location) as id_to_act_map:
        csvr = csv.reader(id_to_act_map)
        for row in csvr:
            identifier, activity = row
            activity = int(activity)
            if activity == 2:
                activity = 1
            id_to_activity_dict[int(identifier[:-1])] = activity
    return id_to_activity_dict


def json_constructor(probas, labels, dump_location):
    return_obj = []
    for idx, proba in enumerate(probas):
        exp_result = bool(float(proba) >= 0.5)
        return_obj.append(
            {
                "EXP_NUMBER": int(labels[idx]),
                "isActive": exp_result,
                "activePseudoprobability": float(proba),
            }
        )
    with open(dump_location, "w") as outfile:
        json.dump(return_obj, outfile)
