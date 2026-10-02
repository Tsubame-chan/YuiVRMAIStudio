"""Verify service ownership without touching the user's running services."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('service_ownership', ROOT/'scripts/service_ownership.py')
ownership = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ownership)


def test_reused_pid_is_never_signalled(tmp_path, monkeypatch):
    path=tmp_path/'backend-18040.json'
    path.write_text(json.dumps({'pid':1234,'start':'old-process'}))
    monkeypatch.setattr(ownership,'fingerprint',lambda pid:'different-process')
    monkeypatch.setattr(ownership.os,'kill',lambda *args: (_ for _ in ()).throw(AssertionError('Foreign process signalled')))
    assert 'retained' in ownership.stop(tmp_path,'backend-18040')
    assert not path.exists()


def test_stop_only_owns_recorded_root_and_descendants(tmp_path, monkeypatch):
    alive={101:'root',102:'child',999:'unrelated'}
    monkeypatch.setattr(ownership,'fingerprint',lambda pid:alive.get(pid,''))
    monkeypatch.setattr(ownership,'process_parents',lambda:{101:1,102:101,999:1})
    signalled=[]
    def stop(pid, signal):signalled.append(pid);alive.pop(pid,None)
    monkeypatch.setattr(ownership.os,'kill',stop)
    ownership.record(tmp_path,'backend-18040',101)
    ownership.stop(tmp_path,'backend-18040')
    assert set(signalled)=={101,102} and alive=={999:'unrelated'}


def test_replaced_record_is_retained(tmp_path):
    path=tmp_path/'backend-18040.json'
    path.write_text(json.dumps({'pid':42,'start':'replacement'}))
    ownership.forget_if_unchanged(path,{'pid':41,'start':'previous'})
    assert path.exists()


def test_launcher_reads_saved_endpoint_without_exporting_credentials(tmp_path):
    saved=tmp_path/'settings.json'
    saved.write_text(json.dumps({'schema':1,'values':{'tts_provider':'http','http_tts_provider_id':'irodori',
        'http_tts_base_url':'http://127.0.0.1:41099','voicevox_base_url':'http://192.0.2.1:50021',
        'openai_api_key':'test-secret-never-export'}}))
    env=dict(os.environ,YUI_BACKEND_SETTINGS_PATH=str(saved),IRODORI_BASE_URL='http://127.0.0.1:41080')
    result=subprocess.check_output([sys.executable,str(ROOT/'scripts/effective_service_settings.py')],env=env,text=True)
    values=json.loads(result)
    assert values['IRODORI_BASE_URL']=='http://127.0.0.1:41099'
    assert values['VOICEVOX_START_LOCAL']=='0'
    assert 'test-secret-never-export' not in result and not any('KEY' in key for key in values)
