#!/usr/bin/env python3
import yaml
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('vt_yml')

args = parser.parse_args()

results = yaml.load(open(args.vt_yml, 'r'))

VIRUSTOTAL_URL_FORMAT = 'https://www.virustotal.com/gui/file/{}'
for file in results:
    name = file['meaningful_name']
    print('{}: {}'.format(name, VIRUSTOTAL_URL_FORMAT.format(file['_id'])))
    if file['last_analysis_stats']['malicious'] > 0:
        print('::error file={}::Reported malicious!'.format(name))
    if file['last_analysis_stats']['suspicious'] > 0:
        print('::warn file={}::Reported malicious!'.format(name))