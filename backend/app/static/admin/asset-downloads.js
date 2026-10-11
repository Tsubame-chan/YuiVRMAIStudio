// Acquisition outlives navigation/reload. Keep progress separate from editor drafts.
const esc = v => String(v ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const active = new Set(['queued','downloading','verifying','installing','preparing','cancelling']);
const phases = {queued:'準備中',downloading:'ダウンロード中',verifying:'ファイルを検証中',installing:'音声エンジンを導入中',preparing:'最初の音声を準備中',cancelling:'中止しています',cancelled:'中止しました',failed:'準備できませんでした',ready:'準備できました'};
const size = n => n >= 1e9 ? `${(n/1e9).toFixed(2)} GB` : `${(n/1e6).toFixed(1)} MB`;
const button=(action,label,id,cls='quiet')=>`<button type="button" class="${cls}" data-acquire="${action}" data-asset="${esc(id)}">${label}</button>`;

export function bindAssetDownloads(api,notice,confirmAction,reloadVoices) {
    const banner=document.querySelector('#asset-progress');
    let assets=[], jobs=[], catalogError='', connected=true, mounted, entryKey='', bannerKey='', dismissed='', compact=true, pending=false, timer;
    const observer=new ResizeObserver(()=>{
        const save=document.querySelector('#savebar');
        const saveHeight=save.hidden?0:save.getBoundingClientRect().height+12;
        document.documentElement.style.setProperty('--acquisition-save-height',`${saveHeight}px`);
        document.documentElement.style.setProperty('--acquisition-height',`${banner.hidden?0:banner.getBoundingClientRect().height+20}px`);
    });
    observer.observe(banner);observer.observe(document.querySelector('#savebar'));
    function job(id){return jobs.find(j=>j.asset_id===id);}
    function paintEntry(){
        const target=document.querySelector('#asset-acquisition');
        if(!target)return;
        const key=JSON.stringify([assets,catalogError,jobs.map(j=>[j.asset_id,j.phase,j.installed]),pending]);
        if(target===mounted&&key===entryKey)return;
        mounted=target;entryKey=key;
        target.innerHTML=`<section class="card acquisition-entry"><div><h2>Irodoriの音声を追加</h2><p>このPCのBackendで、3つの自然な日本語音声を使えます。取得中もアプリ・管理画面の操作を続けられます。</p></div>${catalogError?`<p class="field-error">${esc(catalogError)}</p>${button('catalog','状態を再取得','')}`:assets.map(a=>{
            const j=job(a.id), busy=active.has(j?.phase), ready=j?.phase==='ready';
            return `<div class="acquisition-option"><div><h3>${esc(a.name)} · ${a.platform==='macos-arm64'?'Apple Silicon Mac':'Windows NVIDIA'}</h3><p>モデル ${size(a.download_bytes)} ＋ 音声エンジンの依存ソフト<br>開始時の空き容量目安 ${size(a.minimum_free_bytes)}</p><p class="subtle">${esc(a.license_notice)} <a href="${esc(a.license_url)}" target="_blank" rel="noopener noreferrer">利用条件</a> · <a href="${esc(a.source_url)}" target="_blank" rel="noopener noreferrer">取得元</a></p>${!a.supported?`<p class="field-error">${esc(a.reason)}</p>`:''}${j?`<p class="subtle">${esc(phases[j.phase]||j.phase)}</p>`:''}</div><div class="actions">${ready?button('voices','声の一覧を更新',a.id):busy?button('show','進捗を表示',a.id):a.supported?button('start',j?'準備を再試行':'取得する',a.id,'primary'):button('catalog','対応状況を再確認',a.id)}${j?.installed&&!busy?button('repair','データを再検証・修復',a.id):''}</div></div>`;
        }).join('')}<p class="subtle acquisition-note">標準のVOICEVOXと、現在選択中の声はそのまま使えます。追加した声はアプリのBackend音声一覧からも選べます。</p></section>`;
        if(pending)target.querySelectorAll('button').forEach(b=>b.disabled=true);
    }
    function paintBanner(){
        const j=jobs.find(j=>active.has(j.phase))||[...jobs].sort((a,b)=>b.updated_at-a.updated_at)[0];
        const hide=!j||dismissed===`${j.asset_id}:${j.phase}`;
        if(hide){banner.hidden=true;bannerKey='';document.documentElement.style.setProperty('--acquisition-height','0px');return;}
        banner.hidden=false;
        const key=`${j.asset_id}:${j.phase}:${compact}`;
        if(bannerKey!==key){
            bannerKey=key;
            banner.innerHTML=`<div class="acquisition-heading"><strong id="acquisition-title" role="status" aria-live="polite" aria-atomic="true"></strong>${button('compact',compact?'詳細':'小さくする',j.asset_id)}${compact&&active.has(j.phase)?button('cancel','中止',j.asset_id):''}${!active.has(j.phase)?button('dismiss','閉じる',j.asset_id):''}</div><div class="acquisition-meter"><progress id="acquisition-bar" aria-label="モデルのダウンロード進捗"></progress><span id="acquisition-bytes"></span></div><div ${compact?'hidden':''}><p id="acquisition-detail"></p><div class="actions">${active.has(j.phase)?button('cancel','中止',j.asset_id):j.phase==='ready'?button('voices','声の一覧を更新',j.asset_id,'primary'):button('start','再試行',j.asset_id,'primary')}${button('open','取得画面へ',j.asset_id)}</div></div>`;
        }
        const title=`Irodori · ${phases[j.phase]||j.phase}`;if(banner.querySelector('#acquisition-title').textContent!==title)banner.querySelector('#acquisition-title').textContent=title;
        banner.querySelector('#acquisition-detail').textContent=connected?j.detail:'Backendへの接続が切れました。再接続後に進捗を読み直します。';
        const bar=banner.querySelector('#acquisition-bar'), bytes=banner.querySelector('#acquisition-bytes');
        const downloading=j.phase==='downloading';
        banner.querySelector('.acquisition-meter').hidden=!downloading;
        if(downloading){bar.max=j.total_bytes||1;bar.value=j.downloaded_bytes||0;bytes.textContent=j.total_bytes>0?`${Math.floor(Math.min(1,Math.max(0,(j.downloaded_bytes||0)/j.total_bytes))*100)}% / 100%`:'準備中';}
        banner.querySelectorAll('[data-acquire=cancel]').forEach(b=>b.disabled=j.phase==='cancelling');
    }
    async function load(){
        try{const data=await api('api/assets');assets=data.assets.filter(a=>data.host_platform==='Darwin'?a.platform==='macos-arm64':data.host_platform==='Windows'?a.platform==='windows-nvidia':true);jobs=data.jobs;catalogError=data.blocked||'';connected=true;}
        catch(e){catalogError=e.message;connected=false;}
        paintEntry();paintBanner();
    }
    async function poll(){
        try{const data=await api('api/assets/status');jobs=data.jobs;connected=true;}
        catch{connected=false;}
        paintEntry();paintBanner();timer=setTimeout(poll,document.hidden?5000:1500);
    }
    async function handle(action,id){
        if(action==='catalog'){await load();return;}
        if(action==='compact'){compact=!compact;paintBanner();return;}
        if(action==='dismiss'){dismissed=`${id}:${job(id)?.phase}`;paintBanner();return;}
        if(action==='show'){dismissed='';compact=true;paintBanner();return;}
        if(action==='open'){location.hash='providers/tts';return;}
        if(action==='voices'){await reloadVoices();return;}
        if(pending)return;
        if(action==='start'||action==='repair'){
            const a=assets.find(a=>a.id===id);if(!a)return;
            if(!await confirmAction(action==='repair'?'追加音声を修復しますか':'Irodoriの音声を取得しますか',`モデル ${size(a.download_bytes)} と依存ソフトを取得・導入します。空き容量の目安は ${size(a.minimum_free_bytes)} です。\n${a.license_notice}\n現在選択中の声は変わりません。取得中も操作できます。`,'取得・準備する'))return;
            pending=true;paintEntry();
            try{const j=await api(`api/assets/${encodeURIComponent(id)}/start`,{method:'POST',body:{repair:action==='repair'}});jobs=jobs.filter(x=>x.asset_id!==id).concat(j);dismissed='';compact=true;paintBanner();}
            finally{pending=false;paintEntry();}
        }else if(action==='cancel'){
            if(!await confirmAction('取得処理を中止しますか','取得・音声の準備を止めます。取得済みファイルは再試行で使えます。現在の声はそのまま利用できます。','中止する'))return;
            const j=await api(`api/assets/${encodeURIComponent(id)}/cancel`,{method:'POST'});jobs=jobs.filter(x=>x.asset_id!==id).concat(j);paintBanner();paintEntry();
        }
    }
    document.addEventListener('click',e=>{const b=e.target.closest('[data-acquire]');if(b)handle(b.dataset.acquire,b.dataset.asset).catch(e=>notice(e.message));});
    return {mount:paintEntry,async start(){await load();timer=setTimeout(poll,1500);},stop(){clearTimeout(timer);observer.disconnect();}};
}
