// Engine installation is separate from the application's native voice assets.
const guideUrl='https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/BACKEND_TTS_GUIDE.md';
const entry=(title,steps,section)=>({title,steps,links:[['詳しくはこちら（GitHubの導入ガイド）',`${guideUrl}#${section}`]]});
const guides={
 voicevox:entry('VOICEVOX Engine','公式VOICEVOXを導入して起動し、この画面で接続先・話者を選んで試聴します。通常の接続先は http://127.0.0.1:50021 です。','voicevox'),
 aivis:entry('AivisSpeech Engine','AivisSpeechを導入して起動し、使いたい音声モデルをAivis側で追加します。この画面でAivisを選び、話者を選んで試聴・保存します。通常は http://127.0.0.1:10101 です。','aivis'),
 'irodori-mlx':entry('Irodori / Apple Silicon Mac','mlx-audioとIrodoriモデルを導入し、HTTPサーバーを起動します。「提供元と接続」でIrodori / Mac MLXを追加し、モデル・声の説明を選んで試聴します。','irodori-mac'),
 'irodori-server':entry('Irodori / Windows・CUDA等','Irodori-TTS-Serverと対応モデルを導入して起動します。「提供元と接続」でIrodori-TTS-Serverを追加し、声の説明または登録済みの声を選んで試聴します。','irodori-windows'),
 http:entry('その他のHTTP TTS','対応するAPI形式で接続先を追加します。一覧が取れないAPIは、提供元のモデル名・voice名を入力して試聴してください。独自形式にはadapterの追加が必要です。','other-tts'),
};
export function engineGuide(endpoint){const s=endpoint.settings||{},dialect=endpoint.capabilities?.dialect||(s.http_tts_payload_format==='irodori_openai_speech'?'irodori-server':/irodori/i.test((s.http_tts_provider_id||'')+' '+(s.http_tts_model||''))?'irodori-mlx':'');return guides[dialect]||guides[endpoint.provider_type]||guides.http;}
export const endpointTemplates={
 voicevox:{name:'VOICEVOX',provider_type:'voicevox',settings:{voicevox_base_url:'http://127.0.0.1:50021'}},
 aivis:{name:'AivisSpeech',provider_type:'aivis',settings:{aivis_base_url:'http://127.0.0.1:10101'}},
 http:{name:'HTTP TTS',provider_type:'http',settings:{http_tts_payload_format:'generic',http_tts_endpoint:'/tts'}},
 speech:{name:'Speech API互換',provider_type:'http',settings:{http_tts_payload_format:'openai_speech',http_tts_endpoint:'/v1/audio/speech'}},
 'irodori-mlx':{name:'Irodori MLX',provider_type:'http',settings:{http_tts_base_url:'http://127.0.0.1:41080',http_tts_payload_format:'openai_speech',http_tts_endpoint:'/v1/audio/speech',http_tts_provider_id:'irodori',http_tts_model:'mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit',http_tts_format:'wav',http_tts_gender:'female',http_tts_lang_code:'ja',http_tts_instruct:'明るく、聞き取りやすい自然な声。'}},
 'irodori-server':{name:'Irodori-TTS-Server',provider_type:'http',settings:{http_tts_base_url:'http://127.0.0.1:8088',http_tts_payload_format:'irodori_openai_speech',http_tts_endpoint:'/v1/audio/speech',http_tts_health_endpoint:'/health',http_tts_provider_id:'irodori-server',http_tts_model:'irodori-tts',http_tts_voice:'none',http_tts_format:'wav',http_tts_instruct:'明るく、聞き取りやすい自然な声。',http_tts_irodori_num_steps:24}},
};
