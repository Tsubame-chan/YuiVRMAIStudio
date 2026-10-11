"""Actual-byte acquisition, recovery, ownership and admin-boundary acceptance."""
import asyncio, hashlib, json, sys, threading, zipfile
import httpx
import pytest
from fastapi.testclient import TestClient
from app.core.asset_downloads import AssetManager, AcquisitionError, atomic_json, extract_verified_zip
from app.core.irodori_acquisition import IrodoriAdapter, RUNTIME_ASSETS
from app.core.config import get_settings
from app.main import app

BODY=b'model-data-'*1000

def asset(id='test-model'):
    return {'id':id,'name':'Example','platform':'test','adapter':'test','version':'v1','files':[{'path':'model.bin','url':'https://example.org/immutable/model','sha256':hashlib.sha256(BODY).hexdigest(),'bytes':len(BODY)}],'minimum_free_bytes':0,'license_url':'https://example.org/license','source_url':'https://example.org/model','license_notice':'test'}

class Adapter:
    def __init__(self):
        self.installs=0;self.prepares=0;self.registered=0;self.started=asyncio.Event();self.release=asyncio.Event();self.release.set()
    async def unsupported(self,a):return ''
    async def install(self,m,a,d,files,c):
        self.installs+=1;d.mkdir(parents=True,exist_ok=True);(d/'model.bin').write_bytes(files[0][1].read_bytes())
    async def prepare(self,m,a,d,c):
        self.prepares+=1;self.started.set();await m.cancellable(a['id'],self.release.wait())
    def register(self,a,d):self.registered+=1

def http(monkeypatch,handler):
    original=httpx.AsyncClient
    monkeypatch.setattr('app.core.asset_downloads.httpx.AsyncClient',lambda **kw:original(**kw,transport=httpx.MockTransport(handler)))

async def finished(m,aid='test-model'):
    await asyncio.wait_for(asyncio.shield(m.tasks[aid]),timeout=5);return m.jobs[aid]

def test_duplicate_start_nonblocking_preserves_existing_files(tmp_path,monkeypatch):
    requests=[];http(monkeypatch,lambda r:(requests.append(r),httpx.Response(200,content=BODY))[1])
    async def check():
        a=Adapter();a.release.clear();m=AssetManager(tmp_path,[asset()],a)
        unrelated=tmp_path/'user-voices.json';unrelated.write_text('keep')
        await m.start('test-model');await a.started.wait()
        assert m.snapshot()['jobs'][0]['phase']=='preparing'
        await m.start('test-model');assert len(requests)==1 and a.installs==1
        a.release.set();assert (await finished(m))['phase']=='ready'
        assert unrelated.read_text()=='keep' and a.registered==1
        await m.close()
    asyncio.run(check())

def test_bad_sha_then_retry_does_not_register(tmp_path,monkeypatch):
    attempt=0
    def handler(r):
        nonlocal attempt
        attempt+=1;return httpx.Response(200,content=BODY if attempt>1 else b'x'*len(BODY))
    http(monkeypatch,handler)
    async def check():
        a=Adapter();m=AssetManager(tmp_path,[asset()],a);await m.start('test-model');j=await finished(m)
        assert j['phase']=='failed' and '検証' in j['detail'] and a.installs==a.registered==0
        await m.start('test-model');assert (await finished(m))['phase']=='ready' and attempt==2
        await m.close()
    asyncio.run(check())

@pytest.mark.parametrize('failure',[httpx.ConnectError('offline'),httpx.ReadTimeout('stalled')])
def test_network_failure_actionable(tmp_path,monkeypatch,failure):
    http(monkeypatch,lambda r:(_ for _ in ()).throw(failure))
    async def check():
        m=AssetManager(tmp_path,[asset()],Adapter());await m.start('test-model');j=await finished(m)
        assert j['phase']=='failed' and '通信' in j['detail'] and not j['installed'];await m.close()
    asyncio.run(check())

@pytest.mark.parametrize('mode',['short','range','long'])
def test_invalid_response_size_resume_rejected(tmp_path,monkeypatch,mode):
    response=httpx.Response(206,content=BODY,headers={'Content-Range':'bytes 0-1/2'}) if mode=='range' else httpx.Response(200,content=BODY[:-1] if mode=='short' else BODY+b'oversize')
    http(monkeypatch,lambda r:response)
    async def check():
        a=Adapter();m=AssetManager(tmp_path,[asset()],a);await m.start('test-model')
        assert (await finished(m))['phase']=='failed' and a.registered==0;await m.close()
    asyncio.run(check())

