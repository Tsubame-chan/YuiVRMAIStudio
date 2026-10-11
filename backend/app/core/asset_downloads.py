"""Fixed-catalog, non-blocking acquisition jobs. No request-supplied URL or command.

Verified files are cached for retry; installation and runtime readiness are separate.
Only processes started by this manager may be stopped. A per-home OS lock prevents
multiple Backend workers from installing/running the same managed runtime.
"""
from __future__ import annotations

import asyncio
import hashlib
import json
import os
import platform
import shutil
import stat
import subprocess
import tempfile
import threading
import time
import zipfile
from pathlib import Path, PurePosixPath

import httpx

from app.core.admin_settings import settings_path

CATALOG_PATH = Path(__file__).resolve().parents[1] / 'assets/asset-catalog.json'
ACTIVE = {'queued', 'downloading', 'verifying', 'installing', 'preparing', 'cancelling'}


class AcquisitionError(Exception):
    """Public, actionable error; never expose dependency output/credentials."""


class AcquisitionCancelled(Exception):
    pass


def atomic_json(path: Path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temp = tempfile.mkstemp(prefix='.acquisition-', dir=path.parent)
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as f:
            os.chmod(temp, 0o600)
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.flush()
            os.fsync(f.fileno())
        os.replace(temp, path)
    finally:
        if os.path.exists(temp): os.unlink(temp)


def check_cancel(cancel):
    if cancel.is_set(): raise AcquisitionCancelled()


def verified(path: Path, spec: dict, cancel=None):
    if path.is_symlink() or not path.is_file() or path.stat().st_size != spec['bytes']: return False
    digest = hashlib.sha256()
    with path.open('rb') as f:
        while chunk := f.read(1024 * 1024):
            if cancel is not None: check_cancel(cancel)
            digest.update(chunk)
    return digest.hexdigest() == spec['sha256']


def safe_relative(name: str):
    p = PurePosixPath(name)
    reserved = {'CON','PRN','AUX','NUL',*[f'COM{i}' for i in range(1,10)],*[f'LPT{i}' for i in range(1,10)]}
    if (not name or '\\' in name or ':' in name or p.is_absolute() or '..' in p.parts
            or any(part.endswith((' ', '.')) or part.split('.')[0].upper() in reserved for part in p.parts)):
        raise AcquisitionError('配布ファイルの構造が不正です。取得を中止しました。')
    return p


def extract_verified_zip(archive: Path, destination: Path, maximum: int, cancel):
    """Never extract links, devices, traversal, ADS or case-colliding paths."""
    with zipfile.ZipFile(archive) as z:
        infos = z.infolist()
        if len(infos) > 10000 or sum(f.file_size for f in infos) > maximum:
            raise AcquisitionError('展開サイズが上限を超えています。')
        seen = set()
        for f in infos:
            check_cancel(cancel)
            rel = safe_relative(f.filename.rstrip('/'))
            if str(rel).casefold() in seen: raise AcquisitionError('配布ファイルのパスが重複しています。')
            seen.add(str(rel).casefold())
            kind = stat.S_IFMT(f.external_attr >> 16)
            if kind not in (0, stat.S_IFREG, stat.S_IFDIR): raise AcquisitionError('配布ファイルに未対応のリンクがあります。')
            path = destination.joinpath(*rel.parts)
            if f.is_dir(): path.mkdir(parents=True, exist_ok=True); continue
            path.parent.mkdir(parents=True, exist_ok=True)
            with z.open(f) as src, path.open('xb') as out:
                while chunk := src.read(1024 * 1024):
                    check_cancel(cancel)
                    out.write(chunk)


class AssetManager:
    def __init__(self, home=None, catalog=None, adapter=None):
        self.home = Path(home or os.environ.get('YUI_ASSET_HOME', settings_path().parent / 'addons'))
        self.catalog = catalog or json.loads(CATALOG_PATH.read_text(encoding='utf-8'))['assets']
        self.assets = {a['id']: a for a in self.catalog}
        self.jobs = {}
        self.tasks = {}
        self.cancels = {}
        self.processes = {}
        self.lock = asyncio.Lock()
        self.lock_file = None
        self.closed = False
        if adapter is None:
            from app.core.irodori_acquisition import IrodoriAdapter
            adapter = IrodoriAdapter()
        self.adapter = adapter
        self.blocked = ''

    def persist(self):
        atomic_json(self.home / 'state.json', {'schema':1, 'jobs':self.jobs})

    def acquire_owner(self):
        self.home.mkdir(parents=True, exist_ok=True)
        if self.home.is_symlink(): raise AcquisitionError('追加データの保存先を確認してください。')
        f = (self.home / 'owner.lock').open('a+b')
        try:
            if os.name == 'nt':
                import msvcrt
                if f.tell() == 0: f.write(b'0'); f.flush()
                f.seek(0); msvcrt.locking(f.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(f.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            f.close()
            raise AcquisitionError('別のBackendが追加データを管理しています。そのBackendの画面で操作してください。')
        self.lock_file = f

    async def restore(self):
        if not (self.home / 'state.json').exists(): return
        try:
            self.acquire_owner()
            raw = (self.home / 'state.json').read_bytes()
            if len(raw) > 128 * 1024: raise ValueError('state size')
            saved = json.loads(raw)
            if saved.get('schema') != 1: raise ValueError('state schema')
            for aid, j in saved.get('jobs', {}).items():
                if aid not in self.assets or not isinstance(j, dict): continue
                # Recover only our public fields; never trust stored runtime paths/commands.
                self.jobs[aid] = {k:v for k,v in j.items() if k in {'asset_id','phase','detail','downloaded_bytes','total_bytes','updated_at','installed','version'}}
                self.jobs[aid].update(asset_id=aid)
                if j.get('phase') in ACTIVE:
                    self.update(aid, phase='failed', detail='Backendの再起動で処理が中断しました。再試行できます。')
                elif j.get('installed') and j.get('version') == self.assets[aid]['version']:
                    # Runtime ownership cannot survive a process exit. Start and check
                    # our installed runtime asynchronously; never adopt an unknown PID.
                    self.launch(aid, restart=True)
        except AcquisitionError as exc:
            self.blocked = str(exc)
        except (OSError, ValueError, TypeError):
            self.blocked = '追加データの状態ファイルを読み込めません。保存先を確認してください。'

    def update(self, aid, **values):
        self.jobs.setdefault(aid, {}).update(values, updated_at=time.time())
        self.persist()

    def snapshot(self):
        for aid, process in list(self.processes.items()):
            if process.returncode is not None and self.jobs.get(aid, {}).get('phase') == 'ready':
                self.update(aid, phase='failed', detail='音声エンジンが停止しました。「準備を再試行」で再起動できます。')
        return {'jobs':list(self.jobs.values()), 'blocked':self.blocked}

    async def available(self):
        entries = []
        for a in self.catalog:
            reason = self.blocked or await self.adapter.unsupported(a)
            free = shutil.disk_usage(self.home if self.home.exists() else self.home.parent).free
            entries.append({k:a[k] for k in ('id','name','version','platform','source_url','license_url','license_notice')} |
                           {'download_bytes':sum(f['bytes'] for f in a['files']), 'minimum_free_bytes':a['minimum_free_bytes'],
                            'free_bytes':free,'supported':not reason,'reason':reason})
        return {'assets':entries, 'host_platform':platform.system(), **self.snapshot()}

    def launch(self, aid, restart=False):
        a = self.assets[aid]
        self.cancels[aid] = threading.Event()
        self.jobs[aid] = {'asset_id':aid,'phase':'queued','detail':'準備しています。','downloaded_bytes':0,
                          'total_bytes':sum(f['bytes'] for f in a['files']), 'installed':restart,'version':a['version'],'updated_at':time.time()}
        self.persist()
        self.tasks[aid] = asyncio.create_task(self.execute(aid, restart))

    async def start(self, aid, repair=False):
        async with self.lock:
            if aid not in self.assets: raise KeyError(aid)
            if self.closed: raise AcquisitionError('Backendが終了処理中です。')
            if self.blocked: raise AcquisitionError(self.blocked)
            if not self.lock_file: self.acquire_owner()
            prior = self.tasks.get(aid)
            if prior and not prior.done(): return self.jobs[aid]
            self.snapshot()
            if self.jobs.get(aid, {}).get('phase') == 'ready' and not repair: return self.jobs[aid]
            # Avoid multiple dependency installers competing for storage/GPU.
            if any(not t.done() for t in self.tasks.values()): raise AcquisitionError('別の追加データを準備しています。完了後に開始してください。')
            a = self.assets[aid]
            reason = await self.adapter.unsupported(a)
            if reason: raise AcquisitionError(reason)
            installed = not repair and self.jobs.get(aid, {}).get('installed', False) and (self.install_dir(a) / 'installed.json').is_file()
            reserved = sum(min(f['bytes'], max((self.home/'cache'/f['sha256']).stat().st_size if (self.home/'cache'/f['sha256']).is_file() else 0,
                                               (self.home/'cache'/(f['sha256']+'.partial')).stat().st_size if (self.home/'cache'/(f['sha256']+'.partial')).is_file() else 0)) for f in a['files'])
            if not installed and shutil.disk_usage(self.home).free < max(0, a['minimum_free_bytes'] - reserved):
                raise AcquisitionError('追加データと依存ソフトのための空き容量が不足しています。容量を確保して再試行してください。')
            self.launch(aid, restart=installed)
            return self.jobs[aid]

    def install_dir(self, asset):
        return self.home / 'installed' / asset['id'] / asset['version']

    async def cancel(self, aid):
        async with self.lock:
            if aid not in self.jobs: raise KeyError(aid)
            if self.tasks.get(aid) and not self.tasks[aid].done():
                self.cancels[aid].set()
                self.update(aid, phase='cancelling', detail='処理を停止しています。取得済みデータは再利用できます。')
            return self.jobs[aid]

    async def cancellable(self, aid, awaitable):
        task = asyncio.create_task(awaitable)
        try:
            while not task.done():
                check_cancel(self.cancels[aid])
                await asyncio.wait({task}, timeout=.25)
            return await task
        finally:
            if not task.done():
                task.cancel()
                await asyncio.gather(task, return_exceptions=True)

    async def download(self, aid, spec, completed):
        cancel = self.cancels[aid]
        target = self.home / 'cache' / spec['sha256']
        target.parent.mkdir(parents=True, exist_ok=True)
        self.update(aid, phase='verifying', detail='取得済みファイルを確認しています。')
        if await asyncio.to_thread(verified, target, spec, cancel):
            self.update(aid, downloaded_bytes=completed + spec['bytes']); return target
        partial = target.with_suffix('.partial')
        if partial.is_symlink(): raise AcquisitionError('取得先に未対応のリンクがあります。')
        offset = partial.stat().st_size if partial.exists() else 0
        if offset >= spec['bytes']: partial.unlink(); offset = 0
        self.update(aid, phase='downloading', detail='追加音声をダウンロードしています。', downloaded_bytes=completed+offset)
        async def transfer():
            nonlocal offset
            async with httpx.AsyncClient(follow_redirects=True, timeout=httpx.Timeout(30, connect=10), trust_env=False) as client:
                async with client.stream('GET', spec['url'], headers={'Accept-Encoding':'identity', **({'Range':f'bytes={offset}-'} if offset else {})}) as response:
                    response.raise_for_status()
                    if response.status_code == 206:
                        if not offset or response.headers.get('Content-Range') != f'bytes {offset}-{spec["bytes"]-1}/{spec["bytes"]}':
                            raise AcquisitionError('取得元の再開情報が一致しません。再試行してください。')
                    elif response.status_code == 200:
                        offset = 0
                    else: raise AcquisitionError('取得元から正しいファイルを受信できませんでした。')
                    length = response.headers.get('Content-Length')
                    if length and int(length) != spec['bytes'] - offset:
                        raise AcquisitionError('取得元のファイルサイズが一致しません。')
                    last = 0
                    with partial.open('ab' if offset else 'wb') as out:
                        async for chunk in response.aiter_bytes(1024 * 256):
                            check_cancel(cancel)
                            offset += len(chunk)
                            if offset > spec['bytes']: raise AcquisitionError('取得サイズが上限を超えました。')
                            out.write(chunk)
                            if time.monotonic() - last > .4:
                                self.update(aid, downloaded_bytes=completed+offset); last=time.monotonic()
        await self.cancellable(aid, transfer())
        check_cancel(cancel)
        self.update(aid, phase='verifying', detail='ファイルの完全性を確認しています。')
        if not await asyncio.to_thread(verified, partial, spec, cancel):
            partial.unlink(missing_ok=True)
            raise AcquisitionError('ファイルの検証に失敗しました。再試行すると正しいファイルを再取得します。')
        os.replace(partial, target)
        self.update(aid, downloaded_bytes=completed+spec['bytes'])
        return target

    async def execute(self, aid, restart):
        a = self.assets[aid]; cancel = self.cancels[aid]; dest = self.install_dir(a)
        try:
            if not restart:
                completed = 0; files = []
                for spec in a['files']:
                    check_cancel(cancel)
                    files.append((spec, await self.download(aid, spec, completed))); completed += spec['bytes']
                self.update(aid, phase='installing', detail='音声エンジンと依存ソフトを導入しています。数分かかることがあります。')
                await self.adapter.install(self, a, dest, files, cancel)
                check_cancel(cancel)
                atomic_json(dest / 'installed.json', {'version':a['version'], 'asset_id':aid})
                self.update(aid, installed=True)
            self.update(aid, phase='preparing', detail='音声エンジンを起動し、最初の音声生成を確認しています。')
            await self.adapter.prepare(self, a, dest, cancel)
            check_cancel(cancel)
            await asyncio.to_thread(self.adapter.register, a, dest)
            self.update(aid, phase='ready', detail='3つの声を追加しました。声の一覧を更新して選べます。')
        except AcquisitionCancelled:
            await self.stop_runtime(aid)
            self.update(aid, phase='cancelled', detail='中止しました。取得済みデータを使って再試行できます。')
        except Exception as exc:
            await self.stop_runtime(aid)
            downloading = self.jobs[aid]['phase'] in {'queued', 'downloading', 'verifying'}
            detail = str(exc) if isinstance(exc, AcquisitionError) else ('取得元に接続できませんでした。通信を確認して再試行してください。' if downloading and isinstance(exc, httpx.HTTPError) else '追加データの準備に失敗しました。実行環境と空き容量を確認して再試行してください。')
            self.update(aid, phase='failed', detail=detail)

    async def subprocess(self, aid, args, *, cwd, env=None, runtime=False, timeout=1800):
        check_cancel(self.cancels[aid])
        log = self.home / 'logs' / f'{aid}.log'; log.parent.mkdir(parents=True, exist_ok=True)
        clean = {k:v for k,v in os.environ.items() if k not in {'PYTHONHOME','PYTHONPATH','VIRTUAL_ENV'} and not k.endswith('API_KEY')}
        clean.update(env or {})
        options = {'creationflags':subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session':True}
        with log.open('ab') as out:
            os.chmod(log, 0o600)
            proc = await asyncio.create_subprocess_exec(*map(str,args), cwd=str(cwd), env=clean, stdout=out, stderr=out, **options)
        if runtime:
            self.processes[aid] = proc
            return proc
        deadline = time.monotonic() + timeout
        try:
            while proc.returncode is None:
                check_cancel(self.cancels[aid])
                if time.monotonic() > deadline: raise AcquisitionError('依存ソフトの導入が時間内に完了しませんでした。通信を確認して再試行してください。')
                await asyncio.sleep(.25)
            if proc.returncode: raise AcquisitionError('依存ソフトを導入できませんでした。通信・空き容量・対応するGPUドライバを確認し、再試行してください。')
        finally:
            if proc.returncode is None: await self.terminate(proc)

    @staticmethod
    async def terminate(proc):
        if proc.returncode is not None: return
        try:
            if os.name == 'nt':
                killer = await asyncio.create_subprocess_exec('taskkill.exe','/PID',str(proc.pid),'/T','/F', stdout=asyncio.subprocess.DEVNULL, stderr=asyncio.subprocess.DEVNULL)
                await asyncio.wait_for(killer.wait(), timeout=10)
            else:
                import signal
                os.killpg(proc.pid, signal.SIGTERM)
            await asyncio.wait_for(proc.wait(), timeout=10)
        except (ProcessLookupError, asyncio.TimeoutError):
            if proc.returncode is None:
                if os.name != 'nt':
                    import signal
                    try: os.killpg(proc.pid, signal.SIGKILL)
                    except ProcessLookupError: pass
                else: proc.kill()
                await proc.wait()

    async def stop_runtime(self, aid):
        proc = self.processes.pop(aid, None)
        if proc: await self.terminate(proc)

    async def close(self):
        self.closed = True
        for event in self.cancels.values(): event.set()
        if self.tasks: await asyncio.gather(*self.tasks.values(), return_exceptions=True)
        for aid in list(self.processes): await self.stop_runtime(aid)
        if self.lock_file: self.lock_file.close(); self.lock_file = None
