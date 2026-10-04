import json
import sqlite3
from uuid import uuid4
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
    assert 'offline edit' not in json.dumps(edited)
    assert commit(c,b,edited,{'memory:m1':'local'}).status_code==409
    assert commit(c,b,edited,{'memory:m1':'remote'}).json()['items'][0]['deleted']


def test_deleted_item_cannot_return_from_committed_retry_or_same_id(client):
    c=client; a=pair(c); b=pair(c,'PC')
    char=c.post('/sync/characters',headers=auth(a),json={'name':'C'}).json()['id']
    original=plan(c,a,char,[item('FORGOTTEN_TEXT')])
    saved=commit(c,a,original).json()
    version=saved['items'][0]['version']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute("INSERT INTO sync_backend_responses VALUES(?,?,?,?)",(char,'cached','signature','FORGOTTEN_TEXT'))
    deletion=plan(c,b,char,[item('',base=version,deleted=True)])
    assert commit(c,b,deletion).json()['items'][0]['deleted']
    replay=commit(c,a,original)
    assert replay.status_code==200
    assert 'FORGOTTEN_TEXT' not in replay.text
    assert replay.json()['items'][0]['deleted']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        stored=db.execute('SELECT payload,result FROM sync_plans WHERE id=?',(original['plan_id'],)).fetchone()
        assert 'FORGOTTEN_TEXT' not in ''.join(stored)
        assert db.execute('SELECT 1 FROM sync_backend_responses WHERE character_id=?',(char,)).fetchone() is None
    fresh=plan(c,a,char,[item('new text',base=version+1)])
    assert len(fresh['conflicts'])==1
    assert commit(c,a,fresh,{'memory:m1':'local'}).status_code==409
    assert commit(c,a,fresh,{'memory:m1':'remote'}).json()['items'][0]['deleted']


def test_legacy_cached_plan_migration_scrubs_text_and_invalidates_pending(client):
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    old_plan=plan(c,d,char,[item('OLD_PRIVATE_TEXT')])
    old_snapshot=commit(c,d,old_plan).json()
    assert commit(c,d,plan(c,d,char,[item('',base=old_snapshot['items'][0]['version'],deleted=True)])).json()['items'][0]['deleted']
    pending=plan(c,d,char,[item('PENDING_TEXT','other')])
    path=get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        db.execute('UPDATE sync_plans SET payload=?,result=? WHERE id=?',
                   (json.dumps({'changes':[{'value':{'content':'OLD_PRIVATE_TEXT'}}]}),
                    json.dumps({'choices':{},'snapshot':old_snapshot}),old_plan['plan_id']))
        db.execute("DELETE FROM sync_metadata WHERE key='v1_plan_privacy_migrated'")
    from app.core.device_sync import SyncStore
    SyncStore(get_settings().database_url)
    with sqlite3.connect(path) as db:
        assert db.execute('SELECT 1 FROM sync_plans WHERE id=?',(pending['plan_id'],)).fetchone() is None
        payload,result=db.execute('SELECT payload,result FROM sync_plans WHERE id=?',(old_plan['plan_id'],)).fetchone()
        assert payload=='{}' and 'OLD_PRIVATE_TEXT' not in result
    assert commit(c,d,old_plan).json()['items'][0]['deleted']


def test_companion_migration_preview_is_read_only_and_flags_bad_sources(client):
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    original=commit(c,d,plan(c,d,char,[item('private memory'),history('hello','h1','2026-10-03T01:00:00Z')])).json()
    before=original['revision']
    path=f'/admin/api/sync/characters/{char}/companion-dry-run'
    first=c.get(path)
    assert first.status_code==200
    report=first.json()
    assert report['counts']['memory']==1 and report['counts']['history']==1
    assert report['counts']['roles_user']==1
    assert report['ready_for_mapping'] and not report['writes_performed']
    assert 'private memory' not in first.text and 'hello' not in first.text
    assert c.get(path).json()['legacy_digest']==report['legacy_digest']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT revision FROM sync_characters WHERE id=?',(char,)).fetchone()[0]==before
        db.execute("INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)",(char,'memory','broken',before+1,0,json.dumps({
            'content':'derived','pinned':False,'recorded_utc':'',
            'source_ids':['m1','missing'],'source_versions':[1,1],
            'basis':'user_confirmed_connection'}),'test'))
    second=c.get(path).json()
    assert not second['ready_for_mapping']
    assert any(x['code']=='source_unavailable' for x in second['issues'])


def test_companion_migration_preview_rejects_nonobject_and_unknown_speaker(client):
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    path=get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        db.execute("INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)",
                   (char,'memory','bad',1,0,'[]','test'))
        value=history('private','h1','2026-10-03T01:00:00Z')['value']
        value['speaker']='Unknown person'
        db.execute("INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)",
                   (char,'history','h1',1,0,json.dumps(value),'test'))
    result=c.get(f'/admin/api/sync/characters/{char}/companion-dry-run')
    assert result.status_code==200
    assert not result.json()['ready_for_mapping']
    assert {issue['code'] for issue in result.json()['issues']}=={'invalid_value','ambiguous_role'}
    assert 'private' not in result.text


def test_companion_migration_rejects_conflicting_role_evidence_even_with_override(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    value = history('private statement', 'legacy:turn:user', '2026-10-03T01:00:00Z')['value']
    value['speaker'] = 'Assistant'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute('INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)',
                   (character, 'history', 'legacy:turn:user', 1, 0, json.dumps(value), 'test'))
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert [issue['code'] for issue in report['issues']] == ['conflicting_role']
    assert report['counts']['roles_user'] == report['counts']['roles_assistant'] == 0
    assert not report['ready_for_mapping']
    assert 'private statement' not in json.dumps(report)
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    result = c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import', json={
        'expected_digest': report['legacy_digest'],
        'role_overrides': {'legacy:turn:user': 'user'},
    })
    assert result.status_code == 409
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute("SELECT count(*) FROM sqlite_master WHERE name='companion_v2_records'").fetchone()[0] == 0


def test_shadow_import_preserves_legacy_roles_sources_tombstones_and_is_idempotent(client, monkeypatch):
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    db_path=get_settings().database_url.removeprefix('sqlite:///')
    now='2026-10-03T01:00:00Z'
    entries=[
        ('profile','name',{'text':'Yui'},0),
        ('history','z-user',history('question','z-user',now)['value'],0),
        ('history','a-assistant',{**history('answer','a-assistant',now)['value'],
                                  'speaker':'Assistant','conversation_id':'conversation:z-user',
                                  'turn_id':'turn:z-user'},0),
        ('history','gone',{},1),
        ('memory','m1',item('mango')['value'],0),
        ('memory','m2',item('ginger','m2')['value'],0),
        ('memory','m3',{'content':'both were tasty','pinned':True,'recorded_utc':now,
                        'source_ids':['m1','m2'],'source_versions':[1,1],
                        'basis':'user_confirmed_connection'},0),
        ('memory','gone',{},1),
    ]
    with sqlite3.connect(db_path) as db:
        for kind,legacy_id,value,deleted in entries:
            db.execute('INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)',
                       (char,kind,legacy_id,1,deleted,json.dumps(value),'test'))
    preview=c.get(f'/admin/api/sync/characters/{char}/companion-dry-run').json()
    assert preview['ready_for_mapping'], preview['issues']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED','true')
    get_settings.cache_clear()
    endpoint=f'/admin/api/sync/characters/{char}/companion-shadow-import'
    assert c.post(endpoint,json={'expected_digest':'0'*64}).status_code==409
    imported=c.post(endpoint,json={'expected_digest':preview['legacy_digest']})
    assert imported.status_code==200, imported.text
    assert imported.json()['status']=='shadow_imported'
    assert c.post(endpoint,json={'expected_digest':preview['legacy_digest']}).json()['status']=='already_imported'
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_v2 import CompanionStore
    projected=project_legacy_snapshot(CompanionStore(get_settings().database_url),d['device_id'],char)
    projected_items={(x['kind'],x['id']):x for x in projected['items']}
    with sqlite3.connect(db_path) as db:
        original={(kind,legacy_id):(bool(deleted),value)
                  for kind,legacy_id,deleted,value in db.execute(
                      'SELECT kind,id,deleted,value_json FROM sync_items WHERE character_id=?',(char,))}
        metadata=' '.join(row[0] for row in db.execute(
            'SELECT metadata FROM companion_v2_legacy_metadata WHERE character_id=?',(char,)))
        assert 'question' not in metadata and 'answer' not in metadata
        assert 'mango' not in metadata and 'ginger' not in metadata
    assert set(projected_items)==set(original)
    for key,(deleted,value_json) in original.items():
        assert projected_items[key]['deleted']==deleted
        assert projected_items[key]['value']==({} if deleted else json.loads(value_json))
    page=c.get(f'/companion/v2/characters/{char}/snapshot',headers=auth(d))
    assert page.status_code==200, page.text
    items=page.json()['items']
    assert any(x['entity_type']=='profile' and x['profile']['text']=='Yui' for x in items)
    assert any(x['entity_type']=='record' and x['record'] and x['record']['kind']=='user_utterance'
               and x['record']['text']=='question' for x in items)
    assert any(x['entity_type']=='record' and x['record'] and x['record']['kind']=='assistant_utterance'
               and x['record']['text']=='answer' for x in items)
    changes=c.get(f'/companion/v2/characters/{char}/changes',headers=auth(d)).json()['items']
    dialogue=[x['record']['text'] for x in changes if x['entity_type']=='record' and x['record'] and
              x['record']['text'] in {'question','answer'}]
    assert dialogue==['question','answer']
    derived=[x for x in items if x['entity_type']=='memory' and x['memory'] and
             x['memory']['text']=='both were tasty'][0]
    assert derived['memory']['basis']=='legacy_unknown' and derived['memory']['pinned']
    assert len(derived['memory']['source_refs'])==2
    assert any(x['entity_type']=='record' and x['deleted'] for x in items)
    assert any(x['entity_type']=='memory' and x['state']=='deleted' for x in items)
    with sqlite3.connect(db_path) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',(char,)).fetchone()[0]==len(entries)
        db.execute("UPDATE sync_items SET value_json=? WHERE character_id=? AND kind='memory' AND id='m1'",
                   (json.dumps(item('changed')['value']),char))
    assert c.post(endpoint,json={'expected_digest':preview['legacy_digest']}).status_code==409
    after_drift=project_legacy_snapshot(CompanionStore(get_settings().database_url),d['device_id'],char)
    assert next(x for x in after_drift['items'] if x['kind']=='memory' and x['id']=='m1')['value']['content']=='mango'


