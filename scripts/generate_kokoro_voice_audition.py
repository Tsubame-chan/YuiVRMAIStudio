#!/usr/bin/env python3
"""Local audition artifact; uses the same INT8 model/native bridge as the app."""
import base64
import ctypes as C
import hashlib
import json
from pathlib import Path
import shutil
import time
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/reports/kokoro_voice_audition_20261005'
PACK = ROOT / 'builds/kokoro-validation-data-20261005'
VOICES = [('af_sky', 'Sky', '女性'), ('af_nova', 'Nova', '女性'), ('af_nicole', 'Nicole', '女性'), ('af_bella', 'Bella', '女性'), ('am_puck', 'Puck', '男性'), ('am_michael', 'Michael', '男性'), ('af_heart', 'Heart（前回の比較基準）', '女性・参考')]
TEXTS = [
    ('挨拶', "Hey! You are finally here. I missed you! What shall we do today?", 'hˈA! ju ɑɹ fˈInəli hˈɪɹ. ˈI mˈɪst ju! wˈʌt ʃæl wi dˈu tədˈA?'),
    ('元気な返答', "Wow, that is amazing! You did it! Let us try something fun together.", 'wˈaʊ, ðæt ɪz əmˈAzɪŋ! ju dˈɪd ɪt! lˈɛt ʌs tɹˈI sˈʌmθɪŋ fˈʌn təɡˈɛðəɹ.'),
    ('穏やかな返答', "It is okay. Take your time. I am right here with you, and I am happy to listen.", 'ɪt ɪz OkˈA. tˈAk jɔɹ tˈIm. ˈI æm ɹˈIt hˈɪɹ wɪð ju, ænd ˈI æm hˈæpi tə lˈɪsən.'),
]

