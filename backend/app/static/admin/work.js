export async function mountWork(root,api,notice) {
    const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    let closed=false;
    root.innerHTML=`<h1>CSVから集計と図を作る</h1><p>選んだCSVをこのPCのBackendで処理し、分類別の件数・合計と棒グラフを作ります。外部AIへの送信やAPI料金はありません。元ファイルは変更しません。</p><form class="card" id="csv-work"><label for="csv-file">UTF-8のCSV（2MiB・20,000行・分類100項目まで）</label><input id="csv-file" type="file" accept=".csv,text/csv"><details><summary>CSVを貼り付ける</summary><label for="csv-paste">見出しを含むCSV本文（ファイルを選んだ場合はファイルを優先）</label><textarea id="csv-paste" maxlength="2097152"></textarea></details><label for="csv-group">分類する列の見出し</label><input id="csv-group" required maxlength="256"><label for="csv-value">合計する数値列の見出し</label><input id="csv-value" required maxlength="256"><p>空欄や数値でない行は、除外せずエラーとして知らせます。</p><button class="primary">このCSVを集計する</button><p id="csv-result" aria-live="polite"></p></form><section class="card"><h2>作業と成果物</h2><button id="work-refresh" class="quiet">状態を更新</button><div id="work-list"></div></section>`;
    async function refresh(){
        const jobs=(await api('api/work')).items;
        if(closed||!root.isConnected)return;
        root.querySelector('#work-list').innerHTML=jobs.length?jobs.map(x=>`<article class="row"><div><strong>${esc(x.filename)}</strong><p>${esc(x.detail)}</p><small>${esc(x.request_id)} · ${esc({running:'PCが処理中',completed:'完了',failed:'失敗'}[x.state]||x.state)}</small><div class="actions">${x.artifacts.map(name=>`<button class="quiet" data-job="${esc(x.request_id)}" data-artifact="${esc(name)}">${esc({'summary.csv':'集計CSV','chart.svg':'棒グラフ','report.html':'レポート'}[name])}を保存</button>`).join('')}</div></div></article>`).join(''):'<p>まだ作業はありません。</p>';
        root.querySelectorAll('[data-artifact]').forEach(button=>button.onclick=async()=>{
            button.disabled=true;
            try{const blob=await api(`api/work/${encodeURIComponent(button.dataset.job)}/artifacts/${button.dataset.artifact}`,{blob:true});const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=button.dataset.artifact;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}catch(e){notice(e.message);}finally{button.disabled=false;}
        });
    }
    root.querySelector('#work-refresh').onclick=()=>refresh().catch(e=>notice(e.message));
    root.querySelector('#csv-work').onsubmit=async event=>{
        event.preventDefault();const form=event.target,file=root.querySelector('#csv-file').files[0],button=form.querySelector('button');
        if(!file&&!root.querySelector('#csv-paste').value.trim()){notice('CSVファイルを選ぶか、本文を貼り付けてください。');return;}if(file&&file.size>2*1024*1024){notice('CSVは2MiBまでです。');return;}
        button.disabled=true;
        try{
            const csv_text=file?new TextDecoder('utf-8',{fatal:true}).decode(await file.arrayBuffer()):root.querySelector('#csv-paste').value;
            if(closed||!form.isConnected)return;
            const result=await api('api/work/csv',{method:'POST',body:{request_id:crypto.randomUUID(),filename:file?.name||'貼り付けたCSV',csv_text,group_column:root.querySelector('#csv-group').value,value_column:root.querySelector('#csv-value').value}});
            if(closed||!form.isConnected)return;
            root.querySelector('#csv-result').textContent=result.detail;await refresh();
        }catch(e){notice(e instanceof TypeError?'UTF-8のCSVを選んでください。':e.message);}finally{button.disabled=false;}
    };
    await refresh();return()=>{closed=true;};
}
