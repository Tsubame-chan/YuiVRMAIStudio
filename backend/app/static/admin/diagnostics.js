// Request-level diagnostics. Raw terminal output and user content are never read.
const operations={chat:'会話',tts:'読み上げ',stt:'音声認識',vision:'画像理解',realtime:'Realtime',weather:'天気',probe:'接続確認',settings:'設定保存'};
const providerNames={openai:'OpenAI',voicevox:'VOICEVOX',aivis:'AivisSpeech',http:'外部TTS',lmstudio:'LM Studio',litert_lm:'LiteRT-LM',xai:'xAI',gemini:'Gemini'};
const stages={started:'受付',context_ready:'参照情報の準備',generating:'提供元へ処理を依頼',provider_selected:'接続先の選択',fallback:'代替提供元へ変更',cached:'保存済みの応答を使用',completed:'終了',failed:'失敗',issue:'原因の切り分け',configured_only:'キーの設定有無のみ確認',connection_ok:'接続先から応答',probe_skipped:'接続確認を未実行'};
const issues={
 missing_key:['APIキーが設定されていません。','該当する提供元にキーを設定し、保存してから動作テストを実行してください。'],
 missing_endpoint:['接続先URLが設定されていません。','使うエンジンの接続先を設定し、保存してください。'],
 authentication:['提供元が認証を拒否しました。','APIキーが有効か、接続先と契約が一致しているか確認してください。'],
 quota:['提供元の利用枠または残高で拒否されました。','提供元の管理画面で残高・契約の利用上限を確認してください。'],
 rate_limit:['提供元が利用頻度の上限で拒否しました。','少し待ってから再実行してください。繰り返す場合は提供元の上限を確認してください。'],
 unreachable:['接続先へ到達できませんでした。','ローカルエンジンの起動、接続先URL、ネットワークを確認してください。未起動とは断定できません。'],
 timeout:['応答待ちがタイムアウトしました。','接続確認を実行してください。接続できる場合はモデルの負荷や処理時間を確認してください。'],
 model_not_found:['指定したモデルを提供元が見つけられませんでした。','モデル名と、その契約・接続先で利用できるモデルを確認してください。'],
 not_found:['提供元が指定した接続先を見つけられませんでした。','モデル名、接続先URL、APIパスを確認してください。'],
 provider_unavailable:['提供元がサーバーエラーを返しました。','ローカルエンジンなら状態を確認し、外部APIなら提供元の復旧後に再実行してください。'],
 permission:['保存先への書き込みが拒否されました。','保存先のアクセス権を確認してください。設定保存の場合は既存の設定を保持しています。'],
 disk_full:['保存先の空き容量が不足しています。','必要なデータを保護して空き容量を確保し、再実行してください。'],
 settings_conflict:['別の画面で設定が変更されています。','未保存の変更を戻し、状態を更新してから設定し直してください。'],
 invalid_settings:['設定値を保存できませんでした。','選択肢、URL、数値の範囲を確認してください。既存の設定は保持しています。'],
 unsupported:['この提供元・機能の組合せは実装されていません。','対応する提供元を選択してください。'],
 provider_error:['応答を取得できませんでした。原因は確定できていません。','接続確認で設定と到達性を切り分けてください。解決しなければ診断情報を控えて確認してください。'],
 invalid_input:['入力を受け付けられませんでした。','必須項目、ファイル形式・容量、入力値を確認してください。'],
 backend_error:['Backendが処理を完了できませんでした。','接続確認で切り分けてください。この記録だけでは原因を断定できません。'],
};
const safe=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function target(g){const p=g.providers.at(-1);const cap=g.operation==='probe'?(['voicevox','aivis','http'].includes(p)?'tts':p==='gemini'?'vision':'chat'):g.operation;return ['chat','tts','stt','vision','realtime'].includes(cap)?'providers/'+cap:'settings';}
function groupEvents(rows){
 const groups=new Map();
 for(const event of rows){if(!event.trace)continue;let g=groups.get(event.trace);if(!g){g={trace:event.trace,events:[],providers:[],operation:event.operation,time:event.time};groups.set(event.trace,g);}g.events.push(event);if(event.provider&&!g.providers.includes(event.provider))g.providers.push(event.provider);if(event.model)g.model=event.model;}
 return [...groups.values()].map(g=>{g.end=g.events.findLast(x=>['completed','failed'].includes(x.stage));g.issue=g.events.findLast(x=>x.stage==='issue');g.fallback=g.events.find(x=>x.stage==='fallback');g.counts=g.events.findLast(x=>x.counts)?.counts;g.failed=!!g.issue||g.end?.stage==='failed';g.running=!g.end;return g;}).reverse();
}
export function bindDiagnostics(api){
 const panel=document.querySelector('#diagnostics'),toggle=document.querySelector('#diagnostics-toggle'),list=document.querySelector('#event-list'),status=document.querySelector('#event-status');
 let opened=false,paused=false,timer,cursor=0,rows=[],busy=false,gap=false,selected='',groups=[],firstLoad=true;
 function card(g){
  const onlyConfig=g.events.some(x=>x.stage==='configured_only'),skipped=g.events.some(x=>x.stage==='probe_skipped');
  const elapsed=g.end?.elapsed_ms!==undefined?g.end.elapsed_ms/1000:(Date.now()-Date.parse(g.time))/1000;
  const code=g.issue?.code||(g.failed?([400,413,415,422].includes(g.end?.status)?'invalid_input':'backend_error'):g.fallback?.code);
  const issue=issues[code]||issues.provider_error;
  let result=g.failed?'失敗':g.running?'応答待ち':g.fallback?'代替で完了':onlyConfig?'設定のみ確認':skipped?'未検証':'完了';
  let explanation=g.failed?issue[0]:g.running?`受付から${elapsed.toFixed(1)}秒経過。${g.events.some(x=>x.stage==='generating')?'提供元からの応答はまだ返っていません。':'まだ処理が完了していません。'}`:g.fallback?'選択した提供元では生成できず、代替の提供元で音声を出力しました。':onlyConfig?'キーの設定有無だけを確認しました。認証・モデルの応答はまだ未確認です。':skipped?'接続確認パスが未設定のため、生成できるかは未確認です。':g.events.some(x=>x.stage==='cached')?'保存済みの応答を使用しました。今回は生成し直していません。':`${elapsed.toFixed(1)}秒で処理を完了しました。`;
  const providers=g.providers.map(x=>providerNames[x]||x).join(' → ');
  const counts=g.counts?`<div class="diagnostic-context"><strong>今回準備した参考情報</strong><p>履歴 ${g.counts.history||0}件・Backend記憶 ${g.counts.memories||0}件${g.counts.local_memory_chars?`・受信した端末記憶 ${g.counts.local_memory_chars}文字`:''}${g.counts.screen_chars?`・受信した画面の参考文 ${g.counts.screen_chars}文字`:''}</p><small>件数は内容の正しさや、AIが理解したことの保証ではありません。</small></div>`:'';
  return `<article class="diagnostic-card ${g.failed?'error':g.fallback?'warning':''} ${g.trace===selected?'selected':''}" data-trace="${safe(g.trace)}"><div class="event-meta"><time>${safe(new Date(g.time).toLocaleTimeString('ja-JP'))}</time><span>${result}</span></div><h3>${safe(operations[g.operation]||g.operation)}${providers?' · '+safe(providers):''}</h3>${g.model?`<div class="subtle">モデル: ${safe(g.model)}</div>`:''}<p>${safe(explanation)}</p>${g.failed?`<div class="diagnostic-advice"><strong>次に確認すること</strong><p>${safe(issue[1])}</p></div>`:g.fallback?`<div class="diagnostic-advice"><strong>希望した提供元を直すには</strong><p>${safe(issue[0]+' '+issue[1])}</p></div>`:''}${counts}<div class="actions">${g.failed||g.fallback?`<button class="quiet" data-diagnostic-go="${target(g)}">関連設定を開く</button>`:onlyConfig||skipped?`<button class="quiet" data-diagnostic-go="playground/${target(g).split('/')[1]||'chat'}">動作テストを開く</button>`:''}${g.counts?'<button class="link" data-diagnostic-go="memory">記憶を確認</button>':''}</div><details data-detail="${safe(g.trace)}"><summary>技術情報・処理の内訳</summary><p class="subtle">診断ID ${safe(g.trace)}${g.end?.status?` · Backend HTTP ${g.end.status}`:''}${code?' · 分類 '+safe(code):''}</p><ol class="diagnostic-timeline">${g.events.map(x=>`<li>${safe(stages[x.stage]||x.stage)}${x.provider?' · '+safe(providerNames[x.provider]||x.provider):''}</li>`).join('')}</ol><button class="link" data-diagnostic-copy="${safe(g.trace)}">診断情報をコピー</button></details></article>`;
 }
 function render(){
  const expanded=new Set([...list.querySelectorAll('details[open]')].map(x=>x.dataset.detail||'successes'));
  groups=groupEvents(rows);const filter=document.querySelector('#event-filter').value,windowMs=Number(document.querySelector('#event-window').value);
  const visible=groups.filter(g=>(!windowMs||Date.now()-Date.parse(g.time)<=windowMs||g.running)&&(!selected||g.trace===selected)&&(filter==='all'||filter==='failed'&&(g.failed||g.fallback)||filter==='running'&&g.running));
  const important=visible.filter(g=>g.running||g.failed||g.fallback||g.events.some(x=>['configured_only','probe_skipped'].includes(x.stage))),done=visible.filter(g=>!important.includes(g));
  list.innerHTML=important.map(card).join('')+(done.length?`<details class="diagnostic-successes"><summary>完了した処理（${done.length}件）</summary>${done.map(card).join('')}</details>`:'')||'<div class="empty-state">この条件の処理はありません。失敗した処理や応答待ちがあると、原因・経過時間・確認先をここに表示します。</div>';
  list.querySelectorAll('details').forEach(x=>{x.open=expanded.has(x.dataset.detail||'successes')||(selected&&!x.dataset.detail);});
  document.querySelector('#event-clear-selection').hidden=!selected;
  status.textContent=paused?'更新を一時停止中':gap?'更新中 · 一部の古い記録は残っていません':'更新中';
 }
 async function poll(){
  if(!opened||paused||busy)return;busy=true;
  try{const data=await api('api/events?after='+cursor);if(!opened||paused)return;if(data.reset){rows=[];gap=false;}cursor=data.cursor;gap=gap||data.truncated;rows.push(...data.items);const cutoff=Date.now()-24*60*60*1000;rows=rows.filter(x=>Date.parse(x.time)>=cutoff).slice(-1000);if(firstLoad||data.items.length||data.reset||groups.some(g=>g.running)){render();firstLoad=false;}}
  catch{status.textContent='記録を取得できません。Backendが起動しているか確認し、管理画面を再読み込みしてください。';}
  finally{busy=false;if(opened&&!paused)timer=setTimeout(poll,1500);}
 }
 function setOpen(value){opened=value;paused=false;document.querySelector('#event-pause').setAttribute('aria-pressed','false');document.querySelector('#event-pause').textContent='更新を一時停止';panel.hidden=!value;toggle.setAttribute('aria-expanded',String(value));document.body.classList.toggle('diagnostics-open',value);clearTimeout(timer);if(value){poll();document.querySelector('#diagnostics-close').focus();}else toggle.focus();}
 toggle.onclick=()=>{selected='';setOpen(!opened);};document.querySelector('#diagnostics-close').onclick=()=>setOpen(false);
 for(const id of ['event-filter','event-window'])document.querySelector('#'+id).onchange=()=>{selected='';render();};
 document.querySelector('#event-clear-selection').onclick=()=>{selected='';render();};
 document.querySelector('#event-pause').onclick=e=>{paused=!paused;e.target.setAttribute('aria-pressed',String(paused));e.target.textContent=paused?'更新を再開':'更新を一時停止';clearTimeout(timer);if(paused)status.textContent='更新を一時停止中';else poll();};
 list.onclick=async e=>{const go=e.target.closest('[data-diagnostic-go]');if(go){if(innerWidth<1280)setOpen(false);location.hash=go.dataset.diagnosticGo;return;}const copy=e.target.closest('[data-diagnostic-copy]');if(copy){const g=groups.find(x=>x.trace===copy.dataset.diagnosticCopy);if(!g)return;try{await navigator.clipboard.writeText(JSON.stringify({diagnostic_id:g.trace,operation:g.operation,time:g.time,providers:g.providers,model:g.model,events:g.events},null,2));copy.textContent='コピーしました';}catch{copy.textContent='コピーできませんでした';}}};
 panel.addEventListener('keydown',e=>{if(e.key==='Escape')setOpen(false);});
 return {open(trace=''){selected=trace;document.querySelector('#event-filter').value='all';document.querySelector('#event-window').value='0';setOpen(true);render();}};
}
