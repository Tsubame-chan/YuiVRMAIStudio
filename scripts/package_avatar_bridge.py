from pathlib import Path
import json,zipfile,hashlib,shutil
import argparse
parser=argparse.ArgumentParser(description='Create the distributable Yui Avatar Bridge ZIP and VPM index (no avatars).')
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
r=Path(__file__).resolve().parent.parent
pkg=r/'avatar-bridge/YuiAvatarBridge/Packages/jp.tsubamechan.yui-avatar-bridge'
if not pkg.exists():pkg=r/'Packages/jp.tsubamechan.yui-avatar-bridge'
manifest=json.loads((pkg/'package.json').read_text())
files=[p for p in pkg.rglob('*') if p.is_file() and not p.name.startswith('YuiBridgeGateCli') and not p.name.startswith('.')]
out=args.output;out.mkdir(parents=True,exist_ok=True);zipPath=out/(manifest['name']+'-'+manifest['version']+'.zip')
with zipfile.ZipFile(zipPath,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(files):
  info=zipfile.ZipInfo(p.relative_to(pkg).as_posix(),(2026,9,9,0,0,0));info.external_attr=0o100644<<16;info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,p.read_bytes())
sha=hashlib.sha256(zipPath.read_bytes()).hexdigest();zipPath.with_suffix('.zip.sha256').write_text(sha+'  '+zipPath.name+'\n')
manifest=json.loads((pkg/'package.json').read_text());manifest['zipSHA256']=sha
repo=dict(name='Yui Avatar Bridge',id='jp.tsubamechan.yui.vpm',url='https://raw.githubusercontent.com/Tsubame-chan/YuiVRMAIStudio/main/vpm/index.json',author='Tsubame-chan',packages={manifest['name']:dict(versions={manifest['version']:manifest})})
(out/'index.json').write_text(json.dumps(repo,indent=2)+'\n');print(zipPath,sha)