@pytest.mark.parametrize('supports_range',[True,False])
def test_resume_or_server_full_response(tmp_path,monkeypatch,supports_range):
    def handler(r):
        assert r.headers['range']=='bytes=100-'
        return httpx.Response(206,content=BODY[100:],headers={'Content-Range':f'bytes 100-{len(BODY)-1}/{len(BODY)}'}) if supports_range else httpx.Response(200,content=BODY)
    http(monkeypatch,handler)
    async def check():
        m=AssetManager(tmp_path,[asset()],Adapter());(tmp_path/'cache').mkdir();(tmp_path/'cache'/asset()['files'][0]['sha256']).with_suffix('.partial').write_bytes(BODY[:100])
        await m.start('test-model');assert (await finished(m))['phase']=='ready'
        assert (m.install_dir(asset())/'model.bin').read_bytes()==BODY;await m.close()
    asyncio.run(check())

def test_cancel_prepare_retry_reuses_cache(tmp_path,monkeypatch):
    requests=[];http(monkeypatch,lambda r:(requests.append(r),httpx.Response(200,content=BODY))[1])
    async def check():
        a=Adapter();a.release.clear();m=AssetManager(tmp_path,[asset()],a)
        await m.start('test-model');await a.started.wait();await m.cancel('test-model');j=await finished(m)
        assert j['phase']=='cancelled' and j['installed'] and a.registered==0
        a.release.set();await m.start('test-model');assert (await finished(m))['phase']=='ready'
        assert len(requests)==1 and a.installs==1 and a.prepares==2;await m.close()
    asyncio.run(check())

def test_dependency_failure_retry_with_cached_model(tmp_path,monkeypatch):
    requests=[];http(monkeypatch,lambda r:(requests.append(r),httpx.Response(200,content=BODY))[1])
    class Fails(Adapter):
        async def install(self,m,a,d,f,c):
            self.installs+=1
            if self.installs==1:raise AcquisitionError('依存ソフトの導入に失敗しました。')
            d.mkdir(parents=True,exist_ok=True)
    async def check():
        a=Fails();m=AssetManager(tmp_path,[asset()],a);await m.start('test-model');assert (await finished(m))['phase']=='failed'
        await m.start('test-model');assert (await finished(m))['phase']=='ready'
        assert a.installs==2 and len(requests)==1;await m.close()
    asyncio.run(check())

def test_capacity_unsupported_no_network(tmp_path):
    async def check():
        spec=asset();spec['minimum_free_bytes']=10**20;m=AssetManager(tmp_path,[spec],Adapter())
        with pytest.raises(AcquisitionError,match='空き容量'):await m.start('test-model')
        assert not m.tasks;await m.close()
        a=Adapter()
        async def unsupported(asset):return 'NVIDIA GPUが必要です。'
        a.unsupported=unsupported;m=AssetManager(tmp_path,[asset()],a)
        with pytest.raises(AcquisitionError,match='NVIDIA'):await m.start('test-model')
        await m.close()
    asyncio.run(check())

@pytest.mark.parametrize('phase,installed',[('installing',False),('ready',True)])
def test_restart_interruption_or_installed_prepare(tmp_path,phase,installed):
    atomic_json(tmp_path/'state.json',{'schema':1,'jobs':{'test-model':{'phase':phase,'installed':installed,'version':'v1'}}})
    async def check():
        a=Adapter();m=AssetManager(tmp_path,[asset()],a);await m.restore()
        if installed:
            assert (await finished(m))['phase']=='ready' and a.installs==0 and a.prepares==1
        else:assert m.jobs['test-model']['phase']=='failed' and '再起動' in m.jobs['test-model']['detail']
        await m.close()
    asyncio.run(check())

def test_exclusive_owner_shutdown_only_owned(tmp_path):
    async def check():
        m=AssetManager(tmp_path,[asset()],Adapter());m.acquire_owner();other=AssetManager(tmp_path,[asset()],Adapter())
        with pytest.raises(AcquisitionError,match='別のBackend'):other.acquire_owner()
        m.cancels['test-model']=threading.Event()
        unrelated=await asyncio.create_subprocess_exec(sys.executable,'-c','import time;time.sleep(20)')
        owned=await m.subprocess('test-model',[sys.executable,'-c','import time;time.sleep(20)'],cwd=tmp_path,runtime=True)
        await m.close();assert owned.returncode is not None and unrelated.returncode is None
        unrelated.terminate();await unrelated.wait();other.acquire_owner();await other.close()
    asyncio.run(check())

@pytest.mark.parametrize('name',['../escape','/absolute','C:/absolute','dir\\bad','bad:stream','CON.txt','dir/name.','dir/name '])
def test_zip_unsafe_paths_rejected(tmp_path,name):
    p=tmp_path/'asset.zip'
    with zipfile.ZipFile(p,'w') as z:z.writestr(name,b'bad')
    with pytest.raises(AcquisitionError):extract_verified_zip(p,tmp_path/'extract',1024,threading.Event())

def test_zip_links_duplicate_size(tmp_path):
    for case in ['symlink','duplicate','size']:
        p=tmp_path/(case+'.zip')
        with zipfile.ZipFile(p,'w') as z:
            if case=='symlink':
                info=zipfile.ZipInfo('link');info.external_attr=0o120777<<16;z.writestr(info,'../target')
            else:
                z.writestr('File',b'12345')
                if case=='duplicate':z.writestr('file',b'12345')
        with pytest.raises(AcquisitionError):extract_verified_zip(p,tmp_path/case,3 if case=='size' else 100,threading.Event())