def pitch_shift(x, semitones):
    # NumPy translation of canonical YuiSpeechPitch.Shift; duration is preserved.
    n, hop = 1024, 256
    ratio = 2 ** (semitones / 12)
    window = .5 - .5 * np.cos(2 * np.pi * np.arange(n) / n)
    last = np.zeros(n // 2 + 1); phase = last.copy()
    out = np.zeros(len(x)); weights = out.copy()
    bins = np.arange(n // 2 + 1)
    targets = np.rint(bins * ratio).astype(int)
    valid = targets <= n // 2
    for start in range(-n, len(x), hop):
        frame = np.zeros(n)
        left, right = max(0, start), min(len(x), start + n)
        if right <= left: continue
        frame[left-start:right-start] = x[left:right]
        spec = np.fft.rfft(frame * window)
        angle = np.angle(spec); expected = 2 * np.pi * bins * hop / n
        difference = angle - last - expected
        difference -= 2 * np.pi * np.rint(difference / (2 * np.pi))
        last = angle
        freq = (bins + difference * n / (2 * np.pi * hop)) * ratio
        mag = np.abs(spec); mags = np.zeros_like(last); freqs = mags.copy()
        np.add.at(mags, targets[valid], mag[valid])
        np.add.at(freqs, targets[valid], (mag * freq)[valid])
        freq = np.divide(freqs, mags, out=bins.astype(float).copy(), where=mags > 0)
        phase += 2 * np.pi * freq * hop / n
        rendered = np.fft.irfft(mags * np.exp(1j * phase), n)
        w = window[left-start:right-start]
        out[left:right] += rendered[left-start:right-start] * w
        weights[left:right] += w*w
    return np.clip(out / np.maximum(.001, weights), -1, 1).astype(np.float32)

class Result(C.Structure):
    _fields_ = [('ok', C.c_int32), ('sampleRate', C.c_int32), ('count', C.c_int64), ('samples', C.POINTER(C.c_float)), ('error', C.c_char_p)]

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT/'audio').mkdir(exist_ok=True)
    scratch = OUT/'voice-data'; scratch.mkdir(exist_ok=True)
    bank = np.load(Path.home()/'.cache/yui-vrm-ai-studio/kokoro/voices-v1.0.bin')
    bridge = C.CDLL(str(ROOT/'unity/Assets/Plugins/macOS/libYuiKokoroBridge.dylib'))
    bridge.YuiKokoro_Synthesize.argtypes = [C.c_char_p, C.c_char_p, C.c_char_p, C.POINTER(C.c_int64), C.c_int32, C.c_float]
    bridge.YuiKokoro_Synthesize.restype = C.POINTER(Result)
    bridge.YuiKokoro_Free.argtypes = [C.POINTER(Result)]
    vocab = json.loads((PACK/'config.json').read_text())['vocab']
    clips = []
    for voice, name, gender in VOICES:
        voicefile = scratch/f'{voice}.bin'
        bank[voice].astype('<f4').tofile(voicefile)
        for index, (label, text, phonemes) in enumerate(TEXTS):
            assert all(p in vocab for p in phonemes), phonemes
            tokens = np.array([0]+[vocab[p] for p in phonemes]+[0], dtype=np.int64)
            before = time.monotonic()
            r = bridge.YuiKokoro_Synthesize(str(ROOT/'unity/Assets/Plugins/macOS/Voicevox/libvoicevox_onnxruntime.1.17.3.dylib').encode(), str(PACK/'kokoro-v1.0.int8.onnx').encode(), str(voicefile).encode(), tokens.ctypes.data_as(C.POINTER(C.c_int64)), len(tokens), 1.)
            try:
                if not r.contents.ok: raise RuntimeError(r.contents.error)
                x = np.ctypeslib.as_array(r.contents.samples, shape=(r.contents.count,)).copy()
                rate = r.contents.sampleRate
            finally: bridge.YuiKokoro_Free(r)
            elapsed = time.monotonic()-before
            assert len(x) > rate and np.isfinite(x).all() and np.max(np.abs(x)) > .01
            for pitch in [0, 2]:
                audio = x if pitch == 0 else pitch_shift(x, pitch)
                # Same peak ceiling; no compression, noise reduction, or speed changes.
                audio = audio * (.85 / max(.85, float(np.max(np.abs(audio)))))
                path = OUT/'audio'/f'{voice}-{index}-p{pitch}.wav'
                sf.write(path, audio, rate, subtype='PCM_16')
                clips.append(dict(voice=voice, sentence=index, pitch=pitch, file=str(path.relative_to(OUT)), seconds=len(audio)/rate, synthesis_seconds=elapsed, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            print(f'{voice} {label}: {len(x)/rate:.2f}s', flush=True)
    bridge.YuiKokoro_Release()
    for name in ['Kokoro-Apache-2.0.txt', 'ONNX-conversion-LICENSE.txt']:
        shutil.copyfile(PACK/name, OUT/name)
    notice = (PACK/'NOTICE.txt').read_text().replace('Heart and Bella voices', 'Sky, Nova, Nicole, Bella, Puck, Michael and Heart voices').replace('two voice tensors', 'seven voice tensors').replace('This pack contains data only. No eSpeak NG, phonemizer, Python or executable.', 'This audition includes generated WAV audio, a self-contained HTML player and raw float32 voice tensors. No model, eSpeak NG or native executable is bundled. Pitch +2 is a Yui-derived audio transformation.')
    (OUT/'NOTICE.txt').write_text(notice)
    manifest = dict(model='Kokoro v1.0 INT8 ONNX', model_sha256=hashlib.sha256((PACK/'kokoro-v1.0.int8.onnx').read_bytes()).hexdigest(), voices=[dict(id=v,name=n,gender=g) for v,n,g in VOICES], texts=[dict(label=l,text=t,phonemes=p) for l,t,p in TEXTS], clips=clips, speed=1, sample_rate=24000, pitch_algorithm='NumPy translation of YuiSpeechPitch.Shift; +2 semitones preserves duration', acceptance='User listening pending; no production voice selection yet')
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
    data = dict(manifest)
    data['clips'] = [dict(c,src='data:audio/wav;base64,'+base64.b64encode((OUT/c['file']).read_bytes()).decode()) for c in clips]
    template = (ROOT/'scripts/local_tts/kokoro_audition_template.html').read_text()
    (OUT/'index.html').write_text(template.replace('/*AUDITION_DATA*/', json.dumps(data,ensure_ascii=False)))
    print(OUT/'index.html')

if __name__ == '__main__': main()
