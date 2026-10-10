import json
import os
import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.core.config import get_settings
from app.core.admin_settings import read_overrides, settings_path, save_settings
from app.db.repositories import ChatRepository, MemoryRepository
from app.models.chat import ChatResponse
from app.models.memory import MemorySaveRequest


@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('YUI_BACKEND_SETTINGS_PATH', str(tmp_path/'settings.json'))
    monkeypatch.setenv('DATABASE_URL', 'sqlite:///'+str(tmp_path/'test.db'))
    monkeypatch.setenv('OPENAI_API_KEY', 'test-secret-never-return')
    get_settings.cache_clear()
    app.dependency_overrides.clear()
    with TestClient(app, base_url='http://localhost', client=('127.0.0.1', 55555)) as c:
        assert c.post('/admin/session',headers={'X-Yui-Admin':'1'}).status_code==200
        c.headers['X-Yui-Admin']='1'
        yield c
    app.dependency_overrides.clear()
    get_settings.cache_clear()


def test_session_origin_host_and_remote_boundaries(client):
    assert client.get('/admin/api/overview').status_code==200
    assert client.get('/admin/api/overview',headers={'Host':'evil.test'}).status_code==403
    assert client.post('/admin/session',headers={'Origin':'https://evil.test'}).status_code==403
    assert client.patch('/admin/api/settings',headers={'X-Yui-Admin':''},json={}).status_code==403
    with TestClient(app,base_url='http://localhost',client=('10.0.0.2',3333)) as remote:
        assert remote.post('/admin/session',headers={'X-Yui-Admin':'1'}).status_code==403
        assert remote.get('/admin/').status_code==403
        # Native app APIs still use the established VPN contract.
        assert remote.get('/health').status_code==200
    client.cookies.clear()
    assert client.get('/admin/api/run/memory/search').status_code in (403,405)
    assert client.get('/admin/api/overview').status_code==403


def test_secret_redaction_persistence_and_conflict(client):
    data=client.get('/admin/api/overview')
    assert 'test-secret-never-return' not in data.text
    settings=data.json()['settings']
    assert settings['secrets']['openai_api_key'] is True
    saved=client.patch('/admin/api/settings',json={'revision':settings['revision'],'changes':{'openai_api_key':'','openai_chat_model':'model-new'}})
    assert saved.status_code==200
    assert get_settings().openai_api_key=='test-secret-never-return'
    assert get_settings().openai_chat_model=='model-new'
    assert 'openai_api_key' not in read_overrides()[0]
    if os.name != "nt":  # Windows chmod does not implement POSIX permission bits.
        assert os.stat(settings_path()).st_mode & 0o777 == 0o600
    assert client.patch('/admin/api/settings',json={'revision':settings['revision'],'changes':{'character_name':'overwritten'}}).status_code==409
    get_settings.cache_clear()
    assert get_settings().openai_chat_model=='model-new'
    cleared=client.patch('/admin/api/settings',json={'revision':saved.json()['revision'],'changes':{'openai_api_key':None}})
    assert cleared.status_code==200 and cleared.json()['secrets']['openai_api_key'] is False
    assert get_settings().openai_api_key==''


@pytest.mark.parametrize('changes',[{'database_url':'sqlite:///bad.db'},{'chat_provider':'imaginary'}, {'voicevox_base_url':'file:///etc/passwd'}, {'voicevox_base_url':'http://token:secret@localhost'}, {'http_tts_endpoint':'//evil.test'}, {'openai_max_output_tokens':-1}, {'openai_chat_model':{}}, {'xai_api_key':{'secret':'do-not-echo'}}, {'voicevox_base_url':42}, {'http_tts_endpoint':42}, {'daily_chat_limit':True}, {'openai_web_search_enabled':'false'}])
def test_invalid_values_leave_settings_unchanged_and_no_input_echo(client,changes):
    before=client.get('/admin/api/overview').json()['settings']['revision']
    r=client.patch('/admin/api/settings',json={'revision':before,'changes':changes})
    assert r.status_code==422
    assert 'do-not-echo' not in r.text
    assert read_overrides()==({},'initial')


