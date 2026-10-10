import asyncio
import os
import pytest
import httpx
from test_admin_console import client
from app.core.config import Settings
from app.core.voice_library import LibraryUpdate, save, load, resolve_profile, capabilities, endpoint_settings
from app.models.tts import TTSRequest, TTSResponse
from app.providers.voicevox_tts import TTSProviderError
from app.providers.router import ProviderRouter
from app.api.routes import _synthesize_tts_with_fallback

@pytest.fixture(autouse=True)
def storage(tmp_path,monkeypatch):
    monkeypatch.setenv('YUI_VOICE_LIBRARY_PATH',str(tmp_path/'voices.json'))

def endpoint(id='remote'):
    return {'id':id,'name':'Remote','provider_type':'http','settings':{'http_tts_base_url':'http://127.0.0.1:9876','http_tts_payload_format':'irodori_openai_speech','http_tts_api_key':'private-key'}}

def profile():
    return {'id':'main','name':'Saved voice','endpoint_id':'remote','parameters':{'speed_scale':1.2,'voice_instruct':'calm'},'voice':'voice-a','model':'irodori'}

def test_multiple_endpoints_keys_reload_conflict(client):
    body={'revision':'initial','endpoints':[endpoint(),endpoint('second')],'profiles':[profile()]}
    r=client.put('/admin/api/voice-library',json=body);assert r.status_code==200
    assert 'private-key' not in r.text and len(r.json()['endpoints'])==5
    assert r.json()['endpoints'][3]['key_configured'] is True
    assert client.get('/tts/profiles').json()=={'items':[{'id':'main','name':'Saved voice','provider':'http'}]}
    assert 'http://127' not in client.get('/tts/profiles').text
    assert client.put('/admin/api/voice-library',json=body).status_code==409
    body['revision']=r.json()['revision'];body['endpoints'][0]['settings']['http_tts_api_key']=''
    r=client.put('/admin/api/voice-library',json=body);assert r.status_code==200
    assert load()[0]['endpoints'][0]['settings']['http_tts_api_key']=='private-key'
    body['revision']=r.json()['revision'];body['endpoints'][0]['settings']['http_tts_api_key']=None
    assert client.put('/admin/api/voice-library',json=body).status_code==200
    assert load()[0]['endpoints'][0]['settings']['http_tts_api_key']==''
    if os.name != 'nt':  # Windows chmod does not implement POSIX permission bits.
        assert os.stat(os.environ['YUI_VOICE_LIBRARY_PATH']).st_mode&0o777==0o600

def test_explicit_fields_override_profile_without_model_defaults():
    save(LibraryUpdate(revision='initial',endpoints=[endpoint()],profiles=[profile()]),Settings())
    req,s,_,_=resolve_profile('main',TTSRequest(text='hello'),Settings())
    assert req.speed_scale==1.2 and req.voice_instruct=='calm' and s.http_tts_voice=='voice-a'
    req,_,_,_=resolve_profile('main',TTSRequest(text='hello',speed_scale=1.5),Settings())
    assert req.speed_scale==1.5
    assert TTSRequest(text='legacy',speaker_id=3,pitch_scale=.1).speaker_id==3

def test_new_endpoint_does_not_inherit_another_server_credentials_or_controls():
    settings=Settings(http_tts_api_key='legacy-secret',http_tts_voice='legacy-voice',http_tts_model='irodori')
    e=endpoint();e['settings']={'http_tts_base_url':'http://127.0.0.1:9876','http_tts_payload_format':'openai_speech'}
    actual=endpoint_settings(e,settings)
    assert actual.http_tts_api_key=='' and actual.http_tts_voice=='' and actual.http_tts_model==''
    p=profile();p['parameters']={'speed_scale':1.2};p['model']='speech'
    save(LibraryUpdate(revision='initial',endpoints=[e],profiles=[p]),settings)
    req,_,_,_=resolve_profile('main',TTSRequest(text='hello'),settings)
    assert req.speed_scale==1.2 and req.pitch_scale is None and req.intonation_scale is None

