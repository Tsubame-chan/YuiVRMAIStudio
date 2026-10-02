#!/usr/bin/env python3
"""Stop only a process started by this installation, with PID reuse protection."""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import re
import signal
import calendar
import subprocess
import sys
import time


def fingerprint(pid):
    try:
        if os.name == 'nt':
            result = subprocess.run(['powershell.exe','-NoProfile','-Command',
                f'$p=Get-Process -Id {int(pid)} -ErrorAction Stop; $p.StartTime.ToUniversalTime().Ticks'],capture_output=True,text=True,timeout=5)
        else:
            result = subprocess.run(['/bin/ps','-p',str(int(pid)),'-o','lstart='],capture_output=True,text=True,timeout=5,env={**os.environ,'LC_ALL':'C','TZ':'UTC'})
        return result.stdout.strip() if result.returncode == 0 else ''
    except (OSError, subprocess.SubprocessError): return ''


def process_parents():
    if os.name == 'nt':
        result = subprocess.run(['powershell.exe','-NoProfile','-Command',
            'Get-CimInstance Win32_Process | ForEach-Object { "{0} {1}" -f $_.ProcessId,$_.ParentProcessId }'],capture_output=True,text=True,timeout=15)
    else:
        result = subprocess.run(['/bin/ps','-axo','pid=,ppid='],capture_output=True,text=True,timeout=5)
    return {int(parts[0]):int(parts[1]) for line in result.stdout.splitlines() if len(parts:=line.split())==2 and all(p.isdigit() for p in parts)}

def birth_seconds(pid):
    value=fingerprint(pid)
    if not value:return ''
    if os.name=='nt':return str((int(value)-621355968000000000)//10000000)
    return str(calendar.timegm(time.strptime(value,'%a %b %d %H:%M:%S %Y')))


def record(directory, name, pid):
    stamp = fingerprint(pid)
    if not stamp: raise RuntimeError('Started process is no longer running; ownership was not recorded.')
    directory.mkdir(parents=True,exist_ok=True)
    path=directory/(name+'.json');temp=path.with_suffix('.tmp')
    try:
        with temp.open('w') as output:
            if os.name != 'nt':os.chmod(temp,0o600)
            json.dump({'pid':pid,'start':stamp},output);output.flush();os.fsync(output.fileno())
        os.replace(temp,path)
    finally:temp.unlink(missing_ok=True)


def forget_if_unchanged(path, data):
    if path.exists() and json.loads(path.read_text()) == data:
        path.unlink(missing_ok=True)


def stop(directory, name):
    path=directory/(name+'.json')
    if not path.exists():return 'No process owned by this installation.'
    data=json.loads(path.read_text());pid=int(data['pid'])
    if fingerprint(pid)!=data['start']:
        forget_if_unchanged(path,data);return 'Recorded process already stopped. Other processes were retained.'
    parents=process_parents();owned={pid};changed=True
    while changed:
        next_owned=owned|{child for child,parent in parents.items() if parent in owned and fingerprint(child)}
        changed=next_owned!=owned;owned=next_owned
    identities={p:fingerprint(p) for p in owned}
    # Verify the root again after collection; do not adopt a recycled PID's children.
    if fingerprint(pid)!=data['start']:return 'Process identity changed; no stop signal sent.'
    for p in sorted(owned,key=lambda p:p==pid):
        if identities[p] and fingerprint(p)==identities[p]:
            try:os.kill(p,signal.SIGTERM)
            except ProcessLookupError:pass
    deadline=time.monotonic()+5
    while time.monotonic()<deadline and any(identities[p] and fingerprint(p)==identities[p] for p in owned):time.sleep(.1)
    for p in owned:
        if identities[p] and fingerprint(p)==identities[p]:
            try:os.kill(p,getattr(signal,'SIGKILL',signal.SIGTERM))
            except ProcessLookupError:pass
    forget_if_unchanged(path,data)
    return 'Stopped processes owned by this installation.'


def main():
    parser=argparse.ArgumentParser();parser.add_argument('operation',choices=['record','stop','stop-all','birth','stop-ledger']);parser.add_argument('--directory',type=Path);parser.add_argument('--name');parser.add_argument('--pid',type=int);parser.add_argument('--file',type=Path)
    args=parser.parse_args()
    if args.operation=='birth':
        if not args.pid or args.pid<1:parser.error('Process ID required.')
        print(birth_seconds(args.pid));return
    if not args.directory:parser.error('Ownership directory required.')
    if args.operation in {'record','stop'} and (not args.name or not re.fullmatch(r'(backend|voicevox|aivis|irodori)-[0-9]{1,5}',args.name)):parser.error('Service name and port required.')
    if args.operation=='stop-ledger':
        if not args.file or not args.file.exists():return
        authorized=set()
        for line in args.file.read_text().splitlines():
            parts=line.split()
            if len(parts)==3 and parts[1].isdigit() and parts[2].isdigit() and birth_seconds(int(parts[1]))==parts[2]:authorized.add(int(parts[1]))
        for path in args.directory.glob('*.json'):
            if re.fullmatch(r'(backend|voicevox|aivis|irodori)-[0-9]{1,5}',path.stem) and int(json.loads(path.read_text())['pid']) in authorized:print(stop(args.directory,path.stem))
        return
    if args.operation=='record':
        if not args.pid or args.pid<1:parser.error('Process ID required.')
        record(args.directory,args.name,args.pid)
    elif args.operation=='stop':print(stop(args.directory,args.name))
    elif args.directory.exists():
        for path in args.directory.glob('*.json'):
            if re.fullmatch(r'(backend|voicevox|aivis|irodori)-[0-9]{1,5}',path.stem):print(path.stem+': '+stop(args.directory,path.stem))


if __name__=='__main__':main()