def test_shadow_import_requires_explicit_ambiguous_role_mapping(client, monkeypatch):
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    value=history('hello','h1','2026-10-03T01:00:00Z')['value']
    value['speaker']='Old avatar name'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute('INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)',
                   (char,'history','h1',1,0,json.dumps(value),'test'))
    report=c.get(f'/admin/api/sync/characters/{char}/companion-dry-run').json()
    assert not report['ready_for_mapping']
    assert [x['code'] for x in report['issues']]==['ambiguous_role']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED','true')
    get_settings.cache_clear()
    endpoint=f'/admin/api/sync/characters/{char}/companion-shadow-import'
    assert c.post(endpoint,json={'expected_digest':report['legacy_digest']}).status_code==409
    accepted=c.post(endpoint,json={'expected_digest':report['legacy_digest'],
                                   'role_overrides':{'h1':'assistant'}})
    assert accepted.status_code==200, accepted.text
    assert c.post(endpoint,json={'expected_digest':report['legacy_digest'],
                                 'role_overrides':{'h1':'user'}}).status_code==409
    page=c.get(f'/companion/v2/characters/{char}/snapshot',headers=auth(d)).json()
    record=[x['record'] for x in page['items'] if x['entity_type']=='record'][0]
    assert record['kind']=='assistant_utterance' and record['author']=='legacy:assistant'
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_v2 import CompanionStore
    projected = project_legacy_snapshot(CompanionStore(get_settings().database_url), d['device_id'], char)
    assert projected['items'][0]['value']['speaker'] == 'Old avatar name'


def test_legacy_projection_reads_v2_corrections_without_changing_v1_source(client, monkeypatch):
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_v2 import CompanionStore
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'Original'}).json()['id']
    created = commit(c, device, plan(c, device, character, [
        {'kind': 'profile', 'id': 'name', 'value': {'text': 'Original'}},
        history('first', 'old:user', '2026-10-03T01:00:00Z'),
    ]))
    assert created.status_code == 200
    original = created.json()['items']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    snapshot = c.get(f'/companion/v2/characters/{character}/snapshot', headers=auth(device)).json()
    profile = next(x for x in snapshot['items'] if x['entity_type'] == 'profile')
    conversation = next(x for x in snapshot['items'] if x['entity_type'] == 'conversation')
    new_id = uuid4().hex
    operations = [
        {'op_id': uuid4().hex, 'type': 'update_profile', 'entity_id': profile['entity_id'],
         'expected_revision': 1, 'payload': {'field': 'name', 'text': 'Corrected'}},
        {'op_id': uuid4().hex, 'type': 'append_record', 'entity_id': new_id,
         'expected_revision': 0, 'payload': {'kind': 'user_utterance',
         'conversation_id': conversation['entity_id'], 'turn_id': uuid4().hex,
         'text': 'second', 'recorded_at': '2026-10-04T01:00:00+09:00', 'realm': 'real'}},
    ]
    result = c.post(f'/companion/v2/characters/{character}/commit', headers=auth(device),
                    json={'batch_id': uuid4().hex, 'operations': operations})
    assert result.status_code == 200, result.text
    projected = project_legacy_snapshot(CompanionStore(get_settings().database_url),
                                        device['device_id'], character)
    items = {(item['kind'], item['id']): item for item in projected['items']}
    assert items['profile', 'name']['value']['text'] == 'Corrected'
    assert items['profile', 'name']['version'] == next(x['version'] for x in original if x['kind'] == 'profile') + 1
    assert items['history', 'v2:' + new_id]['value']['text'] == 'second'
    assert items['history', 'v2:' + new_id]['value']['mode'] == 'talk'
    assert items['history', 'old:user']['value']['conversation_id'] == 'conversation:old:user'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert json.loads(db.execute("SELECT value_json FROM sync_items WHERE character_id=? AND kind='profile'",
                                     (character,)).fetchone()[0])['text'] == 'Original'


def test_legacy_projection_refuses_to_flatten_changed_memory_basis(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_v2 import CompanionStore
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    assert commit(c, device, plan(c, device, character, [item('private memory')])).status_code == 200
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    snapshot = c.get(f'/companion/v2/characters/{character}/snapshot', headers=auth(device)).json()
    memory_row = next(x for x in snapshot['items'] if x['entity_type'] == 'memory')
    changed = dict(memory_row['memory'], basis='ai_inference')
    response = c.post(f'/companion/v2/characters/{character}/commit', headers=auth(device),
                      json={'batch_id': uuid4().hex, 'operations': [{
                          'op_id': uuid4().hex, 'type': 'put_memory',
                          'entity_id': memory_row['entity_id'], 'expected_revision': 1,
                          'payload': changed,
                      }]})
    assert response.status_code == 200, response.text
    with pytest.raises(HTTPException) as failure:
        project_legacy_snapshot(CompanionStore(get_settings().database_url),
                                device['device_id'], character)
    assert failure.value.status_code == 409
    assert failure.value.detail['code'] == 'memory_basis_unrepresentable'


def test_prepared_legacy_profile_and_history_writes_replay_into_v2(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    name = Item.model_validate({'kind': 'profile', 'id': 'name', 'value': {'text': 'Updated name'}})
    prepared_name = prepare_legacy_change(store, device['device_id'], character, uuid4().hex, name)
    first = apply_prepared_legacy_change(store, device['device_id'], prepared_name)
    assert apply_prepared_legacy_change(store, device['device_id'], prepared_name) == first
    utterance = Item.model_validate(history('private hello', 'h1:user', '2026-10-04T01:00:00Z'))
    prepared_history = prepare_legacy_change(store, device['device_id'], character,
                                             uuid4().hex, utterance)
    accepted = apply_prepared_legacy_change(store, device['device_id'], prepared_history)
    assert apply_prepared_legacy_change(store, device['device_id'], prepared_history) == accepted
    projected = project_legacy_snapshot(store, device['device_id'], character)
    items = {(x['kind'], x['id']): x for x in projected['items']}
    assert items['profile', 'name']['value']['text'] == 'Updated name'
    assert items['history', 'h1:user']['value'] == utterance.value
    assert items['history', 'h1:user']['origin_device'] == device['device_id']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        metadata = ' '.join(row[0] for row in db.execute(
            'SELECT metadata FROM companion_v2_legacy_metadata WHERE character_id=?', (character,)))
        assert 'private hello' not in metadata
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?', (character,)).fetchone()[0] == 0
    corrected = Item.model_validate({**history('private corrected', 'h1:user', '2026-10-04T01:00:00Z'),
                                     'base_version': 1})
    prepared_correction = prepare_legacy_change(store, device['device_id'], character,
                                                uuid4().hex, corrected)
    apply_prepared_legacy_change(store, device['device_id'], prepared_correction)
    after = project_legacy_snapshot(store, device['device_id'], character)
    changed = next(x for x in after['items'] if x['kind'] == 'history')
    assert changed['version'] == 2 and changed['value']['text'] == 'private corrected'
    sourced = item('memory change')
    sourced['value'].update(source_ids=['source-a', 'source-b'], source_versions=[1, 1],
                            basis='user_confirmed_connection')
    with pytest.raises(HTTPException) as failure:
        prepare_legacy_change(store, device['device_id'], character, uuid4().hex,
                              Item.model_validate(sourced))
    assert failure.value.status_code == 409
    assert failure.value.detail['code'] == 'legacy_connection_source_stale'


def test_prepared_legacy_batch_shares_conversation_and_replays(client, monkeypatch):
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_legacy_write import prepare_legacy_changes, apply_prepared_legacy_changes
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    first = history('first private text', 'h1:user', '2026-10-04T01:00:00Z')
    second = history('second private text', 'h1:assistant', '2026-10-04T01:00:01Z')
    second['value']['speaker'] = 'Assistant'
    second['value']['conversation_id'] = first['value']['conversation_id']
    prepared = prepare_legacy_changes(store, device['device_id'], character, uuid4().hex,
                                      [Item.model_validate(first), Item.model_validate(second)])
    assert [x['type'] for x in prepared['operations']].count('create_conversation') == 1
    result = apply_prepared_legacy_changes(store, device['device_id'], prepared)
    assert apply_prepared_legacy_changes(store, device['device_id'], prepared) == result
    snapshot = project_legacy_snapshot(store, device['device_id'], character)
    histories = [x for x in snapshot['items'] if x['kind'] == 'history']
    assert {x['id'] for x in histories} == {'h1:user', 'h1:assistant'}
    assert {x['value']['text'] for x in histories} == {'first private text', 'second private text'}
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_conversations WHERE character_id=?',
                          (character,)).fetchone()[0] == 1


def test_prepared_legacy_mapping_failure_rolls_back_v2_commit(client, monkeypatch):
    from app.core.companion_legacy_write import prepare_legacy_changes, apply_prepared_legacy_changes
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    prepared = prepare_legacy_changes(store, device['device_id'], character, uuid4().hex,
                                      [Item.model_validate(history('secret', 'h1:user', '2026-10-04T01:00:00Z'))])
    path = get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        db.execute("CREATE TRIGGER fail_legacy_map BEFORE INSERT ON companion_v2_legacy_map "
                   "BEGIN SELECT RAISE(ABORT, 'mapping failure'); END")
    with pytest.raises(sqlite3.IntegrityError):
        apply_prepared_legacy_changes(store, device['device_id'], prepared)
    with sqlite3.connect(path) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM companion_v2_batches WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_prepared_legacy_plain_memory_deletion_forgets_its_source(client, monkeypatch):
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_shadow_migration import _mapped_id
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    commit(c, device, plan(c, device, character, [item('private memory')]))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    before = project_legacy_snapshot(store, device['device_id'], character)
    memory = next(x for x in before['items'] if x['kind'] == 'memory')
    deletion = Item(kind='memory', id=memory['id'], base_version=memory['version'],
                    deleted=True, value={})
    prepared = prepare_legacy_change(store, device['device_id'], character, uuid4().hex, deletion)
    result = apply_prepared_legacy_change(store, device['device_id'], prepared)
    assert apply_prepared_legacy_change(store, device['device_id'], prepared) == result
    after = project_legacy_snapshot(store, device['device_id'], character)
    tombstone = next(x for x in after['items'] if x['kind'] == 'memory')
    assert tombstone['id'] == memory['id'] and tombstone['deleted'] and tombstone['value'] == {}
    source_id = _mapped_id(store.sync.server_id, character, 'memory_source', memory['id'])
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT deleted,payload FROM companion_v2_records WHERE id=?',
                          (source_id,)).fetchone() == (1, None)
        assert db.execute('SELECT count(*) FROM companion_v2_record_revisions WHERE record_id=?',
                          (source_id,)).fetchone()[0] == 0


