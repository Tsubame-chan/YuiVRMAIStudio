import {mountSharedCharacter} from './shared-character.js';

export function mountDeviceSync(root, api, notice, confirmAction) {
    const esc = v => String(v ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    let closed = false, expiryTimer, unmountShared;
    root.innerHTML = `<div class="workspace"><section class="card"><h2>アプリの端末を登録</h2><p>アプリの接続設定で「キャラクターと会話を同期」を開き、このPCで発行したコードを入力します。</p><p>同期するキャラクターと件数はアプリ側で確認してから反映します。異なる端末の履歴は両方を残し、同じ項目の変更が重なった場合は内容を選びます。</p><button id="sync-code-create" class="primary">登録コードを表示</button><div id="sync-code" aria-live="polite"></div><p class="subtle">コードは10分間・1回だけ有効です。APIキー、アバター、音声モデル、音声の接続設定は同期しません。旧アプリには同期の操作がありません。</p></section><section class="card"><h2>登録済みの端末</h2><div id="sync-devices" aria-live="polite">確認中…</div><p class="subtle">登録解除すると、このPCへの同期ができなくなります。端末に保存済みのデータは残ります。</p></section></div>`;
    const create = root.querySelector('#sync-code-create');
    root.insertAdjacentHTML('beforeend','<div id="shared-character"></div>');
    mountSharedCharacter(root.querySelector('#shared-character'),api,notice,confirmAction).then(unmount=>{if(closed)unmount?.();else unmountShared=unmount;}).catch(e=>notice(e.message));
    create.onclick = async()=> {
        create.disabled = true;
        try {
            const data = await api('api/sync/pairing',{method:'POST'});
            if(closed || !root.isConnected)return;
            const target=root.querySelector('#sync-code');
            target.innerHTML=`<div class="connection-box"><code>${esc(data.code)}</code><span>1回有効 · 10分以内に入力</span></div>`;
            clearTimeout(expiryTimer);
            expiryTimer=setTimeout(()=>{if(!closed&&root.isConnected&&target.isConnected)target.textContent='コードの有効期限が切れました。必要な場合は再発行してください。';},data.expires_in*1000);
        } catch(e) {notice(e.message);} finally {create.disabled=false;}
    };
    async function refresh() {
        const data=await api('api/sync/devices');
        if(closed || !root.isConnected)return;
        const target=root.querySelector('#sync-devices');
        target.innerHTML=data.items.length?data.items.map(d=>`<article class="row"><div><h3>${esc(d.name)}</h3><small>最終接続 ${esc(new Date(d.last_seen*1000).toLocaleString())}</small></div>${d.revoked?'<span class="pill">登録解除済み</span>':`<button class="danger" data-revoke="${esc(d.id)}">登録解除</button>`}</article>`).join(''):'<p>登録済みの端末はありません。</p>';
        target.querySelectorAll('[data-revoke]').forEach(b=>b.onclick=async()=> {
            if(!await confirmAction('この端末の登録を解除しますか','このPCへの同期を停止します。端末に保存済みの履歴は消えません。','登録を解除'))return;
            b.disabled=true;
            try {await api('api/sync/devices/'+encodeURIComponent(b.dataset.revoke)+'/revoke',{method:'POST'});await refresh();}
            catch(e){notice(e.message);b.disabled=false;}
        });
    }
    refresh().catch(e=>notice(e.message));
    return ()=>{closed=true;clearTimeout(expiryTimer);unmountShared?.();};
}
