#!/usr/bin/env python3
"""Build only the original Kokoro wrapper; ONNX Runtime is reused from existing plugins."""
import argparse
from pathlib import Path
import subprocess
p=argparse.ArgumentParser();p.add_argument('--platform',choices=['macos','android'],required=True);p.add_argument('--ndk',type=Path);args=p.parse_args()
root=Path(__file__).resolve().parents[1];source=root/'unity/Assets/Plugins/iOS/YuiKokoroBridge.cpp'
if args.platform=='macos':
 output=root/'unity/Assets/Plugins/macOS/libYuiKokoroBridge.dylib'
 cmd=['xcrun','--sdk','macosx','clang++','-std=c++17','-O2','-dynamiclib','-fvisibility=hidden','-arch','arm64','-arch','x86_64','-mmacosx-version-min=11.0',str(source),'-o',str(output)]
else:
 if not args.ndk:p.error('--ndk is required')
 compiler=args.ndk/'toolchains/llvm/prebuilt/darwin-x86_64/bin/aarch64-linux-android26-clang++'
 output=root/'unity/Assets/Plugins/Android/Kokoro/arm64-v8a/libYuiKokoroBridge.so'
 cmd=[str(compiler),'-std=c++17','-O2','-shared','-fPIC','-fvisibility=hidden','-Wl,-z,max-page-size=16384','-static-libstdc++',str(source),'-ldl','-o',str(output)]
output.parent.mkdir(parents=True,exist_ok=True);subprocess.run(cmd,check=True);print(output)
