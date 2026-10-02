"""Revisioned TTS endpoints and profiles. Legacy request settings are untouched."""
from __future__ import annotations
import hashlib
import json
import os
import tempfile
import threading
from pathlib import Path
from typing import Literal
from pydantic import BaseModel, ConfigDict, Field
from app.core.config import Settings, ROOT_DIR
from app.core.admin_settings import validated
from app.models.tts import TTSRequest

LOCK = threading.RLock()
KINDS = {'voicevox': 'VOICEVOX', 'aivis': 'AivisSpeech', 'http': '対応HTTP TTS'}
IDENT = r'^[A-Za-z0-9_.-]+$'
PARAMS = {'speaker_id','speed_scale','pitch_scale','intonation_scale','volume_scale','pre_phoneme_length','post_phoneme_length','voice_gender','voice_instruct','voice_lang_code'}

class Endpoint(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    id: str = Field(pattern=IDENT, min_length=1, max_length=64)
    name: str = Field(min_length=1, max_length=80)
    provider_type: Literal['voicevox','aivis','http']
    settings: dict = Field(default_factory=dict, max_length=40)

class Profile(BaseModel):
    model_config = ConfigDict(extra='forbid', strict=True)
    id: str = Field(pattern=IDENT, min_length=1, max_length=64)
    name: str = Field(min_length=1, max_length=80)
    endpoint_id: str = Field(pattern=IDENT, min_length=1, max_length=64)
    parameters: dict = Field(default_factory=dict, max_length=12)
    # HTTP model/voice are endpoint settings by default, but can vary per voice.
    voice: str = Field(default='', max_length=200)
    model: str = Field(default='', max_length=200)
    fallback_profile_id: str | None = Field(default=None, pattern=IDENT, max_length=64)

class LibraryUpdate(BaseModel):
    model_config = ConfigDict(extra='forbid')
    revision: str = Field(max_length=128)
    endpoints: list[Endpoint] = Field(max_length=32)
    profiles: list[Profile] = Field(max_length=100)

def library_path():
    return Path(os.environ.get('YUI_VOICE_LIBRARY_PATH', ROOT_DIR/'backend/data/voice-library.json'))

def load():
    p=library_path()
    if p.is_symlink(): raise ValueError('音声設定の保存先を確認してください。')
    if not p.exists(): return {'endpoints':[], 'profiles':[]}, 'initial'
    raw=p.read_bytes()
    if len(raw)>262144: raise ValueError('音声設定が大きすぎます。')
    obj=json.loads(raw)
    if obj.get('schema')!=1: raise ValueError('音声設定の形式を確認してください。')
    model=LibraryUpdate(revision='', endpoints=obj['endpoints'], profiles=obj['profiles'])
    return model.model_dump(exclude={'revision'}), hashlib.sha256(raw).hexdigest()

def endpoints(data, settings):
    # Virtual entries read current .env/admin overrides; no secret copies on migration.
    legacy=[{'id':'builtin-'+k,'name':KINDS[k] if k!='http' else settings.http_tts_provider_id or '外部TTS',
             'provider_type':k,'settings':{}} for k in KINDS]
    return {e['id']:e for e in legacy+data['endpoints']}

def endpoint_settings(e, settings):
    if e['id'].startswith('builtin-'):
        return settings.model_copy(update=e['settings'])
    prefix=e['provider_type']+'_' if e['provider_type']!='http' else 'http_tts_'
    # A new endpoint must not inherit credentials or voice options for another server.
    defaults={k:f.default for k,f in Settings.model_fields.items() if k.startswith(prefix) and k!='http_tts_soundstretch_path'}
    return settings.model_copy(update={**defaults,**e['settings']})

def capabilities(e, settings):
    s=endpoint_settings(e,settings);k=e['provider_type']
    common=[{'name':n,'label':label,'min':lo,'max':hi,'default':v,'step':step,'mode':'engine'} for n,label,lo,hi,v,step in (
        ('speed_scale','話す速さ',.5,2,1,.05),('pitch_scale','声の高さ',-.5,.5,0,.05),('intonation_scale','抑揚',0,2,1,.05),('volume_scale','音量',0,2,1,.05),('pre_phoneme_length','前の無音（秒）',0,1.5,.1,.05),('post_phoneme_length','後の無音（秒）',0,1.5,.1,.05))]
    if k in {'voicevox','aivis'}:return {'voices':'speakers','fields':common,'text_fields':[],'voice_model':False}
    fmt=s.http_tts_payload_format
    if fmt=='generic':return {'voices':'manual','fields':common[:4],'text_fields':[],'voice_model':True}
    irodori=s.http_tts_payload_format=='irodori_openai_speech' or 'irodori' in (s.http_tts_provider_id+' '+s.http_tts_model).lower()
    fields=common[:2] if irodori else common[:1]
    if irodori:
        for f in fields:f['mode']='postprocess'
        if s.http_tts_audio_processor=='none':fields=[]
    server=fmt=='irodori_openai_speech'
    return {'voices':'design' if irodori else 'manual','dialect':'irodori-server' if server else 'irodori-mlx' if irodori else fmt,'fields':fields,
            'text_fields':[{'name':n,'label':l} for n,l in ([('voice_instruct','声の説明')] if server else [('voice_instruct','声の説明'),('voice_gender','声の性別'),('voice_lang_code','言語')])] if irodori else [],
            'voice_model':True,'defaults':{'model':s.http_tts_model,'voice':s.http_tts_voice,
                'voice_instruct':s.http_tts_instruct,'voice_gender':s.http_tts_gender,'voice_lang_code':s.http_tts_lang_code}}

def public_library(settings):
    data,rev=load();out=[]
    for e in endpoints(data,settings).values():
        s=endpoint_settings(e,settings);prefix=e['provider_type']+'_' if e['provider_type']!='http' else 'http_tts_'
        fields={k:v for k,v in s.model_dump().items() if k.startswith(prefix) and k!='http_tts_soundstretch_path'}
        secret=bool(fields.pop('http_tts_api_key', ''))
        out.append({**e,'settings':fields,'key_configured':secret,'builtin':e['id'].startswith('builtin-'),'capabilities':capabilities(e,settings)})
    return {'revision':rev,'endpoints':out,'profiles':data['profiles']}

def validate_profile(p, endpoint, settings):
    cap=capabilities(endpoint,settings)
    allowed={'speaker_id'} if cap['voices']=='speakers' else set()
    allowed|={f['name'] for f in cap['fields']}|{f['name'] for f in cap['text_fields']}
    if set(p['parameters'])-allowed:raise ValueError('この提供元が対応しない音声調整があります。')
    for k,v in p['parameters'].items():
        if k=='speaker_id':
            if type(v) is not int or v<0:raise ValueError('話者IDを確認してください。')
        elif k in {f['name'] for f in cap['fields']}:
            if isinstance(v,bool) or not isinstance(v,(float,int)):raise ValueError('調整値は数値で指定してください。')
        elif not isinstance(v,str) or len(v)>2000:raise ValueError('音声指示を確認してください。')
    TTSRequest(text='validation',**p['parameters'])
    if not cap['voice_model'] and (p['voice'] or p['model']):raise ValueError('この提供元ではモデルやvoice文字列は指定しません。')

def save(body, settings):
    with LOCK:
        old,rev=load()
        if rev!=body.revision:raise FileExistsError('別の画面で音声設定が変更されました。再読込してください。')
        oldmap={e['id']:e for e in old['endpoints']};es=[]
        for item in body.endpoints:
            e=item.model_dump()
            if e['id'].startswith('builtin-'):raise ValueError('既存接続の変更は提供元設定で行ってください。')
            prefix=e['provider_type']+'_' if e['provider_type']!='http' else 'http_tts_'
            if any(not k.startswith(prefix) or k=='http_tts_soundstretch_path' for k in e['settings']):raise ValueError('接続設定を確認してください。')
            key=e['settings'].get('http_tts_api_key','')
            if e['provider_type']=='http' and key=='':
                prior=oldmap.get(e['id'])
                if prior and prior['provider_type']=='http':e['settings']['http_tts_api_key']=prior['settings'].get('http_tts_api_key','')
            elif key is None:e['settings']['http_tts_api_key']=''
            e['settings']=validated(e['settings']);es.append(e)
        ps=[p.model_dump() for p in body.profiles];data={'endpoints':es,'profiles':ps}
        if len({e['id'] for e in es})!=len(es) or len({p['id'] for p in ps})!=len(ps):raise ValueError('IDが重複しています。')
        emap=endpoints(data,settings);pmap={p['id']:p for p in ps}
        for p in ps:
            if p['endpoint_id'] not in emap:raise ValueError('声が使用する接続先を確認してください。')
            validate_profile(p,emap[p['endpoint_id']],settings)
            seen={p['id']};f=p['fallback_profile_id']
            while f:
                if f in seen or f not in pmap:raise ValueError('代替音声の参照や循環を確認してください。')
                seen.add(f);f=pmap[f]['fallback_profile_id']
        raw=json.dumps({'schema':1,**data},ensure_ascii=False,indent=2).encode()
        if len(raw)>262144:raise ValueError('音声設定が大きすぎます。')
        path=library_path();path.parent.mkdir(parents=True,exist_ok=True)
        fd,temp=tempfile.mkstemp(prefix='.voice-library-',dir=path.parent)
        try:
            with os.fdopen(fd,'wb') as f:
                os.chmod(temp,0o600);f.write(raw);f.flush();os.fsync(f.fileno())
            os.replace(temp,path)
        finally:
            if os.path.exists(temp):os.unlink(temp)
        return public_library(settings)

def resolve_profile(profile_id, request, settings, data=None, apply_overrides=True):
    if data is None:data,_=load()
    profile=next((p for p in data['profiles'] if p['id']==profile_id),None)
    if profile is None:raise ValueError('指定した音声プリセットがありません。')
    e=endpoints(data,settings).get(profile['endpoint_id'])
    if e is None:raise ValueError('音声の接続先がありません。')
    validate_profile(profile,e,settings)
    s=endpoint_settings(e,settings)
    if e['provider_type']=='http':s=s.model_copy(update={'http_tts_voice':profile['voice'] or s.http_tts_voice,'http_tts_model':profile['model'] or s.http_tts_model})
    params=dict(profile['parameters'])
    if apply_overrides:
        params.update({k:getattr(request,k) for k in request.model_fields_set & PARAMS})
        validate_profile({**profile,'parameters':params},e,settings)
    cap=capabilities(e,settings)
    allowed={f['name'] for f in cap['fields']}|{f['name'] for f in cap['text_fields']}
    # Legacy model defaults must not send unsupported controls to a profile's API.
    params.update({k:None for k in PARAMS-allowed-{'speaker_id'}})
    resolved=TTSRequest(text=request.text,request_id=request.request_id,provider=e['provider_type'],**params)
    return resolved,s,profile,data