def test_prepared_legacy_plain_memory_create_edit_and_delete(client, monkeypatch):
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    original = Item.model_validate(item('private original'))
    created = prepare_legacy_change(store, device['device_id'], character, uuid4().hex, original)
    apply_prepared_legacy_change(store, device['device_id'], created)
    before = project_legacy_snapshot(store, device['device_id'], character)
    memory = next(x for x in before['items'] if x['kind'] == 'memory')
    assert memory['value'] == original.value
    updated = original.model_copy(update={'base_version': memory['version'],
                                          'value': {**original.value, 'content': 'private revised'}})
    prepared = prepare_legacy_change(store, device['device_id'], character, uuid4().hex, updated)
    apply_prepared_legacy_change(store, device['device_id'], prepared)
    after = project_legacy_snapshot(store, device['device_id'], character)
    memory2 = next(x for x in after['items'] if x['kind'] == 'memory')
    assert memory2['version'] == memory['version'] + 1
    assert memory2['value']['content'] == 'private revised'
    deletion = Item(kind='memory', id=memory2['id'], base_version=memory2['version'], deleted=True)
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, deletion))
    final = project_legacy_snapshot(store, device['device_id'], character)
    assert next(x for x in final['items'] if x['kind'] == 'memory')['deleted']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute("SELECT count(*) FROM companion_v2_records WHERE character_id=? AND "
                          "payload LIKE '%private original%'", (character,)).fetchone()[0] == 0
        assert db.execute("SELECT count(*) FROM companion_v2_records WHERE character_id=? AND "
                          "payload LIKE '%private revised%'", (character,)).fetchone()[0] == 0


def test_isolated_legacy_v2_plan_commit_and_retry(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    request = PlanRequest(character_id=character, items=[
        Item.model_validate(history('private first', 'h1:user', '2026-10-04T01:00:00Z')),
        Item.model_validate(item('private memory')),
    ])
    planned = adapter.plan(device['device_id'], request)
    assert planned['upload']['history'] == 1 and planned['upload']['memory'] == 1
    # Production v1 endpoint must never apply this test plan to sync_items.
    refused = c.post('/sync/commit', headers=auth(device),
                     json={'plan_id': planned['plan_id'], 'choices': {}})
    assert refused.status_code == 409
    result = adapter.commit(device['device_id'], planned['plan_id'], {})
    assert adapter.commit(device['device_id'], planned['plan_id'], {}) == result
    refused_again = c.post('/sync/commit', headers=auth(device),
                           json={'plan_id': planned['plan_id'], 'choices': {}})
    assert refused_again.status_code == 409
    assert {(x['kind'], x['id']) for x in result['items']} == {('history', 'h1:user'), ('memory', 'm1')}
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT payload FROM sync_plans WHERE id=?',
                          (planned['plan_id'],)).fetchone()[0] == '{}'


def test_isolated_legacy_v2_commit_recovers_after_lost_response(client, monkeypatch):
    import app.core.companion_legacy_adapter as module
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    plan_result = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('private once', 'h1:user', '2026-10-04T01:00:00Z'))]))
    original = module.project_legacy_snapshot
    calls = 0

    def interrupted(*args, **kwargs):
        nonlocal calls
        calls += 1
        if calls == 2:
            raise RuntimeError('response lost after v2 commit')
        return original(*args, **kwargs)

    monkeypatch.setattr(module, 'project_legacy_snapshot', interrupted)
    with pytest.raises(RuntimeError, match='response lost'):
        adapter.commit(device['device_id'], plan_result['plan_id'], {})
    monkeypatch.setattr(module, 'project_legacy_snapshot', original)
    recovered = adapter.commit(device['device_id'], plan_result['plan_id'], {})
    assert [x['value']['text'] for x in recovered['items'] if x['kind'] == 'history'] == ['private once']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 1


def test_isolated_legacy_v2_stale_plan_rejected_without_partial_write(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    a = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('first', 'a:user', '2026-10-04T01:00:00Z'))]))
    b = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('second', 'b:user', '2026-10-04T01:00:00Z'))]))
    adapter.commit(device['device_id'], a['plan_id'], {})
    with pytest.raises(HTTPException) as failure:
        adapter.commit(device['device_id'], b['plan_id'], {})
    assert failure.value.status_code == 409
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 1


