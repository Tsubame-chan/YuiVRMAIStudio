#!/usr/bin/env python3
"""Print only non-secret startup settings, never shell code or saved API keys."""
import argparse
import json
from pathlib import Path
import sys
import os
from urllib.parse import urlsplit
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'backend'))
from app.core.config import get_settings

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--format',choices=['json','nul'],default='json');args=parser.parse_args()
    settings=get_settings()
    names={'TTS_PROVIDER':'tts_provider','HTTP_TTS_PROVIDER_ID':'http_tts_provider_id','HTTP_TTS_BASE_URL':'http_tts_base_url',
           'HTTP_TTS_HEALTH_ENDPOINT':'http_tts_health_endpoint','VOICEVOX_BASE_URL':'voicevox_base_url','AIVIS_BASE_URL':'aivis_base_url'}
    values={key:str(getattr(settings,name)) for key,name in names.items()}
    voicevox=urlsplit(settings.voicevox_base_url)
    values.update(VOICEVOX_URL_HOST=voicevox.hostname or '127.0.0.1',
                  VOICEVOX_URL_PORT=str(voicevox.port or (443 if voicevox.scheme=='https' else 80)),
                  VOICEVOX_START_LOCAL='1' if voicevox.scheme=='http' and voicevox.hostname in {'localhost','127.0.0.1','::1'} and voicevox.path in {'','/'} else '0')
    from app.core.admin_settings import read_overrides
    overrides,_=read_overrides()
    values['IRODORI_BASE_URL']=settings.http_tts_base_url if 'http_tts_base_url' in overrides else os.environ.get('IRODORI_BASE_URL') or settings.http_tts_base_url
    if args.format=='json':print(json.dumps(values))
    else:
        for key,value in values.items():sys.stdout.buffer.write((key+'\0'+value+'\0').encode())

if __name__=='__main__':main()
