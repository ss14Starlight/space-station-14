#import github
#import difflib
import os
#import sys
#from github import Github
import subprocess
import json
from time import sleep

def runBuild():
    #run a dotnet build
    #capture the output
    subprocess.check_output(['dotnet', 'build', '/p:ErrorLog=error.json', '--no-incremental'], universal_newlines=True)
    #gather up all the error.json files
    error_files = []
    for root, dirs, files in os.walk('.'):
        for file in files:
            if file == 'error.json':
                error_files.append(os.path.join(root, file))
    #interpret each
    summatedLines = []
    for ef in error_files:
        with open(ef, 'r') as f:
            data = f.read()
            #interpret as json object
            errors = json.loads(data)
            #errors are under runs -> results
            for run in errors.get('runs', []):
                for result in run.get('results', []):
                    message = result.get('message', {})
                    locations = result.get('locations', [])
                    for loc in locations:
                        #get result file
                        resultFIle = loc.get('resultFile', {})
                        uri = resultFIle.get('uri', 'unknown')
                        #print(f"File: {uri}, Message: {message}")
                        summatedLines.append(f"File: {uri}, Message: {message}")

    #delete the error files
    for ef in error_files:
        os.remove(ef)

    return summatedLines

def switchBranch(branchName):
    subprocess.check_output(['git', 'checkout', branchName], universal_newlines=True)

def __main__():
    print("Running builds...")
    print("Switching to stable branch...")
    switchBranch('stable')
    sleep(2)  # wait for branch switch to complete
    print("Running stable build...")
    stablelines = runBuild()
    print("Switching to starlight-dev branch...")
    switchBranch('starlight-dev')
    sleep(2)  # wait for branch switch to complete
    print("Running starlight-dev build...")
    devlines = runBuild()

    print("Comparing results...")
    #compare stablelines and devlines
    stable_set = set(stablelines)
    dev_set = set(devlines)
    diff_output = ""
    only_in_dev = dev_set - stable_set

    #convert to string
    if only_in_dev:
        diff_output += "Warnings only in starlight-dev build:\n"
        for line in only_in_dev:
            diff_output += line + "\n"

    if diff_output:
        print("Differences found between stable and starlight-dev builds:")
        print(diff_output)
        #output to a file
        with open('build_warnings_diff.txt', 'w') as f:
            f.write(diff_output)
            print("Differences written to build_warnings_diff.txt")
    else:
        print("No differences found between stable and starlight-dev builds.")

if __name__ == "__main__":
    __main__()