def test_isolated_legacy_v2_two_devices_reconcile_and_revoke(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    phone, pc = pair(c), pair(c, 'PC')
    character = c.post('/sync/characters', headers=auth(phone), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    phone_plan = adapter.plan(phone['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(item('phone memory', 'phone'))]))
    pc_plan = adapter.plan(pc['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(item('pc memory', 'pc'))]))
    phone_result = adapter.commit(phone['device_id'], phone_plan['plan_id'], {})
    assert phone_result['revision'] > 0
    with pytest.raises(HTTPException) as stale:
        adapter.commit(pc['device_id'], pc_plan['plan_id'], {})
    assert stale.value.status_code == 409
    reconciled = adapter.plan(pc['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(item('pc memory', 'pc'))]))
    assert reconciled['download'] == 1
    result = adapter.commit(pc['device_id'], reconciled['plan_id'], {})
    assert {x['value']['content'] for x in result['items'] if x['kind'] == 'memory'} == {
        'phone memory', 'pc memory'}
    pending = adapter.plan(pc['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(item('later memory', 'later'))]))
    c.post('/admin/api/sync/devices/' + pc['device_id'] + '/revoke')
    with pytest.raises(HTTPException) as revoked:
        adapter.commit(pc['device_id'], pending['plan_id'], {})
    assert revoked.value.status_code == 401
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_memories WHERE character_id=?',
                          (character,)).fetchone()[0] == 2


def test_backend_console_v2_writer_is_atomic_and_has_no_external_device_token(client, monkeypatch):
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.device_sync import SyncStore
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    writer = CompanionBackendWriter(get_settings().database_url)
    assert writer.principal not in {x['id'] for x in c.get('/admin/api/sync/devices').json()['items']}
    assert c.post('/admin/api/sync/devices/' + writer.principal + '/revoke').status_code == 403
    when = '2026-10-04T01:00:00Z'
    values = [('history', 'backend:trial:user', history('private trial', 'backend:trial:user', when)['value']),
              ('memory', 'backend:trial', item('private memory', 'backend:trial')['value'])]
    def failure(db):
        raise RuntimeError('cache write failed')
    with pytest.raises(RuntimeError, match='cache write failed'):
        writer.append(character, uuid4().hex, values, finalize=failure)
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM companion_v2_memories WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
    written = writer.append(character, uuid4().hex, values)
    assert {(x['kind'], x['id']) for x in written['items']} == {
        ('history', 'backend:trial:user'), ('memory', 'backend:trial')}
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_backend_console_v2_connection_uses_source_versions(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.shared_conversation import MemoryConnection, ConfirmConnection, connection_draft, confirm_connection
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    writer = CompanionBackendWriter(get_settings().database_url)
    writer.append(character, uuid4().hex, [('memory', 'm1', item('first', 'm1')['value']),
                                           ('memory', 'm2', item('second', 'm2')['value'])])
    draft = connection_draft(writer, character, MemoryConnection(source_ids=['m1', 'm2']))
    assert draft['source_versions'] == [1, 1]
    writer.append(character, uuid4().hex, [('memory', 'm3', item('third', 'm3')['value'])])
    saved = confirm_connection(writer, character, ConfirmConnection(
        source_ids=draft['source_ids'], source_versions=draft['source_versions'], content='related'))
    assert saved['id'].startswith('connection:')
    memories = {x['id']: x for x in writer.snapshot(character)['items'] if x['kind'] == 'memory'}
    assert memories[saved['id']]['value']['source_ids'] == ['m1', 'm2']
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    adapter = LegacyV2Adapter(get_settings().database_url)
    revised = item('first revised', 'm1', base=1)
    changed = adapter.plan(device['device_id'], PlanRequest(
        character_id=character, items=[Item.model_validate(revised)]))
    adapter.commit(device['device_id'], changed['plan_id'], {})
    with pytest.raises(HTTPException) as stale:
        confirm_connection(writer, character, ConfirmConnection(
            source_ids=draft['source_ids'], source_versions=draft['source_versions'], content='stale'))
    assert stale.value.status_code == 409
    with pytest.raises(HTTPException) as duplicate:
        confirm_connection(writer, character, ConfirmConnection(
            source_ids=['m1', 'm1'], source_versions=[1, 1], content='invalid'))
    assert duplicate.value.status_code in {409, 422}


def test_backend_console_v2_chat_writes_once_and_reuses_cached_response(client, monkeypatch):
    import asyncio
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.shared_conversation import SharedChat, shared_chat
    from app.models.chat import ChatResponse
    from app.providers.router import ProviderRouter
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    calls = []
    class Provider:
        async def generate(self, request, history):
            calls.append((request, history))
            return ChatResponse(text='覚えました。', memory_action='none')
    monkeypatch.setattr(ProviderRouter, 'chat', lambda self: Provider())
    writer = CompanionBackendWriter(get_settings().database_url)
    body = SharedChat(request_id='v2-chat', session_id='talk-one', message='私は紅茶が好きです。')
    first = asyncio.run(shared_chat(writer, get_settings(), character, body))
    repeated = asyncio.run(shared_chat(writer, get_settings(), character, body))
    assert first == repeated and len(calls) == 1
    followup = SharedChat(request_id='v2-chat-followup', session_id='talk-one',
                          message='紅茶について何を覚えていますか？')
    asyncio.run(shared_chat(writer, get_settings(), character, followup))
    assert '私は紅茶が好きです。' in calls[-1][0].context.extra['character_memory']
    assert len(calls) == 2
    state = writer.snapshot(character)
    assert len([x for x in state['items'] if x['kind'] == 'history']) == 4
    assert len([x for x in state['items'] if x['kind'] == 'memory']) == 1
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        source = db.execute("SELECT entity_id FROM companion_v2_legacy_map WHERE character_id=? "
                            "AND kind='history' AND legacy_id='backend:v2-chat:user'",
                            (character,)).fetchone()[0]
        memory = db.execute("SELECT payload FROM companion_v2_memories WHERE character_id=?",
                            (character,)).fetchone()[0]
        assert json.loads(memory)['source_refs'][0]['record_id'] == source
        assert db.execute("SELECT count(*) FROM companion_v2_records WHERE character_id=? "
                          "AND json_extract(payload,'$.kind')='observation'",
                          (character,)).fetchone()[0] == 0

    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import Item, PlanRequest
    original = next(x for x in state['items'] if x['id'] == 'backend:v2-chat:user')
    adapter = LegacyV2Adapter(get_settings().database_url)
    removal = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item(kind='history', id=original['id'], base_version=original['version'],
             deleted=True, value={})]))
    adapter.commit(device['device_id'], removal['plan_id'], {})
    forgotten = writer.snapshot(character)
    assert next(x for x in forgotten['items'] if x['kind'] == 'memory')['deleted']


def test_backend_auto_memory_deletion_keeps_its_user_utterance(client, monkeypatch):
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import Item, PlanRequest
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    writer = CompanionBackendWriter(get_settings().database_url)
    utterance = history('私は紅茶が好きです。', 'backend:source:user', '2026-10-04T01:00:00Z')['value']
    memory = item('私は紅茶が好きです。', 'backend:source')['value']
    writer.append(character, uuid4().hex,
                  [('history', 'backend:source:user', utterance),
                   ('memory', 'backend:source', memory)])
    before = writer.snapshot(character)
    saved = next(x for x in before['items'] if x['kind'] == 'memory')
    adapter = LegacyV2Adapter(get_settings().database_url)
    deletion = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item(kind='memory', id=saved['id'], base_version=saved['version'],
             deleted=True, value={})]))
    adapter.commit(device['device_id'], deletion['plan_id'], {})
    after = writer.snapshot(character)
    assert next(x for x in after['items'] if x['kind'] == 'memory')['deleted']
    assert next(x for x in after['items'] if x['kind'] == 'history')['value']['text'] == '私は紅茶が好きです。'


def test_local_automatic_memory_uses_its_matching_history_as_v2_source(client, monkeypatch):
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import Item, PlanRequest
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    history_id = uuid4().hex
    memory_id = 'auto-history:' + history_id
    text = '私は紅茶が好きです。'
    committed = commit(c, device, plan(c, device, character, [
        history(text, history_id, '2026-10-04T01:00:00Z'), item(text, memory_id)])).json()
    assert {x['id'] for x in committed['items']} == {history_id, memory_id}
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    preview = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    imported = c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                      json={'expected_digest': preview['legacy_digest']})
    assert imported.status_code == 200, imported.text
    db_path = get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(db_path) as db:
        source = db.execute("SELECT entity_id FROM companion_v2_legacy_map WHERE character_id=? "
                            "AND kind='history' AND legacy_id=?", (character, history_id)).fetchone()[0]
        memory = json.loads(db.execute('SELECT payload FROM companion_v2_memories WHERE character_id=?',
                                       (character,)).fetchone()[0])
        assert memory['source_refs'] == [{'record_id': source, 'revision': 1, 'quote': None}]
        assert db.execute("SELECT count(*) FROM companion_v2_records WHERE character_id=? "
                          "AND json_extract(payload,'$.kind')='observation'", (character,)).fetchone()[0] == 0
    adapter = LegacyV2Adapter(get_settings().database_url)
    writer = CompanionBackendWriter(get_settings().database_url)
    snapshot = writer.snapshot(character)
    old_history = next(x for x in snapshot['items'] if x['id'] == history_id)
    deletion = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item(kind='history', id=history_id, base_version=old_history['version'], deleted=True)]))
    adapter.commit(device['device_id'], deletion['plan_id'], {})
    forgotten = writer.snapshot(character)
    assert next(x for x in forgotten['items'] if x['id'] == memory_id)['deleted']


def test_automatic_memory_without_matching_user_history_cannot_be_imported(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    memory_id = 'auto-history:' + uuid4().hex
    # Older protocol-1 data could contain an orphan before source enforcement.
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute('INSERT INTO sync_items VALUES(?,?,?,?,?,?,?)',
                   (character, 'memory', memory_id, 1, 0,
                    json.dumps(item('私は紅茶が好きです。')['value']), device['device_id']))
        db.execute('UPDATE sync_characters SET revision=1 WHERE id=?', (character,))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    preview = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert not preview['ready_for_mapping']
    assert {'item': 'memory:' + memory_id, 'code': 'automatic_memory_source_unavailable'} in preview['issues']
    response = c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                      json={'expected_digest': preview['legacy_digest']})
    assert response.status_code == 409
    assert response.json()['detail']['code'] == 'migration_preview_has_issues'


def test_v1_history_deletion_invalidates_matching_automatic_memory(client):
    c = client
    phone, pc = pair(c), pair(c, 'PC')
    character = c.post('/sync/characters', headers=auth(phone), json={'name': 'C'}).json()['id']
    history_id = uuid4().hex
    memory_id = 'auto-history:' + history_id
    text = '私は紅茶が好きです。'
    first = commit(c, phone, plan(c, phone, character, [
        history(text, history_id, '2026-10-04T01:00:00Z'), item(text, memory_id)])).json()
    source = next(x for x in first['items'] if x['id'] == history_id)
    deletion = {'kind': 'history', 'id': history_id, 'base_version': source['version'],
                'deleted': True, 'value': {}}
    after = commit(c, phone, plan(c, phone, character, [deletion])).json()
    assert next(x for x in after['items'] if x['id'] == history_id)['deleted']
    forgotten = next(x for x in after['items'] if x['id'] == memory_id)
    assert forgotten['deleted'] and forgotten['value'] == {}
    # A stale second device cannot restore either original ID.
    stale = plan(c, pc, character, [history(text, history_id, '2026-10-04T01:00:00Z'),
                                    item(text, memory_id)])
    assert {x['key'] for x in stale['conflicts']} == {'history:' + history_id, 'memory:' + memory_id}
    assert commit(c, pc, stale, {x['key']: 'local' for x in stale['conflicts']}).status_code == 409


