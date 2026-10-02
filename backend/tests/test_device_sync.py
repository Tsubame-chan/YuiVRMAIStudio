import json
import sqlite3
import pytest
from fastapi.testclient import TestClient
from app.main import app
from app.core.config import get_settings


@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setenv('DATABASE_URL', 'sqlite:///' + str(tmp_path/'sync.db'))
    monkeypatch.setenv('YUI_BACKEND_SETTINGS_PATH', str(tmp_path/'settings.json'))
    get_settings.cache_clear()
    with TestClient(app, base_url='http://localhost', client=('127.0.0.1', 4321)) as c:
        c.post('/admin/session', headers={'X-Yui-Admin':'1'})
        c.headers['X-Yui-Admin'] = '1'
        yield c
    get_settings.cache_clear()


def pair(c, name='Phone'):
    code = c.post('/admin/api/sync/pairing').json()['code']
    r = c.post('/sync/pair', json={'name':name,'code':code})
    assert r.status_code == 200
    return r.json()

def auth(d): return {'Authorization':'Bearer '+d['token']}
def item(content, id='m1', base=0, changed=True, deleted=False):
    return {'kind':'memory','id':id,'base_version':base,'changed':changed,'deleted':deleted,
            'value':{} if deleted else {'content':content,'pinned':False,'recorded_utc':'2026-10-03T01:00:00Z'}}
def history(text, id, when):
    return {'kind':'history','id':id,'value':{'text':text,'speaker':'You','mode':'talk',
        'recorded_utc':when,'conversation_id':'conversation:'+id,'turn_id':'turn:'+id}}
def plan(c,d,char,items):
    r=c.post('/sync/plan',headers=auth(d),json={'character_id':char,'items':items})
    assert r.status_code==200, r.text
    return r.json()
def commit(c,d,p,choices=None):
    return c.post('/sync/commit',headers=auth(d),json={'plan_id':p['plan_id'],'choices':choices or {}})


def test_pairing_one_use_authentication_and_revocation(client):
    c=client
    code=c.post('/admin/api/sync/pairing').json()['code']
    d=c.post('/sync/pair',json={'name':'Phone','code':code}).json()
    assert c.post('/sync/pair',json={'name':'Other','code':code}).status_code==401
    assert c.get('/sync/characters').status_code==401
    assert c.get('/sync/characters',headers=auth(d)).status_code==200
    assert d['token'] not in c.get('/admin/api/sync/devices').text
    db=get_settings().database_url.removeprefix('sqlite:///')
    assert d['token'] not in open(db,'rb').read().decode('latin1')
    c.post('/admin/api/sync/devices/'+d['device_id']+'/revoke')
    assert c.get('/sync/characters',headers=auth(d)).status_code==401
    with TestClient(app,base_url='http://localhost',client=('10.0.0.2',42)) as remote:
        assert remote.post('/admin/api/sync/pairing',headers={'X-Yui-Admin':'1'}).status_code==403


def test_failed_pairing_attempt_limit(client):
    code=client.post('/admin/api/sync/pairing').json()['code']
    for _ in range(10): assert client.post('/sync/pair',json={'name':'Phone','code':'ZZZZZZZZZZ'}).status_code==401
    assert client.post('/sync/pair',json={'name':'Phone','code':code}).status_code==401
    assert pair(client)['token']


def test_two_devices_merge_histories_and_memories_without_clock_overwrite(client):
    c=client; phone=pair(c); pc=pair(c,'PC')
    char=c.post('/sync/characters',headers=auth(phone),json={'name':'Character'}).json()['id']
    p=plan(c,phone,char,[item('phone memory')])
    # Planning is read-only; PC can still see an empty snapshot.
    other=plan(c,pc,char,[])
    assert other['download']==0
    first=commit(c,phone,p).json()
    p2=plan(c,pc,char,[item('PC memory','m2')])
    assert p2['download']==1 and not p2['conflicts']
    merged=commit(c,pc,p2).json()
    assert {x['value']['content'] for x in merged['items']}=={'phone memory','PC memory'}
    assert commit(c,pc,p2).json()==merged
    # Repeat the same state with the agreed base: no duplicated records.
    p3=plan(c,phone,char,[item('phone memory',base=first['items'][0]['version'],changed=False)])
    assert p3['upload']['memory']==0 and p3['download']==1
    assert len(commit(c,phone,p3).json()['items'])==2

    # Clock order never decides which independently authored conversation survives.
    p4=plan(c,phone,char,[history('phone conversation','h1','2026-10-04T00:00:00Z')])
    commit(c,phone,p4)
    p5=plan(c,pc,char,[history('PC conversation','h2','2026-10-02T00:00:00Z')])
    merged=commit(c,pc,p5).json()
    assert {x['id'] for x in merged['items']}=={'m1','m2','h1','h2'}
    assert p5['download_counts']=={'profile':0,'memory':2,'history':1,'deletions':0}


