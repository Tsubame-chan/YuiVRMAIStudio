#!/usr/bin/env python3
"""Extract the pinned official CPU runtime and preserve its license notices."""
import argparse
import hashlib
from pathlib import Path
import tarfile
import zipfile

CORE_SHA = '69377af98bc43ec4449e6925a71153d0918d9dca4e0bf225b3ad2f4eca907331'
ORT_SHA = '32b07e7c6c59030434b54eb0255912510942faf193c7a14e0e1a168db5796265'


def prepare(core, ort, destination):
    for source, expected in [(core, CORE_SHA), (ort, ORT_SHA)]:
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
            raise ValueError('Runtime archive checksum mismatch: ' + str(source))
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(core) as archive:
        prefix = 'voicevox_core-windows-x64-0.16.4/'
        for relative, target in [('lib/voicevox_core.dll', 'voicevox_core.dll'), ('LICENSE', 'VOICEVOX-LICENSE'), ('VERSION', 'VOICEVOX-VERSION')]:
            (destination / target).write_bytes(archive.read(prefix + relative))
    with tarfile.open(ort) as archive:
        prefix = 'voicevox_onnxruntime-win-x64-1.17.3/'
        for relative, target in [('lib/voicevox_onnxruntime.dll', 'voicevox_onnxruntime.dll'), ('TERMS.txt', 'ONNX-TERMS.txt'), ('third-party-notices.html', 'ONNX-third-party-notices.html')]:
            member = archive.getmember(prefix + relative)
            if not member.isfile():
                raise ValueError('Expected regular runtime file: ' + member.name)
            (destination / target).write_bytes(archive.extractfile(member).read())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--core', type=Path, required=True)
    parser.add_argument('--onnx', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.core, args.onnx, args.destination)