def test_v1_rejects_new_automatic_memory_without_source(client):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    memory_id = 'auto-history:' + uuid4().hex
    result = c.post('/sync/plan', headers=auth(device),
                    json={'character_id': character, 'items': [item('私は紅茶が好きです。', memory_id)]})
    assert result.status_code == 422
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?', (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM sync_plans WHERE character_id=?', (character,)).fetchone()[0] == 0


def test_v1_history_correction_invalidates_stale_automatic_memory(client):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    history_id = uuid4().hex
    memory_id = 'auto-history:' + history_id
    text = '私は紅茶が好きです。'
    first = commit(c, device, plan(c, device, character, [
        history(text, history_id, '2026-10-04T01:00:00Z'), item(text, memory_id)])).json()
    old = next(x for x in first['items'] if x['id'] == history_id)
    corrected = history('私は緑茶が好きです。', history_id, '2026-10-04T01:00:00Z')
    corrected['base_version'] = old['version']
    after = commit(c, device, plan(c, device, character, [corrected])).json()
    assert next(x for x in after['items'] if x['id'] == memory_id)['deleted']


def test_v1_history_forget_also_invalidates_confirmed_connection(client):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    history_id = uuid4().hex
    automatic_id = 'auto-history:' + history_id
    text = '私は紅茶が好きです。'
    first = commit(c, device, plan(c, device, character, [
        history(text, history_id, '2026-10-04T01:00:00Z'),
        item(text, automatic_id), item('友人は緑茶が好きです。', 'manual')])).json()
    versions = {x['id']: x['version'] for x in first['items']}
    linked = item('飲み物の好みは人ごとに異なる。', 'connection:drinks')
    linked['value'].update(source_ids=[automatic_id, 'manual'],
                           source_versions=[versions[automatic_id], versions['manual']],
                           basis='user_confirmed_connection')
    commit(c, device, plan(c, device, character, [linked]))
    deletion = {'kind': 'history', 'id': history_id, 'base_version': versions[history_id],
                'deleted': True, 'value': {}}
    after = commit(c, device, plan(c, device, character, [deletion])).json()
    state = {x['id']: x for x in after['items']}
    assert state[automatic_id]['deleted'] and state['connection:drinks']['deleted']
    assert not state['manual']['deleted']


def test_new_local_automatic_memory_links_to_new_and_existing_v2_history(client, monkeypatch):
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import Item, PlanRequest
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    preview = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': preview['legacy_digest']}).status_code == 200
    writer = CompanionBackendWriter(get_settings().database_url)
    ids = [uuid4().hex, uuid4().hex]
    texts = ['私は紅茶が好きです。', '私は猫が好きです。']
    writer.append(character, uuid4().hex,
                  [('history', ids[0], history(texts[0], ids[0], '2026-10-04T01:00:00Z')['value']),
                   ('memory', 'auto-history:' + ids[0], item(texts[0])['value'])])
    writer.append(character, uuid4().hex,
                  [('history', ids[1], history(texts[1], ids[1], '2026-10-04T02:00:00Z')['value'])])
    writer.append(character, uuid4().hex,
                  [('memory', 'auto-history:' + ids[1], item(texts[1])['value'])])
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        for history_id in ids:
            source = db.execute("SELECT entity_id FROM companion_v2_legacy_map WHERE character_id=? "
                                "AND kind='history' AND legacy_id=?", (character, history_id)).fetchone()[0]
            memory_id = db.execute("SELECT entity_id FROM companion_v2_legacy_map WHERE character_id=? "
                                   "AND kind='memory' AND legacy_id=?",
                                   (character, 'auto-history:' + history_id)).fetchone()[0]
            payload = json.loads(db.execute('SELECT payload FROM companion_v2_memories WHERE id=?',
                                            (memory_id,)).fetchone()[0])
            assert payload['source_refs'][0]['record_id'] == source
        assert db.execute("SELECT count(*) FROM companion_v2_records WHERE character_id=? "
                          "AND json_extract(payload,'$.kind')='observation'", (character,)).fetchone()[0] == 0
    adapter = LegacyV2Adapter(get_settings().database_url)
    original = next(x for x in writer.snapshot(character)['items'] if x['id'] == ids[1])
    deletion = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item(kind='history', id=ids[1], base_version=original['version'], deleted=True)]))
    adapter.commit(device['device_id'], deletion['plan_id'], {})
    assert next(x for x in writer.snapshot(character)['items']
                if x['id'] == 'auto-history:' + ids[1])['deleted']


def test_backend_console_v2_legacy_import_is_copy_only(client, monkeypatch):
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.shared_conversation import LegacyImport, ConfirmImport, preview_legacy, import_legacy
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    writer = CompanionBackendWriter(get_settings().database_url)
    with writer.connect() as db:
        db.execute("INSERT INTO memories(user_id,character_id,content) VALUES('owner','old','private fact')")
        db.execute("INSERT INTO conversations(user_id,character_id,session_id,role,message) "
                   "VALUES('owner','old','session','user','private turn')")
    scope = LegacyImport(user_id='owner', character_id='old')
    preview = preview_legacy(writer, character, scope)
    assert len(preview['items']) == 2
    result = import_legacy(writer, character, ConfirmImport(**scope.model_dump(),
                                                            preview_hash=preview['preview_hash']))
    assert result['imported'] == 2
    assert preview_legacy(writer, character, scope)['items'] == []
    with writer.connect() as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute("SELECT count(*) FROM memories WHERE user_id='owner'").fetchone()[0] == 1