def test_conflict_choices_are_bound_to_device_and_revision(client):
    c=client; a=pair(c); b=pair(c)
    char=c.post('/sync/characters',headers=auth(a),json={'name':'C'}).json()['id']
    first=commit(c,a,plan(c,a,char,[item('base')])).json()['items'][0]['version']
    commit(c,b,plan(c,b,char,[item('PC changed',base=first)]))
    conflict=plan(c,a,char,[item('Phone changed',base=first)])
    assert conflict['conflicts'][0]['key']=='memory:m1'
    assert commit(c,b,conflict,{'memory:m1':'local'}).status_code==409
    assert commit(c,a,conflict).status_code==422
    resolved=commit(c,a,conflict,{'memory:m1':'remote'}).json()
    assert resolved['items'][0]['value']['content']=='PC changed'
    assert commit(c,a,conflict,{'memory:m1':'local'}).status_code==409
    stale=plan(c,a,char,[item('later','m2')])
    commit(c,b,plan(c,b,char,[item('another','m3')]))
    assert commit(c,a,stale).status_code==409


def test_deletion_survives_offline_reconnect_and_conflicts_with_edit(client):
    c=client; a=pair(c); b=pair(c)
    char=c.post('/sync/characters',headers=auth(a),json={'name':'C'}).json()['id']
    version=commit(c,a,plan(c,a,char,[item('base')])).json()['items'][0]['version']
    removed=commit(c,a,plan(c,a,char,[item('',base=version,deleted=True)])).json()
    assert removed['items'][0]['deleted']
    offline=plan(c,b,char,[item('base',base=version,changed=False)])
    assert not offline['conflicts']
    assert commit(c,b,offline).json()['items'][0]['deleted']
    edited=plan(c,b,char,[item('offline edit',base=version)])
    assert len(edited['conflicts'])==1


def test_character_boundaries_and_unknown_fields(client):
    c=client; d=pair(c)
    one=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    two=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    commit(c,d,plan(c,d,one,[item('private-one')]))
    assert commit(c,d,plan(c,d,two,[])).json()['items']==[]
    invalid=item('text');invalid['value']['api_key']='forbidden'
    assert c.post('/sync/plan',headers=auth(d),json={'character_id':one,'items':[invalid]}).status_code==422
    assert c.post('/sync/plan',headers=auth(d),json={'character_id':one,'items':[item('a'),item('b')]}).status_code==422


def test_server_identity_survives_reopen_but_not_new_database(client, tmp_path):
    from app.core.device_sync import SyncStore
    first=pair(client)
    assert first['server_id']==SyncStore(get_settings().database_url).server_id
    assert client.get('/sync/characters',headers=auth(first)).json()['server_id']==first['server_id']
    assert SyncStore('sqlite:///'+str(tmp_path/'replacement.db')).server_id!=first['server_id']


def test_profile_conflict_and_validation_preserve_shared_identity(client):
    c=client;a=pair(c);b=pair(c,'PC')
    char=c.post('/sync/characters',headers=auth(a),json={'name':'Original'}).json()['id']
    def profile(text,base=0):return {'kind':'profile','id':'name','base_version':base,'value':{'text':text}}
    first=commit(c,a,plan(c,a,char,[profile('First')])).json()['items'][0]['version']
    commit(c,b,plan(c,b,char,[profile('PC name',first)]))
    p=plan(c,a,char,[profile('Phone name',first)])
    result=commit(c,a,p,{'profile:name':'remote'}).json()
    assert result['character_id']==char and result['items'][0]['value']['text']=='PC name'
    for invalid in [profile(''),profile('x'*257),{'kind':'profile','id':'name','deleted':True}]:
        assert c.post('/sync/plan',headers=auth(a),json={'character_id':char,'items':[invalid]}).status_code==422


def test_expired_plan_and_future_baseline_do_not_change_shared_data(client):
    c=client;d=pair(c);char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    p=plan(c,d,char,[item('pending')])
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute('UPDATE sync_plans SET expires=0 WHERE id=?',(p['plan_id'],))
    assert commit(c,d,p).status_code==409
    assert c.post('/sync/plan',headers=auth(d),json={'character_id':char,'items':[item('future',base=8)]}).status_code==409
    assert commit(c,d,plan(c,d,char,[])).json()['items']==[]