@pytest.mark.parametrize('change',[
    lambda b:b['profiles'][0]['parameters'].update({'speed_scale':99}),
    lambda b:b['profiles'][0].update({'endpoint_id':'missing'}),
    lambda b:b['profiles'][0].update({'fallback_profile_id':'main'}),
    lambda b:b['endpoints'][0]['settings'].update({'http_tts_base_url':'http://user:secret@example.com'}),
    lambda b:b['endpoints'][0]['settings'].update({'openai_api_key':'must-not-echo'}),
    lambda b:b['endpoints'][0]['settings'].update({'http_tts_soundstretch_path':'/bin/evil'}),
    lambda b:b['profiles'][0]['parameters'].update({'intonation_scale':1})])
def test_reject_invalid_library_without_partial_write(client,change):
    body={'revision':'initial','endpoints':[endpoint()],'profiles':[profile()]};change(body)
    r=client.put('/admin/api/voice-library',json=body);assert r.status_code==422
    assert 'must-not-echo' not in r.text and 'private-key' not in r.text and load()[1]=='initial'

def test_atomic_failure_retains_settings(client,monkeypatch):
    body={'revision':'initial','endpoints':[endpoint()],'profiles':[profile()]}
    r=client.put('/admin/api/voice-library',json=body);assert r.status_code==200
    before=load();body['revision']=r.json()['revision'];body['profiles'][0]['name']='changed'
    monkeypatch.setattr(os,'replace',lambda *a:(_ for _ in ()).throw(OSError('disk')))
    assert client.put('/admin/api/voice-library',json=body).status_code==503
    assert load()==before

def test_fallback_uses_its_own_speaker_and_tuning(monkeypatch):
    main={'id':'main','name':'Main','endpoint_id':'builtin-voicevox','parameters':{'speaker_id':3,'speed_scale':1.3},'fallback_profile_id':'alt'}
    alt={'id':'alt','name':'Alt','endpoint_id':'builtin-aivis','parameters':{'speaker_id':1431611904,'speed_scale':.8}}
    save(LibraryUpdate(revision='initial',endpoints=[],profiles=[main,alt]),Settings())
    calls=[]
    class Provider:
        def __init__(self,name):self.name=name
        async def synthesize(self,request):
            calls.append((self.name,request.speaker_id,request.speed_scale))
            if self.name=='voicevox':raise TTSProviderError('failed')
            return TTSResponse(audio_url='/audio/test.wav')
    monkeypatch.setattr(ProviderRouter,'tts',lambda self,p=None:Provider(p))
    asyncio.run(_synthesize_tts_with_fallback(ProviderRouter(Settings()),TTSRequest(text='hello',voice_profile_id='main'),Settings()))
    assert calls==[('voicevox',3,1.3),('aivis',1431611904,.8)]

def test_preview_validates_tuning_and_redacts_provider_errors(client,monkeypatch):
    assert client.put('/admin/api/voice-library',json={'revision':'initial','endpoints':[endpoint()],'profiles':[profile()]}).status_code==200
    calls=[]
    class Provider:
        name='http'
        async def synthesize(self,request):
            calls.append(request);raise TTSProviderError('private-provider-response')
    monkeypatch.setattr(ProviderRouter,'tts',lambda self,p=None:Provider())
    r=client.post('/admin/api/voice-preview',json={'endpoint_id':'remote','parameters':{'speed_scale':1.4,'voice_instruct':'calm'},'voice':'new','model':'irodori','text':'test'})
    assert r.status_code==502 and r.headers.get('X-Yui-Trace')
    assert calls[0].speed_scale==1.4 and 'private-provider-response' not in r.text
    assert client.post('/admin/api/voice-preview',json={'endpoint_id':'remote','parameters':{'intonation_scale':1},'text':'test'}).status_code==422

def test_disabled_postprocessing_and_admin_auth(client):
    e=endpoint();e['settings']['http_tts_audio_processor']='none'
    assert capabilities(e,Settings())['fields']==[]
    client.cookies.clear()
    assert client.get('/admin/api/voice-library').status_code==403
    assert client.put('/admin/api/voice-library',json={}).status_code==403