def test_trial_cutover_routes_phone_and_backend_console_to_one_v2_ledger(client, monkeypatch):
    from app.models.chat import ChatResponse
    from app.providers.router import ProviderRouter
    c = client
    phone, pc = pair(c), pair(c, 'PC')
    character = c.post('/sync/characters', headers=auth(phone), json={'name': 'C'}).json()['id']
    commit(c, phone, plan(c, phone, character, [item('before cutover', 'm1')]))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    activated = c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                       params={'device_id': phone['device_id']})
    assert activated.status_code == 200, activated.text
    assert activated.json()['mode'] == 'active_v2'
    from app.core.device_sync import SyncStore, PlanRequest, Item
    from fastapi import HTTPException
    with pytest.raises(HTTPException) as bypass:
        SyncStore(get_settings().database_url).plan(phone['device_id'], PlanRequest(
            character_id=character, items=[Item.model_validate(item('bypass', 'bypass'))]))
    assert bypass.value.detail['code'] == 'v1_writer_disabled_for_active_v2'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        old_count = db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                               (character,)).fetchone()[0]
    synced = commit(c, pc, plan(c, pc, character, [item('after cutover', 'm2')])).json()
    assert {x['value']['content'] for x in synced['items'] if x['kind'] == 'memory'} == {
        'before cutover', 'after cutover'}
    draft = c.post(f'/admin/api/sync/characters/{character}/connection-preview',
                   json={'source_ids': ['m1', 'm2']})
    assert draft.status_code == 200, draft.text
    link = c.post(f'/admin/api/sync/characters/{character}/connections',
                  json={**{key: draft.json()[key] for key in ('source_ids', 'source_versions')},
                        'content': 'Both records are related'})
    assert link.status_code == 200, link.text
    generated = []
    class Provider:
        async def generate(self, request, history):
            generated.append(request.request_id)
            return ChatResponse(text='了解しました。', memory_action='none')
    monkeypatch.setattr(ProviderRouter, 'chat', lambda self: Provider())
    body = {'request_id': 'routed-chat', 'session_id': 'routed-talk', 'message': '私は猫が好きです。'}
    chat = c.post(f'/admin/api/sync/characters/{character}/chat', json=body)
    assert chat.status_code == 200, chat.text
    assert c.post(f'/admin/api/sync/characters/{character}/chat', json=body).json() == chat.json()
    final = c.get(f'/admin/api/sync/characters/{character}').json()
    assert len([x for x in final['items'] if x['kind'] == 'history']) == 2
    assert len([x for x in final['items'] if x['kind'] == 'memory']) == 4
    assert next(x for x in final['items'] if x['id'] == link.json()['id'])['value']['source_ids'] == ['m1', 'm2']
    assert next(x for x in c.get('/sync/characters', headers=auth(pc)).json()['items']
                if x['id'] == character)['revision'] == final['revision']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == old_count
        assert db.execute('SELECT count(*) FROM sync_backend_responses WHERE character_id=?',
                          (character,)).fetchone()[0] == 1
    original = next(x for x in final['items'] if x['id'] == 'm1')
    forgotten = commit(c, phone, plan(c, phone, character, [
        item('', 'm1', base=original['version'], deleted=True)])).json()
    assert next(x for x in forgotten['items'] if x['id'] == 'm1')['deleted']
    assert next(x for x in forgotten['items'] if x['id'] == link.json()['id'])['deleted']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM sync_backend_responses WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
    assert c.post(f'/admin/api/sync/characters/{character}/chat', json=body).status_code == 409
    assert generated == ['routed-chat']
    later = commit(c, pc, plan(c, pc, character, [item('after forgetting', 'm4')])).json()
    assert next(x for x in later['items'] if x['id'] == 'm4')['value']['content'] == 'after forgetting'
    assert next(x for x in later['items'] if x['id'] == 'm2')['value']['content'] == 'after cutover'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_items WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_trial_cutover_requires_second_flag_and_rejects_drift(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    url = f'/admin/api/sync/characters/{character}/companion-trial-cutover'
    assert c.post(url, params={'device_id': device['device_id']}).status_code == 404
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    commit(c, device, plan(c, device, character, [item('late v1 write')]))
    blocked = c.post(url, params={'device_id': device['device_id']})
    assert blocked.status_code == 409
    assert 'legacy_changed_after_shadow' in blocked.json()['detail']['blockers']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_character_routing WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_trial_cutover_reverts_only_before_v2_writes(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    base = f'/admin/api/sync/characters/{character}'
    params = {'device_id': device['device_id']}
    assert c.post(base + '/companion-trial-cutover', params=params).status_code == 200
    assert c.post(base + '/companion-trial-cutover-revert', params=params).json()['mode'] == 'v1'
    assert c.post(base + '/companion-trial-cutover', params=params).status_code == 200
    commit(c, device, plan(c, device, character, [item('new v2 fact')]))
    refused = c.post(base + '/companion-trial-cutover-revert', params=params)
    assert refused.status_code == 409
    assert refused.json()['detail']['code'] == 'trial_cutover_has_writes'


def test_trial_cutover_rejects_shadow_without_compatibility_metadata(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    commit(c, device, plan(c, device, character, [item('original')]))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        db.execute("DELETE FROM companion_v2_legacy_metadata WHERE character_id=? AND kind='memory'",
                   (character,))
    refused = c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                     params={'device_id': device['device_id']})
    assert refused.status_code == 409
    assert 'legacy_metadata_incomplete' in refused.json()['detail']['blockers']


def test_v2_writers_recheck_character_route_inside_commit(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_backend_writer import CompanionBackendWriter
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    import app.core.companion_backend_writer as writer_module
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    assert c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                  params={'device_id': device['device_id']}).status_code == 200
    url = get_settings().database_url
    adapter = LegacyV2Adapter(url, require_active=True)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(item('pending', 'pending'))]))
    with sqlite3.connect(url.removeprefix('sqlite:///')) as db:
        db.execute('DELETE FROM sync_character_routing WHERE character_id=?', (character,))
    with pytest.raises(HTTPException) as stale:
        adapter.commit(device['device_id'], planned['plan_id'], {})
    assert stale.value.detail['code'] == 'companion_v2_route_changed'
    with sqlite3.connect(url.removeprefix('sqlite:///')) as db:
        db.execute("INSERT INTO sync_character_routing VALUES(?,'active_v2')", (character,))
    writer = CompanionBackendWriter(url, require_active=True)
    original_prepare = writer_module.prepare_legacy_changes
    def route_changes_after_prepare(*args, **kwargs):
        prepared = original_prepare(*args, **kwargs)
        with sqlite3.connect(url.removeprefix('sqlite:///')) as db:
            db.execute('DELETE FROM sync_character_routing WHERE character_id=?', (character,))
        return prepared
    monkeypatch.setattr(writer_module, 'prepare_legacy_changes', route_changes_after_prepare)
    with pytest.raises(HTTPException) as changed:
        writer.append(character, uuid4().hex, [('memory', 'backend-new', item('never committed')['value'])])
    assert changed.value.detail['code'] == 'companion_v2_route_changed'
    with sqlite3.connect(url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_memories WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_v2_legacy_adapter_preserves_custom_app_assistant_speaker(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'Yui'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    assert c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                  params={'device_id': device['device_id']}).status_code == 200
    app_history = history('assistant reply', uuid4().hex, '2026-10-04T01:00:00Z')
    app_history['value']['speaker'] = 'Yui'
    app_history['value']['conversation_id'] = 'local-talk'
    app_history['value']['turn_id'] = 'request-one'
    result = commit(c, device, plan(c, device, character, [app_history])).json()
    saved = next(x for x in result['items'] if x['kind'] == 'history')
    assert saved['value'] == app_history['value']
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        payload = db.execute('SELECT payload FROM companion_v2_records WHERE character_id=?',
                             (character,)).fetchone()[0]
    assert json.loads(payload)['kind'] == 'assistant_utterance'


def test_v2_sync_reconciles_app_archive_alias_with_backend_turn_and_tombstone(client, monkeypatch):
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'Yui'}).json()['id']
    when = '2026-10-04T01:00:00Z'
    user = history('same user turn', 'backend:req-one:user', when)
    assistant = history('same assistant turn', 'backend:req-one:assistant', when)
    for entry, speaker in ((user, 'You'), (assistant, 'Assistant')):
        entry['value'].update(speaker=speaker, conversation_id='local-session', turn_id='req-one')
    commit(c, device, plan(c, device, character, [user, assistant]))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    assert c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                  params={'device_id': device['device_id']}).status_code == 200
    local_user = {**user, 'id': uuid4().hex, 'value': {**user['value'], 'recorded_utc': '2026-10-04T01:00:01Z'}}
    local_assistant = {**assistant, 'id': uuid4().hex, 'value': {**assistant['value'], 'speaker': 'Yui'}}
    alias = plan(c, device, character, [local_user, local_assistant])
    assert alias['upload']['history'] == 0 and alias['download'] == 2
    merged = commit(c, device, alias).json()
    assert {x['id'] for x in merged['items'] if x['kind'] == 'history'} == {
        'backend:req-one:user', 'backend:req-one:assistant'}
    divergent = {**local_user, 'value': {**local_user['value'], 'text': 'different'}}
    conflict = c.post('/sync/plan', headers=auth(device),
                      json={'character_id': character, 'items': [divergent]})
    assert conflict.status_code == 409
    assert conflict.json()['detail']['code'] == 'backend_turn_identity_conflict'
    deleted = commit(c, device, plan(c, device, character, [
        {**user, 'base_version': 1, 'deleted': True, 'value': {}}])).json()
    assert next(x for x in deleted['items'] if x['id'] == user['id'])['deleted']
    stale = plan(c, device, character, [local_user])
    assert stale['upload']['history'] == 0
    after = commit(c, device, stale).json()
    assert next(x for x in after['items'] if x['id'] == user['id'])['deleted']
    assert all(x['id'] != local_user['id'] for x in after['items'])


def test_regular_chat_with_paired_shared_id_uses_v2_once_and_syncs_archive_alias(client, monkeypatch):
    from app.models.chat import ChatResponse
    from app.providers.router import ProviderRouter
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'Yui'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    monkeypatch.setenv('COMPANION_V2_CUTOVER_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    assert c.post(f'/admin/api/sync/characters/{character}/companion-trial-cutover',
                  params={'device_id': device['device_id']}).status_code == 200
    seen = []
    class Provider:
        name = 'test'
        async def generate(self, request, history):
            seen.append((request.mode, request.response_instruction, history,
                         dict(request.context.extra)))
            return ChatResponse(text='私も好きです。', memory_action='save')
    monkeypatch.setattr(ProviderRouter, 'chat', lambda self: Provider())
    body = {'request_id': 'paired-one', 'user_id': 'local-user', 'character_id': 'local-avatar',
            'shared_character_id': character, 'session_id': 'local-session',
            'message': '私は紅茶が好きです。', 'mode': 'talk',
            'response_instruction': '短く返して',
            'context': {'extra': {'character_memory': 'forgotten local memory',
                                  'recent_character_dialogue': 'forgotten local dialogue'}}}
    assert c.post('/chat', json=body).status_code == 401
    first = c.post('/chat', headers=auth(device), json=body)
    assert first.status_code == 200, first.text
    assert first.json()['shared_canonical'] is True
    assert c.post('/chat', headers=auth(device), json=body).json() == first.json()
    assert len(seen) == 1 and seen[0][:3] == ('standard', '短く返して', [])
    assert 'forgotten local' not in json.dumps(seen[0][3])
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM conversations WHERE request_id=?',
                          ('paired-one',)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM memories WHERE user_id=?',
                          ('local-user',)).fetchone()[0] == 0
    server = c.get(f'/admin/api/sync/characters/{character}').json()
    turns = [x for x in server['items'] if x['kind'] == 'history']
    assert len(turns) == 2
    local = [{**{'kind': 'history', 'id': uuid4().hex, 'base_version': 0, 'changed': True,
                 'deleted': False}, 'value': {**x['value'],
                    'speaker': 'You' if x['id'].endswith(':user') else 'Yui',
                    'recorded_utc': '2026-10-04T01:00:00Z'}} for x in turns]
    alias = plan(c, device, character, local)
    assert alias['upload']['history'] == 0 and alias['download'] >= 2
    merged = commit(c, device, alias).json()
    assert len([x for x in merged['items'] if x['kind'] == 'history']) == 2

    work = {**body, 'request_id': 'paired-work', 'mode': 'work',
            'message': 'この資料を調べてください。'}
    work_response = c.post('/chat', headers=auth(device), json=work)
    assert work_response.status_code == 200, work_response.text
    assert work_response.json()['shared_canonical'] is True
    assert seen[-1][0] == 'work' and len(seen[-1][2]) == 2
    server = c.get(f'/admin/api/sync/characters/{character}').json()
    assert {x['value']['mode'] for x in server['items'] if x['kind'] == 'history'
            and x['id'].startswith('backend:paired-work:')} == {'work'}

    before_secret = len(server['items'])
    secret = {**body, 'request_id': 'paired-secret', 'message': 'これは保存しないでください。',
              'secret': True}
    secret_response = c.post('/chat', headers=auth(device), json=secret)
    assert secret_response.status_code == 200, secret_response.text
    assert secret_response.json()['shared_canonical'] is True
    after_secret = c.get(f'/admin/api/sync/characters/{character}').json()
    assert len(after_secret['items']) == before_secret
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_backend_responses WHERE request_id=?',
                          ('paired-secret',)).fetchone()[0] == 0

    assert c.post(f"/admin/api/sync/devices/{device['device_id']}/revoke").status_code == 200
    assert c.post('/chat', headers=auth(device), json=work).status_code == 401


