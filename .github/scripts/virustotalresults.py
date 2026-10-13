#!/usr/bin/env python3
import argparse
from yaml import load as yaml_load
try:
    from yaml import CLoader as YAMLLoader
except ImportError:
    from yaml import Loader as YAMLLoader

parser = argparse.ArgumentParser()
parser.add_argument('vt_yml')

args = parser.parse_args()

with open(args.vt_yml, 'r') as source:
    results = yaml_load(source, Loader=YAMLLoader)

    VIRUSTOTAL_URL_FORMAT = 'https://www.virustotal.com/gui/file/{}'
    for file in results:
        name = file['meaningful_name']
        print('{}: {}'.format(name, VIRUSTOTAL_URL_FORMAT.format(file['_id'])))
        if file['last_analysis_stats']['malicious'] > 0:
            print('::error file={}::Reported malicious!'.format(name))
        if file['last_analysis_stats']['suspicious'] > 0:
            print('::warn file={}::Reported malicious!'.format(name))