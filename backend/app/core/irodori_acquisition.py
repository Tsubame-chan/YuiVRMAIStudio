"""OS-specific provisioning for the shared acquisition manager."""
from __future__ import annotations

import asyncio
import io
import json
import os
import platform
import shutil
import socket
import sys
import time
import wave
from pathlib import Path

import httpx

from app.core.asset_downloads import AcquisitionError, check_cancel, extract_verified_zip, safe_relative, verified
from app.core.config import get_settings
from app.core.irodori_presets import VOICES, ROOT as REFERENCE_ROOT

RUNTIME_ASSETS = Path(__file__).resolve().parents[1] / 'assets/irodori-runtime'


def macos_venv_command(env_dir):
    # Portable pyvenv.cfg can report a relative base executable. Resolve the
    # actual executable before venv chooses its links. Only incomplete managed
    # environments are cleared; models and settings live outside this folder.
    create_env = (
        "import os,sys,venv;"
        "sys._base_executable=os.path.realpath(sys.executable);"
        "venv.EnvBuilder(with_pip=True,symlinks=True,clear=True).create(sys.argv[1])"
    )
    return [sys.executable, '-c', create_env, env_dir]


class IrodoriAdapter:
    async def unsupported(self, asset):
        target = asset['platform']
        if target == 'macos-arm64':
            if platform.system() != 'Darwin' or platform.machine() != 'arm64': return 'この追加音声はApple Silicon Mac向けです。'
            if sys.version_info[:2] != (3,12): return 'このMLX音声にはPython 3.12のBackendが必要です。Backendを更新してください。'
            return ''
        if target == 'windows-nvidia':
            if platform.system() != 'Windows' or platform.machine().lower() not in {'amd64','x86_64'}: return 'この追加音声はWindowsのNVIDIA GPU向けです。'
            if not shutil.which('git.exe'): return 'Git for Windowsを導入すると、この画面から追加音声を取得できます。'
            if not shutil.which('nvidia-smi.exe'): return 'NVIDIA GPUと対応ドライバが必要です。標準のVOICEVOXは引き続き利用できます。'
            try:
                p = await asyncio.create_subprocess_exec('nvidia-smi.exe', '--query-gpu=driver_version', '--format=csv,noheader', stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
                try: out, _ = await asyncio.wait_for(p.communicate(), timeout=5)
                except asyncio.TimeoutError:
                    p.kill(); await p.wait(); return 'NVIDIAドライバに応答がありません。GPUの状態を確認してください。'
                if p.returncode or not out.strip(): return 'NVIDIA GPUを利用できません。ドライバを確認してください。'
                if all(int(line.split(b'.')[0]) < 570 for line in out.splitlines() if line.strip()): return 'CUDA 12.8に対応するNVIDIAドライバへ更新してください（570以降）。'
            except (OSError, ValueError): return 'NVIDIA GPUを確認できません。ドライバを確認してください。'
            return ''
        return 'このPCに対応する追加音声はありません。'

    async def install(self, manager, asset, dest, files, cancel):
        # An incomplete dependency setup is retained for retry, but never marked
        # installed. Model copies are hard links to the verified cache on this disk.
        dest.mkdir(parents=True, exist_ok=True)
        if asset['adapter'] == 'irodori-mlx':
            model = dest / 'model'
            for spec, source in files:
                check_cancel(cancel)
                target = model.joinpath(*safe_relative(spec['path']).parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.is_symlink(): raise AcquisitionError('追加音声の保存先を確認してください。')
                if target.exists(): target.unlink()
                os.link(source, target)
            env_dir = dest / '.venv'
            py = env_dir / 'bin/python'
            if not py.is_file():
                await manager.subprocess(asset['id'], macos_venv_command(env_dir), cwd=dest)
            await manager.subprocess(asset['id'], [py, '-m', 'pip', '--isolated', 'install', '--only-binary=:all:', '--no-cache-dir', '--disable-pip-version-check', '--index-url', 'https://pypi.org/simple', '-r', RUNTIME_ASSETS / 'requirements-macos.txt'], cwd=dest)
            # Keep the downloaded config immutable. Quality adjustments are kept
            # in a generated model profile, leaving fixed catalog checks valid.
            profile = dest / 'model-40steps'
            profile.mkdir(exist_ok=True)
            for spec, source in files:
                if spec['path'] == 'config.json': continue
                target = profile.joinpath(*safe_relative(spec['path']).parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.exists(): target.unlink()
                os.link(source, target)
            config = json.loads((model / 'config.json').read_text(encoding='utf-8'))
            config.setdefault('sampler', {})['num_steps'] = 40
            from app.core.asset_downloads import atomic_json
            atomic_json(profile / 'config.json', config)
        elif asset['adapter'] == 'irodori-cuda':
            pack = dest / asset['archive_root']
            # Reuse an intact archive extraction, preserving its completed venv.
            intact = await asyncio.to_thread(self.package_intact, pack, cancel, asset['package_manifest_sha256']) if pack.exists() else False
            if not intact:
                unpack = dest / 'extracting'
                if unpack.exists(): await asyncio.to_thread(shutil.rmtree, unpack)
                unpack.mkdir()
                await asyncio.to_thread(extract_verified_zip, files[0][1], unpack, asset['maximum_expanded_bytes'], cancel)
                if not (unpack / asset['archive_root']).is_dir(): raise AcquisitionError('配布パッケージの構成を確認できません。')
                if pack.exists(): await asyncio.to_thread(shutil.rmtree, pack)
                os.replace(unpack / asset['archive_root'], pack)
                await asyncio.to_thread(shutil.rmtree, unpack)
            await manager.subprocess(asset['id'], ['powershell.exe','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',RUNTIME_ASSETS / 'setup_irodori_v4_windows.ps1','-PackRoot',pack,'-DependenciesOnly'], cwd=dest,
                                     env={'UV_PYTHON_INSTALL_DIR':str(dest/'python'), 'UV_CACHE_DIR':str(dest/'uv-cache')})
        else: raise AcquisitionError('未対応の導入形式です。')

    @staticmethod
    def package_intact(pack, cancel, expected_manifest_sha):
        manifest = pack / 'files.json'
        if not manifest.is_file() or manifest.is_symlink(): return False
        try:
            if manifest.stat().st_size > 256 * 1024: return False
            import hashlib
            if hashlib.sha256(manifest.read_bytes()).hexdigest() != expected_manifest_sha: return False
            for spec in json.loads(manifest.read_text(encoding='utf-8'))['files']:
                path = pack.joinpath(*safe_relative(spec['path']).parts)
                # The archive has an authenticated outer SHA. A local manifest
                # is not a fresh trust root; used only to detect retry corruption.
                if not verified(path, {'bytes':path.stat().st_size, 'sha256':spec['sha256']}, cancel): return False
            return True
        except (KeyError, ValueError, OSError): return False

    async def validate_installed(self, manager, asset, dest, cancel):
        if asset['adapter'] == 'irodori-mlx':
            for spec in asset['files']:
                if not await asyncio.to_thread(verified, dest / 'model' / spec['path'], spec, cancel) or (spec['path'] != 'config.json' and not await asyncio.to_thread(verified, dest / 'model-40steps' / spec['path'], spec, cancel)):
                    raise AcquisitionError('導入済みモデルの検証に失敗しました。取得済みデータから再取得してください。')
            config = json.loads((dest/'model/config.json').read_text(encoding='utf-8'))
            config.setdefault('sampler', {})['num_steps'] = 40
            from app.core.asset_downloads import atomic_json
            atomic_json(dest/'model-40steps/config.json', config)
        else:
            if not await asyncio.to_thread(self.package_intact, dest / asset['archive_root'], cancel, asset['package_manifest_sha256']):
                raise AcquisitionError('導入済みパッケージの検証に失敗しました。再取得してください。')

    def endpoint(self, asset, dest):
        server = asset['adapter'] == 'irodori-cuda'
        return {'id':'managed-'+asset['id'], 'name':'Irodori V4.1 · このPC', 'provider_type':'http', 'settings':{
            'http_tts_base_url':f'http://127.0.0.1:{asset["port"]}',
            'http_tts_provider_id':'irodori-server' if server else 'irodori-mlx',
            'http_tts_endpoint':'/v1/audio/speech', 'http_tts_health_endpoint':'/health' if server else '/v1/models',
            'http_tts_payload_format':'irodori_openai_speech' if server else 'openai_speech',
            'http_tts_model':'irodori-tts' if server else str(dest / 'model-40steps'),
            'http_tts_voice':'bright_natural' if server else 'none', 'http_tts_format':'wav',
            'http_tts_audio_processor':'none', 'http_tts_irodori_num_steps':40,
            'http_tts_irodori_chunking_enabled':True, 'http_tts_irodori_chunk_min_chars':1,
            'http_tts_irodori_first_sentence_chunk_min_chars':1}}

    async def prepare(self, manager, asset, dest, cancel):
        reason = await self.unsupported(asset)
        if reason: raise AcquisitionError(reason)
        await self.validate_installed(manager, asset, dest, cancel)
        await manager.stop_runtime(asset['id'])
        with socket.socket() as s:
            try: s.bind(('127.0.0.1', asset['port']))
            except OSError: raise AcquisitionError('追加音声のポートが別のソフトで使用中です。そのソフトを終了して再試行してください。')
        if asset['adapter'] == 'irodori-mlx':
            args = [dest/'.venv/bin/python','-m','mlx_audio.server','--host','127.0.0.1','--port',str(asset['port'])]
        else:
            args = ['powershell.exe','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',RUNTIME_ASSETS/'start_irodori_v4_windows.ps1','-PackRoot',dest/asset['archive_root'],'-Port',str(asset['port'])]
        proc = await manager.subprocess(asset['id'], args, cwd=dest, runtime=True,
                                      env={'HF_HUB_OFFLINE':'1', 'HF_HUB_DISABLE_TELEMETRY':'1'} if asset['adapter']=='irodori-mlx' else {})
        e = self.endpoint(asset,dest); base = e['settings']['http_tts_base_url']
        async def ready():
            async with httpx.AsyncClient(timeout=httpx.Timeout(180,connect=3),trust_env=False) as client:
                deadline = time.monotonic()+120
                while time.monotonic() < deadline:
                    check_cancel(cancel)
                    if proc.returncode is not None: raise AcquisitionError('音声エンジンを起動できませんでした。実行環境を確認して再試行してください。')
                    try:
                        r = await client.get(base+e['settings']['http_tts_health_endpoint'])
                        if r.is_success: break
                    except httpx.HTTPError: pass
                    await asyncio.sleep(.5)
                else: raise AcquisitionError('音声エンジンの起動を確認できませんでした。再試行してください。')
                # Verify actual synthesis and valid PCM, beyond health/port readiness.
                for voice in VOICES:
                    check_cancel(cancel)
                    payload = {'model':e['settings']['http_tts_model'],'input':'こんにちは。声の準備ができました。','response_format':'wav', 'voice':voice['id'] if asset['adapter']=='irodori-cuda' else 'none'}
                    if asset['adapter']=='irodori-cuda': payload['irodori']={'num_steps':40,'chunking_enabled':True,'chunk_min_chars':1,'first_sentence_chunk_min_chars':1}
                    else: payload.update(ref_audio=str(REFERENCE_ROOT/(voice['id']+'.wav')),ref_text=voice['reference_text'],instruct=voice['caption'],gender='female',lang_code='ja')
                    try:
                        r = await client.post(base+'/v1/audio/speech', json=payload)
                        r.raise_for_status()
                    except httpx.HTTPError:
                        raise AcquisitionError('音声エンジンで音声を生成できませんでした。データを再検証・修復して再試行してください。')
                    try:
                        with wave.open(io.BytesIO(r.content)) as wav:
                            if wav.getframerate() != 48000 or wav.getnframes() < 48000 or wav.getnframes() > 48000*60:
                                raise ValueError('invalid generated audio')
                    except (wave.Error, EOFError, ValueError): raise AcquisitionError('音声の生成結果を確認できませんでした。再試行してください。')
        await manager.cancellable(asset['id'], ready())

    def register(self, asset, dest):
        from app.core.voice_library import LOCK, load, save, LibraryUpdate
        from app.core.irodori_presets import profiles
        settings=get_settings()
        endpoint=self.endpoint(asset,dest)
        presets=profiles(settings.model_copy(update=endpoint['settings']))
        for p in presets:
            p['id']='managed-'+asset['id']+'-'+p['id'].removeprefix('yui-irodori-')
            p['endpoint_id']=endpoint['id']
        with LOCK:
            data, revision=load(settings)
            # Existing managed entries may have been customized. Never replace
            # edits or secrets and never change the selected default provider.
            if not any(e['id']==endpoint['id'] for e in data['endpoints']): data['endpoints'].append(endpoint)
            existing={p['id'] for p in data['profiles']}
            data['profiles'].extend(p for p in presets if p['id'] not in existing)
            save(LibraryUpdate(revision=revision, **data), settings)