def test_voice_discovery_uses_aivis_speakers_and_irodori_models(client,monkeypatch):
    calls=[]
    def respond(request):
        calls.append(request.url.path)
        if request.url.path=='/speakers':
            return httpx.Response(200,json=[{'name':'まい','styles':[{'name':'ノーマル','id':1431611904}]}])
        if request.url.path=='/v1/audio/voices':
            return httpx.Response(200,json={'data':[{'id':'none'}]})
        return httpx.Response(200,json={'data':[{'id':'irodori-test'}]})
    original=httpx.AsyncClient
    monkeypatch.setattr(httpx,'AsyncClient',lambda **kwargs:original(**kwargs,transport=httpx.MockTransport(respond)))
    aivis=client.post('/admin/api/voice-endpoints/builtin-aivis/probe').json()
    assert aivis['voices']==[{'id':1431611904,'label':'まい / ノーマル'}]
    e=endpoint();e['settings']['http_tts_endpoint']='/v1/audio/speech'
    assert client.put('/admin/api/voice-library',json={'revision':'initial','endpoints':[e],'profiles':[profile()]}).status_code==200
    iro=client.post('/admin/api/voice-endpoints/remote/probe').json()
    assert iro['status']=='ok' and iro['models']==['irodori-test'] and iro['voices']==[{'id':'none','label':'声の説明で生成'}]
    assert calls==['/speakers','/v1/models','/v1/audio/voices']
    c=capabilities(e,Settings())
    assert c['voices']=='design' and 'voice_gender' not in {x['name'] for x in c['text_fields']}
    e['settings']={'http_tts_payload_format':'openai_speech'}
    assert capabilities(e,Settings())['text_fields']==[]


@pytest.mark.parametrize("dialect", ["openai_speech", "irodori_openai_speech"])
def test_irodori_initial_library_offers_three_accepted_presets(dialect):
    settings=Settings(http_tts_provider_id="irodori",http_tts_model="mlx-community/Irodori-TTS-v4.1-Small-8bit",http_tts_payload_format=dialect)
    data,revision=load(settings)
    assert revision=="initial"
    assert [p["id"] for p in data["profiles"]]==["yui-irodori-bright_natural","yui-irodori-gentle_friend","yui-irodori-calm_natural"]
    for p in data["profiles"]:
        request,resolved_settings,_,_=resolve_profile(p["id"],TTSRequest(text="こんにちは。"),settings)
        if dialect == "irodori_openai_speech":
            assert resolved_settings.http_tts_voice == p["id"].removeprefix("yui-irodori-")
            assert request.voice_gender is None and request.voice_lang_code is None
        else:
            assert request.voice_gender == "female" and request.voice_lang_code == "ja"
        from app.core.irodori_presets import reference
        audio,text=reference(request.voice_instruct)
        assert audio.read_bytes()[:4]==b"RIFF" and "おかえりなさい" in text
    save(LibraryUpdate(revision=revision,**data),settings)
    assert len(load(settings)[0]["profiles"])==3

@pytest.mark.parametrize("source,target", [("openai_speech", "irodori_openai_speech"), ("irodori_openai_speech", "openai_speech")])
def test_untouched_factory_profiles_follow_builtin_dialect_without_rewriting_file(source, target):
    from app.core.irodori_presets import profiles
    from app.core.voice_library import library_path
    old = Settings(http_tts_provider_id="irodori", http_tts_payload_format=source)
    updated = old.model_copy(update={"http_tts_payload_format": target})
    save(LibraryUpdate(revision="initial", endpoints=[], profiles=profiles(old)), old)
    stored = library_path().read_bytes()
    data, revision = load(updated)
    assert data["profiles"] == profiles(updated)
    for profile in data["profiles"]:
        resolve_profile(profile["id"], TTSRequest(text="こんにちは。"), updated)
    assert library_path().read_bytes() == stored
    assert revision == load(old)[1]


def test_custom_voice_tuning_is_not_silently_removed_on_dialect_change():
    from app.core.irodori_presets import profiles
    old = Settings(http_tts_provider_id="irodori", http_tts_payload_format="openai_speech")
    custom = profiles(old)
    custom[0]["parameters"]["voice_gender"] = "male"
    save(LibraryUpdate(revision="initial", endpoints=[], profiles=custom), old)
    data, _ = load(old.model_copy(update={"http_tts_payload_format": "irodori_openai_speech"}))
    assert data["profiles"][0] == custom[0]
