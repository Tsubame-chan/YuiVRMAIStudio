import {startLive, stopLive} from './realtime.js';
import {createVoiceEditor} from './voices.js';
import {bindDiagnostics} from './diagnostics.js';

const $ = s => document.querySelector(s);
const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const NAV = [['home','ホーム'],['providers','AIと音声'],['playground','試す'],['memory','記憶'],['activity','履歴と利用状況'],['settings','接続と設定']];
const labels = {ok:'接続確認済み',configured:'設定済み・動作未確認',missing_key:'キー未設定',offline:'接続できません',not_configured:'未設定',unknown:'未確認'};
let state, draft = {}, identities = [], probes = {}, view = 'home', section = '', page = 0, generation = 0, dataGeneration = 0, noticeTimer, activeRequest, audioUrl, diagnostics, voiceEditor;
let scope = {user_id:'local_user',character_id:'',session_id:''};
const current = k => Object.hasOwn(draft,k) ? draft[k] : state.settings.values[k];
const provider = id => state.providers.find(p => p.id === id);
const button = (action,label,cls='',attrs='') => `<button type="button" class="${cls}" data-action="${action}" ${attrs}>${label}</button>`;
const pill = status => `<span class="pill ${status==='ok'?'ok':['offline','missing_key'].includes(status)?'warn':''}">${esc(labels[status] || status)}</span>`;
const statusFor = id => probes[id]?.status || (provider(id)?.credential && id!=='http' ? state.settings.secrets[provider(id).credential]?'configured':'missing_key' : current(provider(id)?.endpoint)?'configured':'not_configured');