def test_legacy_connection_memory_tracks_original_versions(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_projection import project_legacy_snapshot
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    for source_id, content in [('m1', 'first original'), ('m2', 'second original')]:
        source = item(content)
        source['id'] = source_id
        apply_prepared_legacy_change(store, device['device_id'],
                                     prepare_legacy_change(store, device['device_id'], character,
                                                           uuid4().hex, Item.model_validate(source)))
    linked = item('confirmed connection')
    linked['id'] = 'connection:c1'
    linked['value'].update(source_ids=['m1', 'm2'], source_versions=[1, 1],
                           basis='user_confirmed_connection')
    prepared = prepare_legacy_change(store, device['device_id'], character, uuid4().hex,
                                     Item.model_validate(linked))
    apply_prepared_legacy_change(store, device['device_id'], prepared)
    snapshot = project_legacy_snapshot(store, device['device_id'], character)
    connection = next(x for x in snapshot['items'] if x['id'] == 'connection:c1')
    assert connection['value'] == linked['value']
    edited = Item.model_validate({**linked, 'base_version': 1,
                                  'value': {**linked['value'], 'content': 'updated connection'}})
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, edited))
    changed = project_legacy_snapshot(store, device['device_id'], character)
    assert next(x for x in changed['items'] if x['id'] == 'connection:c1')['value']['content'] == 'updated connection'
    stale = Item.model_validate({**linked, 'base_version': 2,
                                 'value': {**linked['value'], 'source_versions': [2, 1]}})
    with pytest.raises(HTTPException) as failure:
        prepare_legacy_change(store, device['device_id'], character, uuid4().hex, stale)
    assert failure.value.detail['code'] == 'legacy_connection_source_stale'
    deletion = Item(kind='memory', id='connection:c1', base_version=2, deleted=True)
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, deletion))
    remaining = project_legacy_snapshot(store, device['device_id'], character)
    assert next(x for x in remaining['items'] if x['id'] == 'connection:c1')['deleted']
    assert all(not x['deleted'] for x in remaining['items'] if x['id'] in {'m1', 'm2'})


def test_isolated_legacy_v2_batch_creates_sources_before_connection(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    sources = []
    for source_id, content in [('m1', 'first'), ('m2', 'second')]:
        source = item(content)
        source['id'] = source_id
        sources.append(Item.model_validate(source))
    linked = item('connection')
    linked['id'] = 'connection:c1'
    linked['value'].update(source_ids=['m1', 'm2'], source_versions=[1, 1],
                           basis='user_confirmed_connection')
    adapter = LegacyV2Adapter(get_settings().database_url)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character,
                                                            items=[Item.model_validate(linked), *sources]))
    result = adapter.commit(device['device_id'], planned['plan_id'], {})
    values = {x['id']: x['value'] for x in result['items'] if x['kind'] == 'memory'}
    assert values['connection:c1'] == linked['value']
    assert values['m1']['content'] == 'first' and values['m2']['content'] == 'second'


def test_isolated_legacy_v2_plan_checks_head_inside_commit(client, monkeypatch):
    import app.core.companion_legacy_adapter as module
    from fastapi import HTTPException
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.companion_v2 import Commit as V2Commit, Operation, ProfilePayload
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('must not commit', 'h1:user', '2026-10-04T01:00:00Z'))]))
    original = module.apply_prepared_legacy_changes

    def intervene(store, principal, prepared, **kwargs):
        from app.core.companion_shadow_migration import _mapped_id
        profile_id = _mapped_id(store.sync.server_id, character, 'profile', 'name')
        store.commit(principal, character, V2Commit(batch_id=uuid4().hex, operations=[
            Operation(op_id=uuid4().hex, type='update_profile', entity_id=profile_id,
                      expected_revision=0, payload=ProfilePayload(field='name', text='other update'))]))
        return original(store, principal, prepared, **kwargs)

    monkeypatch.setattr(module, 'apply_prepared_legacy_changes', intervene)
    with pytest.raises(HTTPException) as failure:
        adapter.commit(device['device_id'], planned['plan_id'], {})
    assert failure.value.detail['code'] == 'legacy_plan_revision_conflict'
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_isolated_legacy_v2_plan_over_public_operation_limit_is_atomic(client, monkeypatch):
    from pydantic import ValidationError
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.companion_v2 import Commit as V2Commit
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    requests = []
    for index in range(70):
        value = history(f'line {index}', f'h{index}:user', '2026-10-04T01:00:00Z')
        value['value']['conversation_id'] = 'shared-conversation'
        requests.append(Item.model_validate(value))
    adapter = LegacyV2Adapter(get_settings().database_url)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=requests))
    with pytest.raises(ValidationError):
        V2Commit.model_validate({'batch_id': uuid4().hex, 'operations': [
            {'op_id': uuid4().hex, 'type': 'delete_record', 'entity_id': uuid4().hex,
             'expected_revision': 1} for _ in range(70)]})
    result = adapter.commit(device['device_id'], planned['plan_id'], {})
    assert len([x for x in result['items'] if x['kind'] == 'history']) == 70
    assert adapter.commit(device['device_id'], planned['plan_id'], {}) == result
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 70


def test_isolated_legacy_v2_large_plan_rolls_back_on_late_failure(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    requests = []
    for index in range(70):
        value = history(f'line {index}', f'h{index}:user', '2026-10-04T01:00:00Z')
        value['value']['conversation_id'] = 'shared-conversation'
        requests.append(Item.model_validate(value))
    adapter = LegacyV2Adapter(get_settings().database_url)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=requests))
    path = get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        db.execute("CREATE TRIGGER fail_last_record BEFORE INSERT ON companion_v2_records "
                   "WHEN NEW.payload LIKE '%line 69%' BEGIN SELECT RAISE(ABORT, 'late failure'); END")
    with pytest.raises(sqlite3.IntegrityError, match='late failure'):
        adapter.commit(device['device_id'], planned['plan_id'], {})
    with sqlite3.connect(path) as db:
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM companion_v2_conversations WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT result FROM sync_plans WHERE id=?',
                          (planned['plan_id'],)).fetchone()[0] is None


def test_v2_privacy_change_discards_pending_legacy_adapter_text(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    store = CompanionStore(get_settings().database_url)
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, Item(kind='profile', id='name',
                                                                         value={'text': 'before'})))
    adapter = LegacyV2Adapter(get_settings().database_url)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('pending private text', 'h1:user', '2026-10-04T01:00:00Z'))]))
    path = get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        assert 'pending private text' in db.execute('SELECT payload FROM sync_plans WHERE id=?',
                                                    (planned['plan_id'],)).fetchone()[0]
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, Item(kind='profile', id='name',
                                                                         base_version=1,
                                                                         value={'text': 'after'})))
    with sqlite3.connect(path) as db:
        assert db.execute('SELECT count(*) FROM sync_plans WHERE id=?',
                          (planned['plan_id'],)).fetchone()[0] == 0


def test_isolated_legacy_v2_rejects_unreturnable_count_before_commit(client, monkeypatch):
    from fastapi import HTTPException
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    items = [Item(kind='history', id=f'h{i}:user', value={
        'text': 'synthetic', 'speaker': 'You', 'mode': 'talk',
        'recorded_utc': '2026-10-04T01:00:00Z', 'conversation_id': 'shared',
        'turn_id': f'turn:{i}'}) for i in range(18001)]
    adapter = LegacyV2Adapter(get_settings().database_url)
    with pytest.raises(HTTPException) as failure:
        adapter.plan(device['device_id'], PlanRequest(character_id=character, items=items))
    assert failure.value.status_code == 413
    with sqlite3.connect(get_settings().database_url.removeprefix('sqlite:///')) as db:
        assert db.execute('SELECT count(*) FROM sync_plans WHERE character_id=?',
                          (character,)).fetchone()[0] == 0
        assert db.execute('SELECT count(*) FROM companion_v2_records WHERE character_id=?',
                          (character,)).fetchone()[0] == 0


def test_legacy_result_size_preflight_rejects_bytes_before_write():
    from fastapi import HTTPException
    from app.core.companion_legacy_adapter import _check_legacy_result_size
    snapshot = {'server_id': 'a' * 32, 'character_id': 'b' * 32, 'revision': 0, 'items': []}
    selected = [{'kind': 'history', 'id': f'h{i}:user', 'deleted': False,
                 'value': {'text': 'x' * 1800, 'speaker': 'You', 'mode': 'talk',
                           'recorded_utc': '2026-10-04T01:00:00Z',
                           'conversation_id': 'shared', 'turn_id': f'turn:{i}'}}
                for i in range(5000)]
    with pytest.raises(HTTPException) as failure:
        _check_legacy_result_size(snapshot, selected)
    assert failure.value.status_code == 413
    assert failure.value.detail['code'] == 'legacy_projection_too_large'


