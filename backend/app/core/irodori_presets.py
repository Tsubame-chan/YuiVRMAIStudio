"""Synthetic, user-auditioned V4.1 FP16 reference voices shared with mobile."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1] / "assets" / "irodori"
VOICES = json.loads((ROOT / "voices.json").read_text(encoding="utf-8"))

def profiles(settings=None):
    server = settings is not None and settings.http_tts_payload_format == "irodori_openai_speech"
    return [{"id":"yui-irodori-"+v["id"],"name":v["name"],"endpoint_id":"builtin-http",
             "parameters":({"voice_instruct":v["caption"]} if server else
                           {"voice_instruct":v["caption"],"voice_gender":"female","voice_lang_code":"ja"}),
             "voice":v["id"] if server else "none","model":"","fallback_profile_id":None} for v in VOICES]

def reference(caption):
    v = next((v for v in VOICES if v["caption"] == caption), None)
    return (ROOT / (v["id"] + ".wav"), v["reference_text"]) if v else None