def test_atomic_failure_does_not_apply_or_destroy_existing_settings(client,monkeypatch):
    saved=save_settings({'character_name':'before'},'initial')
    monkeypatch.setattr(os,'replace',lambda *a: (_ for _ in ()).throw(OSError('disk full')))
    r=client.patch('/admin/api/settings',json={'revision':saved['revision'],'changes':{'character_name':'after'}})
    assert r.status_code==503
    get_settings.cache_clear()
    assert get_settings().character_name=='before'


def test_character_and_session_delete_does_not_cross_boundaries(client):
    settings=get_settings(); chat=ChatRepository(settings.database_url); memory=MemoryRepository(settings.database_url)
    for i,(u,c,s) in enumerate([('alice','a','one'),('alice','a','two'),('alice','b','one'),('bob','a','one')]):
        chat.save_chat_turn(request_id=str(i),user_id=u,character_id=c,session_id=s,user_message='test',response=ChatResponse(text='reply'),provider='fake',model='fake')
        memory.save(MemorySaveRequest(user_id=u,character_id=c,content=f'memory-{i}'))
    items=client.get('/admin/api/identities').json()['items']
    assert any(x['session_id']=='two' for x in items)
    body={'user_id':'alice','character_id':'a','session_id':'one','include_memories':False,'confirmation':'wrong'}
    assert client.post('/admin/api/clear-scope',json=body).status_code==422
    body['confirmation']='alice'
    assert client.post('/admin/api/clear-scope',json=body).json()['deleted']['conversations']==2
    assert chat.list_recent_messages('alice',character_id='a',session_id='one')==[]
    assert len(chat.list_recent_messages('alice',character_id='a',session_id='two'))==2
    assert len(chat.list_recent_messages('alice',character_id='b',session_id='one'))==2
    assert len(chat.list_recent_messages('bob',character_id='a',session_id='one'))==2
    assert len(memory.list_recent('alice',character_id='a'))==2


def test_admin_chat_uses_runtime_contract_and_secret_boundary(client,monkeypatch):
    from app.api.routes import ProviderRouter
    class Fake:
        name='fake'
        async def generate(self,request,history):
            assert request.character_id=='console-preview' and request.secret
            return ChatResponse(text='reply',memory_action='save')
    monkeypatch.setattr(ProviderRouter,'chat',lambda s:Fake())
    result=client.post('/admin/api/run/chat',json={'request_id':'r','user_id':'test-user','character_id':'console-preview','message':'remember','secret':True})
    assert result.status_code==200 and result.json()['text']=='reply'
    settings=get_settings()
    assert MemoryRepository(settings.database_url).list_recent('test-user',character_id='console-preview')==[]
    assert ChatRepository(settings.database_url).list_recent_messages('test-user',character_id='console-preview')==[]


def test_static_security_and_readiness_are_truthful(client):
    r=client.get('/admin/')
    assert r.status_code==200 and 'no-store' in r.headers['cache-control']
    assert "frame-ancestors 'none'" in r.headers['content-security-policy']
    assert client.get('/admin/assets/.env').status_code==404
    status=client.post('/admin/api/probe/openai').json()
    assert status['status']=='configured' and status['models']==[]


def test_settings_schema_has_complete_editable_field_coverage(client):
    from app.core.config import Settings
    from app.core.admin_settings import READ_ONLY
    fields=client.get('/admin/api/overview').json()['settings']['fields']
    assert {f['name'] for f in fields}==set(Settings.model_fields)-READ_ONLY


def test_http_tts_registry_resolves_existing_adapter(client):
    from app.providers.router import ProviderRouter
    from app.providers.http_tts import HttpTTSProvider
    settings=get_settings().model_copy(update={'tts_provider':'http','http_tts_base_url':'http://127.0.0.1:41090'})
    assert isinstance(ProviderRouter(settings).tts(), HttpTTSProvider)
    assert isinstance(ProviderRouter(settings).tts('http'), HttpTTSProvider)