def test_isolated_legacy_v2_source_and_connection_edit_same_plan(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    first = item('original one', id='m1')
    second = item('original two', id='m2')
    link = item('original connection', id='connection:c1')
    link['value'].update(source_ids=['m1', 'm2'], source_versions=[1, 1],
                         basis='user_confirmed_connection')
    initial = adapter.plan(device['device_id'], PlanRequest(character_id=character,
                      items=[Item.model_validate(x) for x in (first, second, link)]))
    adapter.commit(device['device_id'], initial['plan_id'], {})
    edited_source = {**first, 'base_version': 1,
                     'value': {**first['value'], 'content': 'new original one'}}
    edited_link = {**link, 'base_version': 1,
                   'value': {**link['value'], 'content': 'new connection',
                             'source_versions': [2, 1]}}
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character,
                      items=[Item.model_validate(x) for x in (edited_link, edited_source)]))
    snapshot = adapter.commit(device['device_id'], planned['plan_id'], {})
    memories = {x['id']: x for x in snapshot['items'] if x['kind'] == 'memory'}
    assert memories['m1']['version'] == 2 and memories['m1']['value']['content'] == 'new original one'
    assert memories['connection:c1']['version'] == 2
    assert memories['connection:c1']['value'] == edited_link['value']
    assert not memories['connection:c1']['deleted']


@pytest.mark.parametrize('linked_first', [False, True])
def test_isolated_legacy_v2_source_and_connection_delete_same_plan(client, monkeypatch, linked_first):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    link = item('connection', id='connection:c1')
    link['value'].update(source_ids=['m1', 'm2'], source_versions=[1, 1],
                         basis='user_confirmed_connection')
    initial = adapter.plan(device['device_id'], PlanRequest(character_id=character,
        items=[Item.model_validate(x) for x in (item('one', id='m1'), item('two', id='m2'), link)]))
    adapter.commit(device['device_id'], initial['plan_id'], {})
    source_delete = Item(kind='memory', id='m1', base_version=1, deleted=True)
    linked_delete = Item(kind='memory', id='connection:c1', base_version=1, deleted=True)
    selected = [linked_delete, source_delete] if linked_first else [source_delete, linked_delete]
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character, items=selected))
    result = adapter.commit(device['device_id'], planned['plan_id'], {})
    memories = {x['id']: x for x in result['items'] if x['kind'] == 'memory'}
    assert memories['m1']['deleted'] and memories['connection:c1']['deleted']
    assert not memories['m2']['deleted']


def test_isolated_legacy_v2_source_edit_invalidates_unedited_connection(client, monkeypatch):
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    adapter = LegacyV2Adapter(get_settings().database_url)
    link = item('connection', id='connection:c1')
    link['value'].update(source_ids=['m1', 'm2'], source_versions=[1, 1],
                         basis='user_confirmed_connection')
    initial = adapter.plan(device['device_id'], PlanRequest(character_id=character,
        items=[Item.model_validate(x) for x in (item('one', id='m1'), item('two', id='m2'), link)]))
    adapter.commit(device['device_id'], initial['plan_id'], {})
    changed_source = item('revised one', id='m1', base=1)
    planned = adapter.plan(device['device_id'], PlanRequest(character_id=character,
                                                            items=[Item.model_validate(changed_source)]))
    result = adapter.commit(device['device_id'], planned['plan_id'], {})
    memories = {x['id']: x for x in result['items'] if x['kind'] == 'memory'}
    assert memories['m1']['value']['content'] == 'revised one'
    assert memories['connection:c1']['deleted'] and memories['connection:c1']['value'] == {}


def test_read_only_cutover_inventory_detects_v1_and_v2_drift(client, monkeypatch):
    from app.core.companion_cutover import preview_cutover
    from app.core.companion_legacy_write import prepare_legacy_change, apply_prepared_legacy_change
    from app.core.companion_v2 import CompanionStore
    from app.core.device_sync import Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    commit(c, device, plan(c, device, character, [item('original')]))
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    url = get_settings().database_url
    clean = preview_cutover(url, device['device_id'], character)
    assert clean['status'] == 'inventory_matches' and clean['blockers'] == []
    assert clean['activation_ready'] is False
    assert clean['routing_blockers'] == ['device_sync_routes_use_v1', 'backend_console_routes_use_v1']
    assert clean['legacy_items'] == clean['projected_items'] == 1
    assert 'original' not in json.dumps(clean)
    commit(c, device, plan(c, device, character, [item('changed', base=1)]))
    drifted = preview_cutover(url, device['device_id'], character)
    assert {'legacy_changed_after_shadow', 'legacy_projection_differs'} <= set(drifted['blockers'])
    store = CompanionStore(url)
    apply_prepared_legacy_change(store, device['device_id'],
                                 prepare_legacy_change(store, device['device_id'], character,
                                                       uuid4().hex, Item(kind='profile', id='name',
                                                                         value={'text': 'v2 name'})))
    both = preview_cutover(url, device['device_id'], character)
    assert 'v2_changed_after_shadow' in both['blockers']


def test_read_only_cutover_inventory_flags_pending_plan(client, monkeypatch):
    from app.core.companion_cutover import preview_cutover
    from app.core.companion_legacy_adapter import LegacyV2Adapter
    from app.core.device_sync import PlanRequest, Item
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    monkeypatch.setenv('COMPANION_V2_TESTING_ENABLED', 'true')
    get_settings.cache_clear()
    report = c.get(f'/admin/api/sync/characters/{character}/companion-dry-run').json()
    assert c.post(f'/admin/api/sync/characters/{character}/companion-shadow-import',
                  json={'expected_digest': report['legacy_digest']}).status_code == 200
    url = get_settings().database_url
    adapter = LegacyV2Adapter(url)
    adapter.plan(device['device_id'], PlanRequest(character_id=character, items=[
        Item.model_validate(history('pending private text', 'h1:user', '2026-10-04T01:00:00Z'))]))
    inventory = preview_cutover(url, device['device_id'], character)
    assert inventory['status'] == 'blocked'
    assert inventory['blockers'] == ['pending_legacy_plans']
    assert 'pending private text' not in json.dumps(inventory)
    response = c.get(f'/admin/api/sync/characters/{character}/companion-cutover-preview',
                     params={'device_id': device['device_id']})
    assert response.status_code == 200 and response.json() == inventory
    assert 'pending private text' not in response.text
    unauthorized = c.get(f'/admin/api/sync/characters/{character}/companion-cutover-preview',
                         params={'device_id': uuid4().hex})
    assert unauthorized.status_code == 401


def test_cutover_preview_does_not_initialize_companion_tables(client, monkeypatch):
    from app.core.companion_cutover import preview_cutover
    c = client
    device = pair(c)
    character = c.post('/sync/characters', headers=auth(device), json={'name': 'C'}).json()['id']
    path = get_settings().database_url.removeprefix('sqlite:///')
    with sqlite3.connect(path) as db:
        before = {x[0] for x in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    report = preview_cutover(get_settings().database_url, device['device_id'], character)
    assert report['status'] == 'not_imported' and report['activation_ready'] is False
    with sqlite3.connect(path) as db:
        after = {x[0] for x in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    assert after == before


def test_uninitialized_sync_store_read_does_not_create_database(tmp_path):
    from app.core.device_sync import SyncStore
    from fastapi import HTTPException
    path = tmp_path / 'missing.db'
    with pytest.raises(HTTPException) as missing:
        SyncStore('sqlite:///' + str(path), initialize=False)
    assert missing.value.status_code == 404
    assert not path.exists()

    with sqlite3.connect(path):
        pass
    with pytest.raises(HTTPException) as empty:
        SyncStore('sqlite:///' + str(path), initialize=False)
    assert empty.value.status_code == 409


def test_shadow_import_rolls_back_all_v2_rows_on_write_failure(client):
    from app.core.companion_v2 import CompanionStore
    from app.core.companion_shadow_migration import shadow_import_character
    c=client; d=pair(c)
    char=c.post('/sync/characters',headers=auth(d),json={'name':'C'}).json()['id']
    commit(c,d,plan(c,d,char,[item('private memory')]))
    database_url=get_settings().database_url
    report=c.get(f'/admin/api/sync/characters/{char}/companion-dry-run').json()
    CompanionStore(database_url)
    with sqlite3.connect(database_url.removeprefix('sqlite:///')) as db:
        db.execute("CREATE TRIGGER fail_import BEFORE INSERT ON companion_v2_memories "
                   "BEGIN SELECT RAISE(ABORT,'injected failure'); END")
    with pytest.raises(sqlite3.IntegrityError):
        shadow_import_character(database_url,char,report['legacy_digest'])
    with sqlite3.connect(database_url.removeprefix('sqlite:///')) as db:
        for table in ('companion_v2_records','companion_v2_record_revisions','companion_v2_memories',
                      'companion_v2_changes','companion_v2_legacy_map','companion_v2_legacy_imports'):
            assert db.execute(f'SELECT count(*) FROM {table}').fetchone()[0]==0


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