async function api(path, options={}) {
    const headers = {'X-Yui-Admin':'1', ...options.headers};
    if (options.body && !(options.body instanceof FormData)) {headers['Content-Type']='application/json';options.body=JSON.stringify(options.body);}
    const response = await fetch(`/admin/${path}`, {...options,headers,credentials:'same-origin',cache:'no-store'});
    if (!response.ok) {
        let message = `処理できませんでした (${response.status})`;
        try {const j=await response.json();message=typeof j.detail==='string'?j.detail:response.status===422?'入力項目を確認してください。':message;}catch{}
        const error=new Error(message);error.trace=response.headers.get("X-Yui-Trace")||"";throw error;
    }
    if(options.blob) return response.blob();
    const result=await response.json();if(result&&typeof result==='object')Object.defineProperty(result,'diagnosticTrace',{value:response.headers.get('X-Yui-Trace')||''});return result;
}
function notice(text) {$('#notice').textContent=text;$('#notice').hidden=false;clearTimeout(noticeTimer);noticeTimer=setTimeout(()=>$('#notice').hidden=true,6500);}
async function confirmAction(title,message,ok='続ける') {
    $('#confirm-title').textContent=title;$('#confirm-message').textContent=message;$('#confirm-ok').textContent=ok;
    const dialog=$('#confirm-dialog');dialog.returnValue='cancel';dialog.showModal();
    return new Promise(resolve=>dialog.addEventListener('close',()=>resolve(dialog.returnValue==='ok'),{once:true}));
}
function changed() {const n=Object.keys(draft).length;$('#savebar').hidden=!n;$('#save-note').textContent=`${n}項目の変更が未保存です。保存すると次のリクエストから反映されます。`;}
function field(f) {
    const val=current(f.name), id=`setting-${f.name}`;
    if(f.type==='boolean') return `<div class="field"><label class="check" for="${id}"><input type="checkbox" id="${id}" data-setting="${f.name}" ${val?'checked':''}>${esc(f.label)}</label></div>`;
    let input;
    if(f.options) input=`<select id="${id}" data-setting="${f.name}">${f.options.map(x=>`<option value="${esc(x)}" ${x===val?'selected':''}>${esc(provider(x)?.label || x || '使用しない')}</option>`).join('')}</select>`;
    else input=`<input id="${id}" data-setting="${f.name}" type="${f.secret?'password':f.type==='number'?'number':'text'}" value="${f.secret?'':esc(val)}" ${f.type==='number'?`min="0" max="1000000" step="${f.integer?1:'any'}"`:''} ${f.secret?'autocomplete="new-password" placeholder="空欄で現在のキーを維持"':'autocomplete="off"'}>`;
    return `<div class="field"><label for="${id}">${esc(f.label)}</label>${input}${f.secret?`<small>${Object.hasOwn(draft,f.name)?draft[f.name]===null?'保存するとキーを解除します':'変更予定':state.settings.secrets[f.name]?'設定済み。キーの値は表示しません。':'未設定'}</small>${button('clear-key','このキーを解除','link',`data-key="${f.name}"`)}`:''}</div>`;
}
function settingsFields(fields) {
    const basic=fields.filter(f=>!f.advanced), advanced=fields.filter(f=>f.advanced);
    return `<div class="field-grid">${basic.map(field).join('')}</div>${advanced.length?`<details ${basic.length?'':'open'}><summary>詳細設定 · ${advanced.length}項目</summary><div class="field-grid">${advanced.map(field).join('')}</div></details>`:''}`;
}
function tabs(items, selected, prefix) {return `<div class="tabs" role="tablist">${items.map(([id,label])=>`<button role="tab" aria-selected="${id===selected}" data-action="tab" data-target="${prefix}/${id}">${esc(label)}</button>`).join('')}</div>`;}
function heading(title,desc) {return `<h1>${title}</h1><p class="lead">${desc}</p>`;}
function cardProvider(cap) {
    const id=state.settings.values[`${cap}_provider`], p=provider(id);
    return `<article class="card feature-card"><div class="actions"><span class="caption">${cap==='chat'?'会話するAI':'読み上げる声'}</span>${pill(statusFor(id))}</div><h2>${esc(p?.label||id)}</h2><p>${esc(cap==='chat'?state.settings.values[`${id}_chat_model`]||'選択中のモデル':p?.description)}</p><div class="actions">${button('go',cap==='chat'?'AIを選ぶ':'声を選ぶ','quiet',`data-target="providers/${cap}"`)}${button('go',cap==='chat'?'会話を試す':'試聴する','',`data-target="playground/${cap}"`)}</div></article>`;
}
function home() {
    return heading('Backendの状態','アプリの接続先、使用中のAIと音声、設定不足を確認します。','YOUR WORKSPACE')+
    `<section class="hero"><div><span class="pill ${state.backend==='ok'?'ok':'warn'}">${state.backend==='ok'?'Backend 起動中':'データベースの確認が必要'}</span><h2>アプリを接続</h2><p>このPCでは表示されたURLをアプリのBackend接続先に設定します。別端末から使う場合は、VPN側の接続先を確認してください。</p></div><div class="connection-box"><div class="caption">このPCのアプリから</div><code>${esc(state.url)}</code><div class="actions">${button('copy-url','URLをコピー','primary')}${button('go','別端末から接続','quiet','data-target="settings/connection"')}</div></div></section>
    <div class="grid">${cardProvider('chat')}${cardProvider('tts')}</div>
    <div class="section-head"><h2>その他の機能</h2><span class="caption">機能別の管理とテスト</span></div><div class="grid three">${[['playground/vision','画像・音声認識・天気','画像理解、文字起こし、音声会話、天気を試す。'],['memory','キャラクターの記憶','Backendに保存した記憶を検索・編集する。'],['activity','履歴と利用状況','会話の振り返りと、今日の利用回数を確認する。']].map(([target,title,desc])=>`<article class="card"><h3>${title}</h3><p class="subtle">${desc}</p>${button('go','開く','link',`data-target="${target}"`)}</article>`).join('')}</div>`;
}
function providerFields(id, cap) {
    return state.settings.fields.filter(f=>{
        if(f.group!==id) return false;
        if(id!=='openai'||f.secret) return true;
        if(cap==='chat') return /openai_(chat_model|max_output_tokens|work_max_output_tokens)$/.test(f.name);
        if(cap==='vision') return f.name.includes('vision');
        if(cap==='stt') return f.name==='openai_transcribe_model';
        return f.name.includes('realtime');
    });
}
function providers() {
    if(section==='tts') return heading('音声の設定','保存した声を編集・試聴し、提供元の接続先を管理します。')+tabs(Object.entries(state.capabilities),'tts','providers')+'<div id=voice-workbench></div><details><summary>既存アプリ向けの提供元設定</summary><a href="#providers/tts-settings">既存の提供元・代替設定を開く</a></details>';
    const cap=section==='tts-settings'?'tts':Object.hasOwn(state.capabilities,section)?section:'chat';
    const opts=state.providers.filter(p=>p.capabilities.includes(cap));
    const id=cap==='realtime'?'openai':current(`${cap}_provider`), p=provider(id)||opts[0];
    const selector=cap==='realtime'?'':`<div class="field"><label for="provider-choice">${esc(state.capabilities[cap])}の提供元</label><select id="provider-choice" data-setting="${cap}_provider" data-rerender="true">${opts.map(o=>`<option value="${o.id}" ${o.id===p.id?'selected':''}>${esc(o.label)}</option>`).join('')}</select></div>`;
    return heading('AIと音声の設定','設定を保存すると、Backend経由の次のリクエストから使われます。アプリのLocal・Direct API設定は変わりません。')+tabs(Object.entries(state.capabilities),cap,'providers')+
    `<div class="workspace"><section class="card">${selector}<div class="section-head"><h2>${esc(p.label)}</h2>${pill(statusFor(p.id))}</div><p>${esc(p.description)}</p>${settingsFields(providerFields(p.id,cap))}${cap==='tts'?`<details><summary>読み上げ失敗時の代替</summary>${field(state.settings.fields.find(f=>f.name==='tts_fallback_provider'))}<p class="subtle">代替で音声が出る場合もあります。希望する声の動作確認は、試聴結果と接続状態を確認してください。</p></details>`:''}</section>
    <aside class="stack"><section class="card"><h3>この設定で動くか確かめる</h3><p class="subtle">接続確認は保存済みの設定を使用します。キーの設定だけでは、認証や生成の成功までは分かりません。</p><div class="actions">${button('probe','接続を確認','quiet',`data-provider="${p.id}"`)}${button('go',cap==='tts'?'試聴する':'試す','primary',`data-target="playground/${cap}"`)}</div><div id="probe-result" class="subtle">${esc(probes[p.id]?.detail||'')}</div></section><section class="info">${p.local?'この接続先のエンジンをPCで起動しておく必要があります。モデルや声のダウンロードは各エンジンで行います。':'外部サービスを選ぶと、入力や必要な記憶がその接続先へ送信されます。API利用料金は各サービスの契約に従います。'}</section>${cap==='realtime'?'<div class="info">音声会話・音声翻訳・文字起こしを試せます。ライブ音声は開始・終了を明示して使います。</div>':''}</aside></div>`;
}
function scopeForm(session=true,trial=false) {
    const s=trial?{user_id:'console-preview',character_id:'console-preview',session_id:'console-preview'}:scope;
    return `<div class="scope"><div class="field-grid"><div class="field"><label for="scope-user">利用者ID</label><input id="scope-user" name="user_id" value="${esc(s.user_id)}" list="users" required maxlength="128"></div><div class="field"><label for="scope-character">キャラクターID（空欄は旧データ）</label><input id="scope-character" name="character_id" value="${esc(s.character_id)}" list="characters" pattern="[A-Za-z0-9_.:-]+" maxlength="128"></div>${session?`<div class="field"><label for="scope-session">セッションID（空欄は旧データ）</label><input id="scope-session" name="session_id" value="${esc(s.session_id)}" list="sessions" maxlength="128"></div>`:''}</div>${!trial?'<small>候補から選ぶかIDを入力し、「表示」を押してください。選んだ範囲だけを表示します。</small>':''}</div>${trial?'':`<datalist id="users">${[...new Set(identities.map(i=>i.user_id))].map(x=>`<option value="${esc(x)}">`).join('')}</datalist><datalist id="characters">${[...new Set(identities.filter(i=>i.user_id===scope.user_id).map(i=>i.character_id).filter(Boolean))].map(x=>`<option value="${esc(x)}">`).join('')}</datalist><datalist id="sessions">${[...new Set(identities.filter(i=>i.user_id===scope.user_id&&i.character_id===(scope.character_id||null)).map(i=>i.session_id).filter(Boolean))].map(x=>`<option value="${esc(x)}">`).join('')}</datalist>`}`;
}
function input(name,label,value='',type='text',extra='') {return `<div class="field"><label for="trial-${name}">${label}</label><input id="trial-${name}" name="${name}" type="${type}" value="${esc(value)}" ${extra}></div>`;}
function textarea(name,label,value='') {return `<div class="field"><label for="trial-${name}">${label}</label><textarea id="trial-${name}" name="${name}" required>${esc(value)}</textarea></div>`;}
function select(name,label,items,value) {return `<div class="field"><label for="trial-${name}">${label}</label><select id="trial-${name}" name="${name}">${items.map(([v,t])=>`<option value="${v}" ${v===value?'selected':''}>${t}</option>`).join('')}</select></div>`;}
const trialTabs=[['chat','会話'],['tts','読み上げ'],['stt','音声認識'],['vision','画像'],['realtime','Realtime'],['weather','天気']];
function playground() {
    const cap=trialTabs.some(x=>x[0]===section)?section:'chat';
    let form='', action='実行する', dest=cap==='weather'?'Open-Meteo':provider(state.settings.values[`${cap}_provider`]||'openai')?.label;
    if(cap==='chat') {form=textarea('message','話しかける','こんにちは。短く自己紹介してください。')+select('mode','応答モード',[['standard','Talk · 短い会話'],['work','Work · 作業を相談']],'standard')+`<details><summary>人格・回答方針・保存範囲</summary>${scopeForm(true,true)}${input('character_name','キャラクター名','Yui')}${input('custom_instruction','性格・口調')}${input('response_instruction','回答の方針')}</details><label class="check"><input type="checkbox" name="secret" checked>試験会話を履歴・記憶に保存しない</label>`;action='送信する';}
    if(cap==='tts') {const id=state.settings.values.tts_provider;form=textarea('text','読み上げる文章','こんにちは。Yuiです。この声でお話ししますね。')+`<div class="field"><label for="voice-choice">声</label><select name="speaker_id" id="voice-choice"><option value="${id==='aivis'?1431611904:14}">既定の声</option></select><small>エンジンの声一覧を取得できます。</small>${button('voices','声一覧を取得','link')}</div><details><summary>声の調整</summary><div class="field-grid">${[['speed_scale','話す速さ',1,.5,2],['pitch_scale','声の高さ',0,-.5,.5],['intonation_scale','抑揚',1,0,2],['volume_scale','音量',1,0,2],['pre_phoneme_length','前の無音（秒）',.1,0,1.5],['post_phoneme_length','後の無音（秒）',.1,0,1.5]].map(([n,l,v,min,max])=>input(n,l,v,'number',`min="${min}" max="${max}" step="0.05"`)).join('')}${input('voice_gender','性別指定')}${input('voice_instruct','演技指示')}${input('voice_lang_code','言語コード')}</div></details>`;action='生成して試聴';}
    if(cap==='stt') {form=input('audio','音声ファイル','','file','accept="audio/*" required')+input('duration_ms','録音時間（ミリ秒・任意）','','number','min="0"')+'<p class="subtle">音声ファイルは25MBまで。実行すると、選択した音声認識サービスに送信します。</p>';action='文字にする';}
    if(cap==='vision') {form=input('image','画像ファイル','','file','accept="image/jpeg,image/png,image/webp,image/heic,image/heif" required')+select('prompt_type','読み取りの目的',[['screen','画面の内容'],['camera','カメラの写真']],'screen')+'<p class="subtle">画像は20MBまで。送信するファイルの内容をご確認ください。</p>';action='画像を読み取る';}
    if(cap==='weather') {form=input('location','調べる場所','東京','text','required');action='天気を調べる';}
    if(cap==='realtime') {form=select('mode','使い方',[['voice','音声会話'],['voice_text','音声→テキスト回答'],['translate','音声翻訳'],['transcribe','文字起こし']],'voice')+input('audio','WAVファイル（ファイルで試す場合）','','file','accept="audio/wav,.wav"')+input('instructions','話し方・翻訳の指示')+'<div class="info">ファイルで試す場合はWAVを選択。ライブはマイクを使用し、接続中の音声をOpenAIへ送信します。どちらも試験会話を保存しません。</div><div class="actions">'+button('live-start','ライブ音声を開始','quiet')+button('live-stop','ライブを終了','danger','hidden')+'</div><p id="live-status" class="subtle"></p>';action='WAVで試す';}
    return heading('動作テスト','保存済みの設定で会話、音声、画像理解、天気をテストします。')+tabs(trialTabs,cap,'playground')+
    `<div class="workspace"><section class="card"><form id="trial-form" data-cap="${cap}">${form}<hr><div class="actions"><button class="primary" type="submit">${action}</button>${button('cancel-trial','待機をキャンセル','quiet','hidden')}</div><p class="subtle">送信先: ${esc(dest)}${cap==='weather'?'':' · 外部APIでは利用料金が発生する場合があります。'}</p></form></section><section class="card"><h3>結果</h3><div id="trial-result" class="result empty">ここに実行結果が表示されます。</div><div id="trial-audio"></div></section></div>`;
}
function memory() {
    return heading('記憶の管理','ここで扱うのはBackendの記憶です。端末内の記憶とは別で、編集・削除は選択したキャラクターにだけ適用します。')+
    `<form id="scope-form">${scopeForm(false)}<div class="actions"><input name="query" aria-label="記憶の検索" placeholder="記憶を検索" id="memory-query"><button class="primary">表示</button>${button('add-memory','記憶を追加','quiet')}</div></form><div id="memory-editor"></div><section class="card" id="data-result"><div class="empty-state">利用者とキャラクターを選び、「表示」を押してください。</div></section><div class="actions section-head">${button('previous','前の20件','quiet','disabled')}${button('next','次の20件','quiet','disabled')}</div>`;
}
function activity() {
    return heading('履歴と利用状況','会話履歴は利用者・キャラクター・セッションごとに分かれています。利用回数は指定した利用者の今日の合計です。')+
    `<form id="scope-form">${scopeForm(true)}<button class="primary">表示</button></form><div id="usage-result"></div><section class="card" id="data-result"><div class="empty-state">表示する範囲を選んでください。</div></section><div class="section-head actions">${button('previous','前の20件','quiet','disabled')}${button('next','次の20件','quiet','disabled')}</div><details><summary>この範囲のデータを削除</summary><p>選択したセッションの会話と応答キャッシュを削除します。記憶を含める場合は、そのキャラクターの全セッション共通の記憶を削除します。端末のデータと利用回数は残ります。</p><label class="check"><input id="delete-memories" type="checkbox">このキャラクターのBackend記憶も含める</label>${input('confirmation','確認のため利用者IDを入力')}${button('clear-scope','選択した範囲を削除','danger')}</details>`;
}
function settings() {
    const selected=['connection','search','limits','cache','general','weather','extensions'].includes(section)?section:'connection';
    const menu=[['connection','接続'],['search','Web検索'],['limits','利用目安'],['cache','保存'],['general','共通設定'],['weather','天気'],['extensions','拡張']];
    let content;
    if(selected==='connection') content=`<div class="workspace"><section class="card"><h2>Yuiアプリとの接続</h2><div class="connection-box"><div class="caption">同じPCから</div><code>${esc(state.url)}</code>${button('copy-url','コピー','link')}</div><h3 class="section-head">別のPC・iPhoneから</h3><ol><li>Backendとアプリの端末を同じ信頼できるVPNへ接続。</li><li>BackendをVPNから到達できるアドレスで起動。</li><li>アプリに <code>http://VPNのIP:ポート</code> を設定し、接続確認。</li></ol><div class="info warning">localhostは別端末から使えません。管理画面はこのPCだけで開けます。アプリ用APIは認証を備えていないため、インターネットへ直接公開しないでください。</div></section><aside class="stack"><section class="card"><h3>Backendの状態</h3><div class="rows"><div class="row">API / DB${pill(state.backend==='ok'?'ok':'offline')}</div><div class="row">管理画面<span>このPCのみ</span></div><div class="row">他の端末からの接続<span class="pill">実機確認が必要</span></div></div><details><summary>起動方法</summary><p>macOS: 既存の <code>scripts/start_mobile_backend_macos.sh</code> は外部待受を使います。VPNの範囲を確認して起動してください。通常起動は <code>scripts/run_backend_macos.sh</code> です。</p><p>CLI: <code>BACKEND_HOST</code> と <code>BACKEND_PORT</code> を設定して再起動。管理UIはlocalhost限定を維持します。</p></details></section><div class="info">画面が開けることと、アプリから到達できることは別です。VPN接続はアプリ側でも確認してください。</div></aside></div>`;
    else if(selected==='extensions') content=`<section class="card"><h2>拡張できること・これからの機能</h2>${state.extensions.map(x=>`<div class="row"><div><h3>${esc(x.name)}</h3><p>${esc(x.detail)}</p></div><span class="pill">${esc(x.state)}</span></div>`).join('')}<hr><p>外部TTSは、対応HTTP形式のサーバーを接続できます。新しいLLM・STT・TTS提供元は能力定義と実行アダプターを登録することで追加します。</p>${button('go','外部TTSを設定','primary','data-target="providers/tts"')}</section>`;
    else content=`<section class="card"><h2>${esc(menu.find(x=>x[0]===selected)[1])}</h2>${selected==='limits'?'<div class="info">ここは日次利用の目安です。現在のBackendでは、この値で通信や課金を自動停止しません。正確な料金と上限は各API提供元で確認してください。</div>':''}${selected==='search'?'<p>OpenAIの会話で使うWeb検索です。ローカルLLMやすべての提供元で使える機能ではありません。</p>':''}${selected==='cache'?'<p>生成音声キャッシュの保持設定です。キャラクターの記憶・会話履歴とは別です。</p>':''}${settingsFields(state.settings.fields.filter(f=>f.group===selected&&!f.name.endsWith('_provider')))}</section>`;
    return heading('接続と設定','アプリとの接続、検索、利用目安、保存を設定します。変更後は「保存して反映」を押してください。')+tabs(menu,selected,'settings')+content;
}
async function render() {
    const route=location.hash.slice(1).split('/');view=NAV.some(x=>x[0]===route[0])?route[0]:'home';section=route[1]||'';page=0;const ticket=++generation;
    $('#nav').innerHTML=NAV.map(([id,label],i)=>`<a href="#${id}" ${id===view?'aria-current="page"':''}><span class="navnum">0${i+1}</span>${label}</a>`).join('');
    $('#breadcrumb').textContent=`ワークスペース / ${NAV.find(x=>x[0]===view)[1]}`;
    if(['memory','activity'].includes(view)) {try{identities=(await api('api/identities')).items;}catch(e){notice(e.message);}}
    if(ticket!==generation)return;
    $('#content').innerHTML=({home,providers,playground,memory,activity,settings})[view]();changed();
    $('#content').querySelectorAll('[data-setting]').forEach(el=>el.addEventListener(el.matches('select,[type=checkbox]')?'change':'input',()=>{const f=state.settings.fields.find(f=>f.name===el.dataset.setting);let v=f.type==='boolean'?el.checked:f.type==='number'?Number(el.value):el.value;if(f.secret&&!v){delete draft[f.name];changed();return;}draft[f.name]=v;if(!f.secret&&v===state.settings.values[f.name])delete draft[f.name];changed();if(el.dataset.rerender)render();}));
    if($('#voice-workbench')){voiceEditor ||= createVoiceEditor(api,notice,state.settings.fields,confirmAction);voiceEditor.mount($('#voice-workbench')).catch(e=>notice(e.message));}
    $('#trial-form')?.addEventListener('submit',runTrial);
    $('#scope-form')?.addEventListener('submit',e=>{e.preventDefault();readScope();page=0;loadData().catch(e=>notice(e.message));});
}
function readScope() {const f=new FormData($('#scope-form'));scope={user_id:String(f.get('user_id')||'').trim(),character_id:String(f.get('character_id')||'').trim(),session_id:String(f.get('session_id')||'').trim()};}
async function loadData() {
    if(!scope.user_id)throw new Error('利用者IDを指定してください。');const ticket=generation, dataTicket=++dataGeneration, loadingScope={...scope};
    const args={user_id:scope.user_id,character_id:scope.character_id||null};let items;
    if(view==='memory') {items=(await api('api/run/memory/search',{method:'POST',body:{...args,query:$('#memory-query').value,limit:20,offset:page*20}})).items;}
    else {const q=new URLSearchParams({user_id:scope.user_id,limit:'20',offset:String(page*20)});if(scope.character_id)q.set('character_id',scope.character_id);if(scope.session_id)q.set('session_id',scope.session_id);items=(await api('api/run/conversations/recent?'+q)).items;}
    if(ticket!==generation||dataTicket!==dataGeneration)return;
    $('#data-result')._scope=loadingScope;
    $('#data-result').innerHTML=items.length?(view==='memory'?items.map(x=>`<article class="memory"><div class="meta"><span>重要度 ${x.importance}/5 · ${esc(x.tags.join(' / '))}</span><span>#${esc(x.id)}</span></div><p>${esc(x.content)}</p><div class="actions">${button('edit-memory','編集','quiet',`data-id="${esc(x.id)}"`)}${button('delete-memory','削除','danger',`data-id="${esc(x.id)}"`)}</div></article>`).join(''):items.map(x=>`<article class="message ${x.role==='user'?'user':''}"><small>${x.role==='user'?'あなた':'AI'} · ${esc(x.created_at)} UTC</small><p>${esc(x.message)}</p></article>`).join('')):'<div class="empty-state">この範囲のデータはありません。</div>';
    $('#data-result')._items=items;
    $('[data-action=previous]').disabled=page===0;$('[data-action=next]').disabled=items.length<20;
    if(view==='activity'){const u=await api('api/run/usage?user_id='+encodeURIComponent(scope.user_id));if(ticket!==generation||dataTicket!==dataGeneration)return;$('#usage-result').innerHTML=`<div class="grid">${[['会話',u.chat_count,u.limits.chat_count,'回'],['読み上げ',u.tts_count,u.limits.tts_count,'回'],['画像',u.vision_count,u.limits.vision_count,'回'],['音声認識',u.stt_minutes.toFixed(1),u.limits.stt_minutes,'分']].map(([label,num,limit,unit])=>`<div class="card"><span class="caption">今日の${label}</span><div class="metric">${num} <small>${unit}</small></div><small>目安 ${limit}${unit} / 自動停止なし</small></div>`).join('')}</div>`;}
}
function editMemory(item) {
    if(item)scope={...$('#data-result')._scope};else readScope();if(!scope.user_id){notice('利用者IDを指定してください。');return;}
    $('#memory-editor').innerHTML=`<section class="card"><h3>${item?'記憶を編集':'記憶を追加'}</h3><p class="subtle">${esc(scope.user_id)} / ${esc(scope.character_id||'旧データ（キャラクターなし）')}</p><form id="memory-form">${textarea('content','覚えておく内容',item?.content||'')}<div class="field-grid">${input('importance','重要度（1〜5）',item?.importance||3,'number','min="1" max="5" required')}${input('tags','タグ（カンマ区切り）',item?.tags.join(', ')||'')}</div><div class="actions"><button class="primary">記憶を保存</button>${button('close-editor','キャンセル','quiet')}</div></form></section>`;
    const editingScope={...scope};$('#memory-form').onsubmit=async e=>{e.preventDefault();const f=new FormData(e.target);try{await api(`api/run/memory/${item?item.id+'/update':'save'}`,{method:'POST',body:{user_id:editingScope.user_id,character_id:editingScope.character_id||null,content:f.get('content'),importance:Number(f.get('importance')),tags:String(f.get('tags')).split(',').map(s=>s.trim()).filter(Boolean)}});$('#memory-editor').innerHTML='';notice('記憶を保存しました。');await loadData();}catch(e){notice(e.message);}};
    $('#trial-content').focus();
}
function playBlob(blob) {if(audioUrl)URL.revokeObjectURL(audioUrl);audioUrl=URL.createObjectURL(blob);$('#trial-audio').innerHTML=`<audio controls src="${audioUrl}"></audio>`;$('#trial-audio audio').play().catch(()=>{});}
function pcmBlob(encoded,rate=24000) {const raw=atob(encoded),bytes=new Uint8Array(raw.length);for(let i=0;i<raw.length;i++)bytes[i]=raw.charCodeAt(i);const h=new ArrayBuffer(44),v=new DataView(h),put=(s,o)=>[...s].forEach((c,i)=>v.setUint8(o+i,c.charCodeAt(0)));put('RIFF',0);v.setUint32(4,36+bytes.length,true);put('WAVEfmt ',8);v.setUint32(16,16,true);v.setUint16(20,1,true);v.setUint16(22,1,true);v.setUint32(24,rate,true);v.setUint32(28,rate*2,true);v.setUint16(32,2,true);v.setUint16(34,16,true);put('data',36);v.setUint32(40,bytes.length,true);return new Blob([h,bytes],{type:'audio/wav'});}
async function runTrial(event) {
    event.preventDefault();if(activeRequest)return;const f=new FormData(event.target),cap=event.target.dataset.cap,ticket=generation;
    if(Object.keys(draft).length&&!await confirmAction('未保存の変更があります','試す操作には保存済みの設定が使われます。編集中の設定を試す場合は先に保存してください。','保存済みの設定で試す'))return;
    activeRequest=new AbortController();const signal=activeRequest.signal;const submit=event.target.querySelector('[type=submit]');submit.disabled=true;$('[data-action=cancel-trial]').hidden=false;$('#trial-result').classList.remove('empty');$('#trial-result').textContent='応答を待っています…';$('#trial-audio').innerHTML='';const start=performance.now();
    try {
        let result;
        if(cap==='chat'){result=await api('api/run/chat',{method:'POST',signal,body:{request_id:crypto.randomUUID(),user_id:f.get('user_id'),character_id:f.get('character_id')||null,session_id:f.get('session_id')||null,mode:f.get('mode'),message:f.get('message'),secret:f.get('secret')==='on',character_name:f.get('character_name'),custom_instruction:f.get('custom_instruction'),response_instruction:f.get('response_instruction')}});}
        else if(cap==='tts'){const body={request_id:crypto.randomUUID(),text:f.get('text'),speaker_id:Number(f.get('speaker_id'))};for(const [k,v] of f)if(k!=='text'&&k!=='speaker_id'&&v!=='')body[k]=['voice_gender','voice_instruct','voice_lang_code'].includes(k)?v:Number(v);result=await api('api/run/tts/audio',{method:'POST',signal,body,blob:true});}
        else if(cap==='weather'){result=await api('api/run/external/weather/current?location='+encodeURIComponent(f.get('location')),{signal});}
        else {const name=cap==='vision'?'image':'audio',file=f.get(name);if(!(file instanceof File)||!file.size)throw new Error('ファイルを選択してください。');if(file.size>(cap==='vision'?20:25)*1024*1024)throw new Error('ファイルが上限を超えています。');if(cap==='stt'&&!f.get('duration_ms'))f.delete('duration_ms');if(cap==='realtime'){f.set('secret','true');f.set('user_id','console-preview');f.set('character_id','console-preview');}result=await api('api/run/'+(cap==='realtime'?'realtime/audio':cap),{method:'POST',signal,body:f});}
        if(ticket!==generation)return;
        const elapsed=((performance.now()-start)/1000).toFixed(1);
        if(result instanceof Blob){$('#trial-result').textContent=`音声を生成しました · ${elapsed}秒\n選択中の提供元が失敗した場合は、設定した代替の声が使われます。`;playBlob(result);}
        else {$('#trial-result').textContent=(result.text||result.summary||JSON.stringify(result,null,2))+`\n\n${elapsed}秒`;if(result.audio_base64)playBlob(pcmBlob(result.audio_base64,result.sample_rate));}
    }catch(e){if(ticket===generation){$('#trial-result').textContent=e.name==='AbortError'?'画面での待機を中止しました。送信済みの処理や課金が取り消されるとは限りません。':e.message;if(e.trace)$('#trial-result').insertAdjacentHTML('beforeend',button('show-diagnostics','原因と対処を確認','quiet',`data-trace="${esc(e.trace)}"`));}}
    finally {activeRequest=null;if(ticket===generation){submit.disabled=false;$('[data-action=cancel-trial]').hidden=true;}}
}
async function handle(action,el) {
    if(action==='show-diagnostics'){diagnostics.open(el.dataset.trace);return;}
    if(action==='go'||action==='tab'){location.hash=el.dataset.target;return;}
    if(action==='copy-url'){try{await navigator.clipboard.writeText(state.url);notice('接続先URLをコピーしました。');}catch{notice('コピーできませんでした。表示されたURLを選択してコピーしてください。');}return;}
    if(action==='probe'){el.disabled=true;try{const r=await api('api/probe/'+el.dataset.provider,{method:'POST'});probes[el.dataset.provider]=r;const badge=$('#content .workspace section.card .section-head .pill');if(badge){badge.textContent=labels[r.status]||r.status;badge.classList.toggle('ok',r.status==='ok');badge.classList.toggle('warn',['offline','missing_key'].includes(r.status));}$('#probe-result').innerHTML=esc(r.detail)+(['offline','missing_key','not_configured'].includes(r.status)&&r.diagnosticTrace?button('show-diagnostics','原因と対処を確認','quiet',`data-trace="${esc(r.diagnosticTrace)}"`):'');notice(r.detail);}finally{el.disabled=false;}return;}
    if(action==='voices'){el.disabled=true;try{const r=await api('api/probe/'+state.settings.values.tts_provider,{method:'POST'});if(r.voices?.length)$('#voice-choice').innerHTML=r.voices.map(v=>`<option value="${esc(v.id)}">${esc(v.label)}</option>`).join('');else notice(r.detail);}finally{el.disabled=false;}return;}
    if(action==='clear-key'){if(await confirmAction('APIキーを解除しますか','保存すると、このBackendではこのキーを使わなくなります。元の.envファイルは変更しません。','解除を設定')){draft[el.dataset.key]=null;render();}return;}
    if(action==='cancel-trial'){activeRequest?.abort();return;}
    if(action==='previous'||action==='next'){page+=action==='next'?1:-1;await loadData();return;}
    if(action==='add-memory'){editMemory();return;}
    if(action==='edit-memory'){editMemory($('#data-result')._items.find(x=>x.id===el.dataset.id));return;}
    if(action==='close-editor'){$('#memory-editor').innerHTML='';return;}
    if(action==='delete-memory'){scope={...$('#data-result')._scope};if(await confirmAction('この記憶を削除しますか',`${scope.user_id} / ${scope.character_id||'旧データ'} の記憶 #${el.dataset.id} を削除します。取り消せません。`,'削除する')){await api(`api/run/memory/${el.dataset.id}/delete`,{method:'POST',body:{user_id:scope.user_id,character_id:scope.character_id||null}});await loadData();notice('選択した記憶を削除しました。');}return;}
    if(action==='clear-scope'){readScope();const shown=$('#data-result')._scope;if(!shown||['user_id','character_id','session_id'].some(k=>shown[k]!==scope[k]))throw new Error('削除する範囲を先に「表示」で確認してください。');if($('#trial-confirmation').value!==scope.user_id)throw new Error('確認用の利用者IDが一致しません。');const include=$('#delete-memories').checked;if(await confirmAction('データを削除しますか',`${scope.user_id} / ${scope.character_id||'旧キャラクター'} / ${scope.session_id||'旧セッション'} の会話・応答キャッシュ${include?'と、このキャラクター全体のBackend記憶':''}を削除します。取り消せません。`,'削除する')){await api('api/clear-scope',{method:'POST',body:{...scope,character_id:scope.character_id||null,session_id:scope.session_id||null,include_memories:include,confirmation:$('#trial-confirmation').value}});await loadData();notice('指定した範囲を削除しました。');}return;}
    if(action==='live-start'){const f=new FormData($('#trial-form'));if(f.get('mode')==='transcribe')throw new Error('文字起こしモードはWAVファイルでお試しください。');el.disabled=true;try{await startLive(f.get('mode'),f.get('instructions'),(text)=>{if($('#trial-result')){$('#trial-result').classList.remove('empty');$('#trial-result').textContent=text;}},text=>{if($('#live-status'))$('#live-status').textContent=text;});el.hidden=true;$('[data-action=live-stop]').hidden=false;}finally{el.disabled=false;}return;}
    if(action==='live-stop'){await stopLive();$('[data-action=live-start]').hidden=false;el.hidden=true;$('#live-status').textContent='ライブを終了しました。';}
}
$('#content').addEventListener('click',event=>{const el=event.target.closest('[data-action]');if(el)handle(el.dataset.action,el).catch(e=>notice(e.message));});
$('#save').onclick=async()=>{const b=$('#save');b.disabled=true;try{state.settings=await api('api/settings',{method:'PATCH',body:{revision:state.settings.revision,changes:draft}});draft={};probes={};voiceEditor?.refresh();notice('保存しました。次のリクエストから反映されます。');await render();}catch(e){notice(e.message);}finally{b.disabled=false;}};
$('#discard').onclick=async()=>{if(await confirmAction('変更を戻しますか','未保存の変更を破棄し、現在の設定に戻します。','戻す')){draft={};render();}};
$('#refresh').onclick=async()=>{if(Object.keys(draft).length){notice('未保存の変更があります。保存するか変更を戻してから更新してください。');return;}try{state=await api('api/overview');probes={};voiceEditor?.refresh();await render();notice('現在の設定を読み込みました。');}catch(e){notice(e.message);}};
window.addEventListener('beforeunload',e=>{if(Object.keys(draft).length||voiceEditor?.dirty()||activeRequest){e.preventDefault();e.returnValue='';}});
window.addEventListener('hashchange',async()=>{if(location.hash==='#content'){$('#content').focus();return;}activeRequest?.abort();await stopLive();await render();window.scrollTo({top:0});});
diagnostics=bindDiagnostics(api);
try {await api('session',{method:'POST'});state=await api('api/overview');scope.user_id=state.settings.values.default_user_id;await render();}
catch(e){$('#content').innerHTML=heading('Backendに接続できません。',esc(e.message))+'<p>Backendが起動しているPCのlocalhostで開き直してください。</p>';}