def test_browser_websocket_origin_is_rejected_before_provider_call(client):
    from starlette.websockets import WebSocketDisconnect
    for path in ('/realtime/stream','/admin/api/run/realtime/stream'):
        with pytest.raises(WebSocketDisconnect) as rejected:
            with client.websocket_connect(path,headers={'Origin':'https://evil.test'}):
                pass
        assert rejected.value.code==1008


def test_activity_feed_is_protected_and_does_not_return_content(client,monkeypatch):
    from app.api.routes import ProviderRouter
    class Fake:
        name='fake'
        async def generate(self,request,history):
            return ChatResponse(text='private-generated-content')
    monkeypatch.setattr(ProviderRouter,'chat',lambda s:Fake())
    before=client.get('/admin/api/events').json()['cursor']
    response=client.post('/admin/api/run/chat',json={'request_id':'trace-test','message':'private-user-content',
        'user_id':'private-user-id','character_id':'console-preview','secret':True,
        'context':{'screen_context':'private-screen-content','extra':{'character_memory':'private-memory-content'}}})
    assert response.status_code==200
    feed=client.get('/admin/api/events',params={'after':before})
    assert feed.status_code==200
    for secret in ('private-generated-content','private-user-content','private-screen-content',
                   'private-memory-content','private-user-id','test-secret-never-return'):
        assert secret not in feed.text
    items=feed.json()['items']
    assert {x['stage'] for x in items} >= {'started','context_ready','generating','completed'}
    assert len({x['trace'] for x in items})==1
    assert any(x.get('counts',{}).get('screen_chars')==len('private-screen-content') for x in items)
    client.cookies.clear()
    assert client.get('/admin/api/events').status_code==403


def test_activity_feed_is_bounded():
    from app.core.runtime_events import record, events_since
    for _ in range(1050): record('started',operation='chat')
    feed=events_since()
    assert len(feed['items'])==1000 and feed['truncated']
    assert events_since(feed['cursor'])['items']==[]
    assert events_since(feed['cursor']+1000)['reset']


@pytest.mark.parametrize('cause,expected', [('connect','unreachable'),('timeout','timeout'),('auth','authentication')])
def test_failed_chat_diagnostic_is_specific_but_redacts_provider_text(client,monkeypatch,cause,expected):
    import httpx
    from app.api.routes import ProviderRouter
    from app.providers.openai_chat import ChatProviderError
    class Fake:
        name='openai'
        async def generate(self,request,history):
            if cause=='connect': error=httpx.ConnectError('private-error-body')
            elif cause=='timeout': error=httpx.ReadTimeout('private-error-body')
            else: error=httpx.HTTPStatusError('private-error-body',request=httpx.Request('POST','https://example.test'),response=httpx.Response(401))
            raise ChatProviderError('private-provider-body') from error
    monkeypatch.setattr(ProviderRouter,'chat',lambda s:Fake())
    before=client.get('/admin/api/events').json()['cursor']
    result=client.post('/admin/api/run/chat',json={'request_id':'failed','message':'private-message','secret':True})
    assert result.status_code==502
    trace=result.headers['X-Yui-Trace']
    feed=client.get('/admin/api/events',params={'after':before})
    assert any(x.get('code')==expected and x['trace']==trace for x in feed.json()['items'])
    assert 'private-error-body' not in feed.text and 'private-provider-body' not in feed.text


def test_failed_probe_is_not_reported_as_success_in_activity(client,monkeypatch):
    import httpx
    async def fail(*a,**kw): raise httpx.ConnectError('private-connection-text')
    monkeypatch.setattr(httpx.AsyncClient,'get',fail)
    before=client.get('/admin/api/events').json()['cursor']
    result=client.post('/admin/api/probe/voicevox')
    assert result.status_code==200 and result.json()['status']=='offline'
    events=client.get('/admin/api/events',params={'after':before}).json()['items']
    assert any(x.get('code')=='unreachable' for x in events)
