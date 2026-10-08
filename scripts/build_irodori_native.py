#!/usr/bin/env python3
"""Build the pinned CoreML SDK with Yui's C ABI; model data is never bundled."""
import argparse, pathlib, shutil, subprocess
p=argparse.ArgumentParser();p.add_argument('--platform',choices=['macos','ios'],default='macos');p.add_argument('--sdk',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1]
work=root/('builds/irodori-unity-native-'+a.platform)
work.mkdir(parents=True,exist_ok=True)
shutil.copytree(a.sdk/'Sources/IrodoriNative',work/'Sources/IrodoriNative',dirs_exist_ok=True)
shutil.copytree(a.sdk/'Sources/IrodoriTTS',work/'Sources/IrodoriTTS',dirs_exist_ok=True)
# SDK v0.2.0 compiles AudioSeal into disposable URLs on every engine load.
# Apply the small Yui-only adaptation to the build copy, never to the SDK checkout.
watermark=work/'Sources/IrodoriTTS/AudioWatermark.swift'
source=watermark.read_text()
old='        let compiled = try MLModel.compileModel(at: package)\n        let config = MLModelConfiguration()\n        config.computeUnits = .cpuAndGPU\n        do { return (try MLModel(contentsOf: compiled, configuration: config), compiled) }\n        catch { try? FileManager.default.removeItem(at: compiled); throw error }'
new='        let config = MLModelConfiguration()\n        config.computeUnits = .cpuAndGPU\n        return try YuiCoreMLModelCache.load(package: package, configuration: config)'
if old not in source: raise RuntimeError('Unsupported SDK AudioWatermark source; review cache patch before building.')
source=source.replace(old,new).replace('deinit { for url in compiledURLs { try? FileManager.default.removeItem(at: url) } }','// Compiled models are persistent cache entries; retain them across engine release.')
watermark.write_text(source)
shutil.copy2(root/'scripts/native/irodori/YuiCoreMLModelCache.swift',work/'Sources/IrodoriTTS/YuiCoreMLModelCache.swift')
(work/'Sources/YuiIrodoriBridge').mkdir(parents=True,exist_ok=True)
shutil.copy2(root/'scripts/native/irodori/YuiIrodoriBridge.swift',work/'Sources/YuiIrodoriBridge/Bridge.swift')
(work/'Package.swift').write_text('''// swift-tools-version: 5.9
import PackageDescription
let package = Package(name: "YuiIrodoriBridge", platforms: [.macOS(.v14), .iOS(.v17)], products: [.library(name:"YuiIrodoriBridge",type:.dynamic,targets:["YuiIrodoriBridge"])], targets:[
.target(name:"IrodoriNative",publicHeadersPath:"include",cxxSettings:[.define("IRODORI_COREML_ONLY",to:"1")],linkerSettings:[.linkedFramework("Foundation"),.linkedFramework("CoreML"),.linkedFramework("Accelerate")]),
.target(name:"IrodoriTTS",dependencies:["IrodoriNative"],linkerSettings:[.linkedFramework("AVFoundation")]),
.target(name:"YuiIrodoriBridge",dependencies:["IrodoriTTS"])], cxxLanguageStandard:.cxx17)
''')
package=work/'Package.swift'
if a.platform=='ios':
    package.write_text(package.read_text().replace('type:.dynamic','type:.static'))
command=['swift','build','-c','release','--product','YuiIrodoriBridge']
if a.platform=='ios':
    sdk=subprocess.check_output(['xcrun','--sdk','iphoneos','--show-sdk-path'],text=True).strip()
    command += ['--sdk',sdk,'--triple','arm64-apple-ios18.0']
subprocess.run(command,cwd=work,check=True)
a.output.parent.mkdir(parents=True,exist_ok=True)
binary_name='libYuiIrodoriBridge.a' if a.platform=='ios' else 'libYuiIrodoriBridge.dylib'
candidates=[p for p in (work/'.build').rglob(binary_name) if not any(part.endswith('.dSYM') for part in p.parts)]
if not candidates: raise RuntimeError('Swift build produced no '+binary_name)
shutil.copy2(max(candidates,key=lambda p:p.stat().st_mtime),a.output)
if a.platform=='macos':
    subprocess.run(['install_name_tool','-id','@rpath/libYuiIrodoriBridge.dylib',str(a.output)],check=True)
    subprocess.run(['codesign','--force','--sign','-',str(a.output)],check=True)
