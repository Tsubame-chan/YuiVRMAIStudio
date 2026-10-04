import copy
import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.core.config import get_settings
from app.models.chat import ChatResponse
from app.providers.router import ProviderRouter
from app.core.shared_conversation import should_auto_remember


def test_auto_memory_keeps_statements_but_skips_questions_without_punctuation():
    assert should_auto_remember("私は猫が好きです")
    assert not should_auto_remember("私の名前を覚えていますか")
    assert not should_auto_remember("What do you remember about my name")


@pytest.fixture
def shared(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", "sqlite:///"+str(tmp_path/"shared.db"))
    monkeypatch.setenv("YUI_BACKEND_SETTINGS_PATH", str(tmp_path/"settings.json"))
    get_settings.cache_clear()
    calls=[]
    class Provider:
        async def generate(self, request, history):
            calls.append((copy.deepcopy(request), history))
            return ChatResponse(text="そうだったんだね。", memory_action="none")
    monkeypatch.setattr(ProviderRouter, "chat", lambda self: Provider())
    with TestClient(app, base_url="http://localhost", client=("127.0.0.1",1234)) as c:
        c.post("/admin/session", headers={"X-Yui-Admin":"1"}); c.headers["X-Yui-Admin"]="1"
        code=c.post("/admin/api/sync/pairing").json()["code"]
        device=c.post("/sync/pair",json={"name":"Phone A","code":code}).json()
        auth={"Authorization":"Bearer "+device["token"]}
        char=c.post("/sync/characters",headers=auth,json={"name":"Yui"}).json()["id"]
        yield c,auth,char,calls
    get_settings.cache_clear()


def push(c,auth,char,items):
    p=c.post("/sync/plan",headers=auth,json={"character_id":char,"items":items})
    assert p.status_code==200,p.text
    r=c.post("/sync/commit",headers=auth,json={"plan_id":p.json()["plan_id"],"choices":{}})
    assert r.status_code==200,r.text
    return r.json()


def memory(id, content, when, version=0, deleted=False):
    return {"kind":"memory","id":id,"base_version":version,"deleted":deleted,
            "value":{} if deleted else {"content":content,"pinned":False,"recorded_utc":when}}


def endpoint(char,path=""): return "/admin/api/sync/characters/"+char+path


def test_backend_conversation_merges_into_phone_and_uses_both_episodes(shared):
    c,auth,char,calls=shared
    push(c,auth,char,[memory("phone-sad","悲しいことがあって落ち込んでいます。","2026-10-03T01:00:00Z"),
        {"kind":"profile","id":"instruction","value":{"text":"落ち着いた優しい相棒"}}])
    body={"request_id":"pc-one","session_id":"pc-conversation","message":"最近絶好調。こういう時に限って悲しいことが起こるんだよなあ。"}
    assert c.post(endpoint(char,"/chat"),json=body).status_code==200
    merged=push(c,auth,char,[])
    memories=[x for x in merged["items"] if x["kind"]=="memory"]
    assert len(memories)==2
    assert {x["value"]["text"] for x in merged["items"] if x["kind"]=="history"}=={body["message"],"そうだったんだね。"}
    c.post(endpoint(char,"/chat"),json={"request_id":"pc-two","session_id":"another-conversation","message":"最近の気分の変化を一緒に振り返って。","secret":True})
    request,history=calls[-1]
    assert "悲しいこと" in request.context.extra["character_memory"]
    assert "最近絶好調" in request.context.extra["character_memory"]
    assert "Do not infer causation" in request.context.extra["character_memory"]
    assert request.custom_instruction=="落ち着いた優しい相棒"
    assert history==[]  # Other conversations are reference records, not this conversation's turns.
    assert push(c,auth,char,[])["items"]==merged["items"]


def test_linked_memory_keeps_evidence_and_is_invalidated_when_source_changes(shared):
    c,auth,char,_=shared
    state=push(c,auth,char,[memory("good","最近絶好調です。","2026-10-01T00:00:00Z"),memory("sad","悲しいことがありました。","2026-10-02T00:00:00Z")])
    draft=c.post(endpoint(char,"/connection-preview"),json={"source_ids":["sad","good"]}).json()
    assert draft["source_ids"]==["good","sad"]
    assert draft["content"].index("絶好調") < draft["content"].index("悲しい")
    assert "因果関係" in draft["notice"]
    body={k:draft[k] for k in ["source_ids","source_versions","content"]}
    body["content"]="好調だと話したあとに、悲しいことがあったと話した。原因はまだ分からない。"
    result=c.post(endpoint(char,"/connections"),json=body)
    assert result.status_code==200,result.text
    id=result.json()["id"]
    merged=push(c,auth,char,[])
    linked=next(x for x in merged["items"] if x["id"]==id)
    assert linked["value"]["source_ids"]==["good","sad"]
    assert linked["value"]["basis"]=="user_confirmed_connection"
    assert len([x for x in merged["items"] if x["kind"]=="memory" and not x["deleted"]])==3
    source=next(x for x in state["items"] if x["id"]=="sad")
    modified=push(c,auth,char,[memory("sad","前の悲しい話は架空の例でした。","2026-10-03T00:00:00Z",source["version"])])
    assert next(x for x in modified["items"] if x["id"]==id)["deleted"]
    assert c.post(endpoint(char,"/connections"),json=body).status_code==409


def test_deleted_source_does_not_resurrect_a_link_after_offline_sync(shared):
    c,auth,char,_=shared
    initial=push(c,auth,char,[memory("one","私は好調です。",""),memory("two","私は悲しいです。","")])
    draft=c.post(endpoint(char,"/connection-preview"),json={"source_ids":["one","two"]}).json()
    c.post(endpoint(char,"/connections"),json={k:draft[k] for k in ["source_ids","source_versions","content"]})
    before=push(c,auth,char,[])
    one=next(x for x in before["items"] if x["id"]=="one")
    after=push(c,auth,char,[memory("one","","",one["version"],True)])
    link=next(x for x in after["items"] if x["id"].startswith("connection:"))
    assert link["deleted"]
    stale=[{**{k:x[k] for k in ['kind','id','deleted','value']},"base_version":x["version"],"changed":False} for x in before["items"]]
    assert next(x for x in push(c,auth,char,stale)["items"] if x["id"]==link["id"])["deleted"]


def test_shared_chat_retry_is_idempotent_and_scoped_to_exact_input(shared):
    c,auth,char,calls=shared
    body={"request_id":"repeat","session_id":"one","message":"私は猫が好きです。"}
    first=c.post(endpoint(char,"/chat"),json=body)
    assert c.post(endpoint(char,"/chat"),json=body).json()==first.json()
    assert len(calls)==1
    assert c.post(endpoint(char,"/chat"),json={**body,"message":"different"}).status_code==409
    assert len(push(c,auth,char,[])["items"])==3


def test_shared_management_is_local_and_same_names_are_not_merged(shared):
    c,auth,char,calls=shared
    other=c.post("/sync/characters",headers=auth,json={"name":"Yui"}).json()["id"]
    push(c,auth,char,[memory("private","私は猫が好き","")])
    assert c.get(endpoint(other)).json()["items"]==[]
    with TestClient(app,base_url="http://localhost",client=("100.100.20.30",1234)) as remote:
        assert remote.get(endpoint(char)).status_code==403
        assert remote.post(endpoint(char,"/chat"),json={"session_id":"one","message":"hello"}).status_code==403
    assert c.post(endpoint(other,"/connection-preview"),json={"source_ids":["private","missing"]}).status_code==409
    assert c.post(endpoint(char,"/connection-preview"),json={"source_ids":["private","private"]}).status_code==422


def test_legacy_import_is_scoped_previewed_idempotent_and_copy_only(shared):
    from app.core.device_sync import SyncStore
    c,auth,char,_=shared
    store=SyncStore(get_settings().database_url)
    with store.connect() as db:
        db.execute("INSERT INTO memories(user_id,character_id,content) VALUES('old-user','old-yui','私は紅茶が好きです。')")
        db.execute("INSERT INTO memories(user_id,character_id,content) VALUES('other-user','old-yui','他人の記憶')")
        for role,text in [('user','最近絶好調です。'),('assistant','よかったね。')]:
            db.execute("INSERT INTO conversations(user_id,character_id,session_id,role,message) VALUES('old-user','old-yui','old-session',?,?)",(role,text))
    body={'user_id':'old-user','character_id':'old-yui'}
    draft=c.post(endpoint(char,'/import-preview'),json=body).json()
    assert len(draft['items'])==3
    assert not any('他人' in str(x) for x in draft['items'])
    with store.connect() as db:
        db.execute("UPDATE memories SET content='私は緑茶が好きです。' WHERE user_id='old-user'")
    assert c.post(endpoint(char,'/import'),json={**body,'preview_hash':draft['preview_hash']}).status_code==409
    draft=c.post(endpoint(char,'/import-preview'),json=body).json()
    assert c.post(endpoint(char,'/import'),json={**body,'preview_hash':draft['preview_hash']}).json()['imported']==3
    merged=push(c,auth,char,[])
    saved=next(x for x in merged['items'] if x['kind']=='memory')
    push(c,auth,char,[memory(saved['id'],'','',saved['version'],True)])
    again=c.post(endpoint(char,'/import-preview'),json=body).json()
    assert again['items']==[] and again['already_shared']==3
    with store.connect() as db:
        assert db.execute('SELECT COUNT(*) FROM memories').fetchone()[0]==2
        assert db.execute('SELECT COUNT(*) FROM conversations').fetchone()[0]==2


def test_legacy_imported_session_can_continue_with_original_roles(shared):
    from app.core.device_sync import SyncStore
    c,_,char,calls=shared
    store=SyncStore(get_settings().database_url)
    with store.connect() as db:
        for role,text in [('user','最初の発言'),('assistant','最初の返答'),('user','次の発言')]:
            db.execute("INSERT INTO conversations(user_id,character_id,session_id,role,message) VALUES('legacy-owner','old-yui','old-session',?,?)",(role,text))
    body={'user_id':'legacy-owner','character_id':'old-yui'}
    draft=c.post(endpoint(char,'/import-preview'),json=body).json()
    session=next(x['value']['conversation_id'] for x in draft['items'] if x['kind']=='history')
    assert ':' in session
    assert c.post(endpoint(char,'/import'),json={**body,'preview_hash':draft['preview_hash']}).status_code==200
    assert c.post(endpoint(char,'/chat'),json={'request_id':'continue','session_id':session,'message':'続きを話して','secret':True}).status_code==200
    assert calls[-1][1]==[{'role':'user','content':'最初の発言'},
                          {'role':'assistant','content':'最初の返答'},
                          {'role':'user','content':'次の発言'}]
    # A pre-fix imported ID had no role suffix. It must not be imported twice.
    with store.connect() as db:
        row=db.execute("SELECT kind,id FROM sync_items WHERE character_id=? AND kind='history' ORDER BY id LIMIT 1",(char,)).fetchone()
        db.execute("UPDATE sync_items SET id=? WHERE character_id=? AND kind=? AND id=?",(row['id'].rsplit(':',1)[0],char,row['kind'],row['id']))
    again=c.post(endpoint(char,'/import-preview'),json=body).json()
    assert again['items']==[] and again['already_shared']==3
    assert c.post(endpoint(char,'/chat'),json={'request_id':'continue-old','session_id':session,'message':'もう一度','secret':True}).status_code==200
    assert calls[-1][1][0]=={'role':'user','content':'最初の発言'}


def test_shared_history_preserves_user_then_assistant_and_context_budget(shared):
    from app.core.shared_conversation import reference_context
    c,auth,char,calls=shared
    c.post(endpoint(char,'/chat'),json={'request_id':'first','session_id':'order','message':'私は猫が好き'})
    c.post(endpoint(char,'/chat'),json={'request_id':'second','session_id':'order','message':'続きを教えて','secret':True})
    assert calls[-1][1]==[{'role':'user','content':'私は猫が好き'},{'role':'assistant','content':'そうだったんだね。'}]
    items=[{'kind':'memory','id':str(i),'deleted':False,'value':{'content':'猫'*600,'pinned':True,'recorded_utc':''}} for i in range(8)]
    assert len(reference_context(items,'猫'))<=2400


def test_old_desktop_roundtrip_retains_connection_provenance_but_edit_is_rejected(shared):
    c,auth,char,_=shared
    push(c,auth,char,[memory('one','私は好調',''),memory('two','私は悲しい','')])
    draft=c.post(endpoint(char,'/connection-preview'),json={'source_ids':['one','two']}).json()
    c.post(endpoint(char,'/connections'),json={k:draft[k] for k in ['source_ids','source_versions','content']})
    state=push(c,auth,char,[])
    linked=next(x for x in state['items'] if x['id'].startswith('connection:'))
    old=memory(linked['id'],linked['value']['content'],linked['value']['recorded_utc'],linked['version'])
    assert next(x for x in push(c,auth,char,[old])['items'] if x['id']==linked['id'])['value']['source_ids']==draft['source_ids']
    old['value']['content']='違う意味へ編集'
    assert c.post('/sync/plan',headers=auth,json={'character_id':char,'items':[old]}).status_code==422


def test_old_client_pulls_invalidated_interpretation_without_blocking_other_records(shared):
    c,auth,char,_=shared
    push(c,auth,char,[memory('one','私は好調',''),memory('two','私は悲しい','')])
    draft=c.post(endpoint(char,'/connection-preview'),json={'source_ids':['one','two']}).json()
    c.post(endpoint(char,'/connections'),json={k:draft[k] for k in ['source_ids','source_versions','content']})
    before=push(c,auth,char,[])
    linked=next(x for x in before['items'] if x['id'].startswith('connection:'))
    source=next(x for x in before['items'] if x['id']=='one')
    push(c,auth,char,[memory('one','','',source['version'],True)])
    old=memory(linked['id'],linked['value']['content'],linked['value']['recorded_utc'],linked['version'])
    merged=push(c,auth,char,[old,memory('new','私は紅茶が好き','')])
    assert next(x for x in merged['items'] if x['id']==linked['id'])['deleted']
    assert next(x for x in merged['items'] if x['id']=='new')['value']['content']=='私は紅茶が好き'