def test_register_preserves_custom_profiles_selection(tmp_path,monkeypatch):
    from app.core.voice_library import LibraryUpdate,load,save,public_library
    from app.core.config import Settings
    monkeypatch.setenv('YUI_VOICE_LIBRARY_PATH',str(tmp_path/'voices.json'));settings=Settings(tts_provider='voicevox',http_tts_api_key='user-secret')
    monkeypatch.setattr('app.core.irodori_acquisition.get_settings',lambda:settings)
    save(LibraryUpdate(revision='initial',endpoints=[],profiles=[{'id':'mine','name':'My voice','endpoint_id':'builtin-voicevox','parameters':{'speaker_id':3}}]),settings)
    a={'id':'irodori-macos-v41','adapter':'irodori-mlx','port':41091};adapter=IrodoriAdapter();adapter.register(a,tmp_path);data,rev=load(settings)
    assert len(data['profiles'])==4 and data['profiles'][0]['id']=='mine'
    assert data['endpoints'][0]['settings']['http_tts_audio_processor']=='none'
    assert settings.tts_provider=='voicevox' and settings.http_tts_api_key=='user-secret'
    data['profiles'][1]['name']='My renamed bright voice';save(LibraryUpdate(revision=rev,**data),settings)
    adapter.register(a,tmp_path);data,_=load(settings);assert data['profiles'][1]['name']=='My renamed bright voice' and len(data['profiles'])==4
    assert 'user-secret' not in json.dumps(public_library(settings))

def test_windows_scripts_no_env_rewrite_and_frozen_py311():
    setup=(RUNTIME_ASSETS/'setup_irodori_v4_windows.ps1').read_text();start=(RUNTIME_ASSETS/'start_irodori_v4_windows.ps1').read_text()
    assert '[switch]$DependenciesOnly' in setup and setup.index('if ($DependenciesOnly)')<setup.index('$envFile =')
    assert '--port $Port' in start and '[int]$Port = 8088' in start
    assert '3.11' in setup and '--frozen --extra cu128' in setup

def test_admin_catalog_not_arbitrary_url(tmp_path,monkeypatch):
    monkeypatch.setenv('YUI_ASSET_HOME',str(tmp_path/'addons'));monkeypatch.setenv('YUI_BACKEND_SETTINGS_PATH',str(tmp_path/'settings.json'));monkeypatch.setenv('DATABASE_URL','sqlite:///'+str(tmp_path/'test.db'));get_settings.cache_clear()
    with TestClient(app,base_url='http://localhost',client=('127.0.0.1',1234)) as c:
        assert c.get('/admin/api/assets').status_code==403
        c.post('/admin/session',headers={'X-Yui-Admin':'1'});c.headers['X-Yui-Admin']='1'
        assert c.get('/admin/api/assets').status_code==200
        assert c.post('/admin/api/assets/unknown/start',json={}).status_code==404
        assert c.post('/admin/api/assets/irodori-macos-v41/start',json={'url':'http://localhost/secrets','command':'evil'}).status_code==422
        assert c.post('/admin/api/assets/irodori-macos-v41/start',headers={'X-Yui-Admin':''},json={}).status_code==403
        assert c.post('/admin/api/assets/irodori-macos-v41/start',headers={'Origin':'https://evil.test'},json={}).status_code==403
        assert c.get('/assets/downloads/status').json()=={'jobs':[],'blocked':''}
        assert c.get('/admin/assets/asset-downloads.js').status_code==200
    get_settings.cache_clear()

def test_preparation_http_failure_is_not_download_network_error(tmp_path,monkeypatch):
    http(monkeypatch,lambda r:httpx.Response(200,content=BODY))
    class Broken(Adapter):
        async def prepare(self,*args):
            raise httpx.HTTPStatusError('private server detail',request=httpx.Request('POST','http://127.0.0.1/audio'),response=httpx.Response(500))
    async def check():
        m=AssetManager(tmp_path,[asset()],Broken());await m.start('test-model');j=await finished(m)
        assert j['phase']=='failed' and j['installed']
        assert '準備' in j['detail'] and '取得元' not in j['detail'] and 'private' not in j['detail']
        await m.close()
    asyncio.run(check())

def test_verified_cache_counts_toward_retry_capacity(tmp_path,monkeypatch):
    from collections import namedtuple
    Disk=namedtuple('Disk','total used free')
    monkeypatch.setattr('app.core.asset_downloads.shutil.disk_usage',lambda p:Disk(50000,45000,5000))
    a=asset();a['minimum_free_bytes']=len(BODY)+4000
    m=AssetManager(tmp_path,[a],Adapter());cache=tmp_path/'cache';cache.mkdir();(cache/a['files'][0]['sha256']).write_bytes(BODY)
    async def check():
        await m.start('test-model');assert (await finished(m))['phase']=='ready';await m.close()
    asyncio.run(check())
