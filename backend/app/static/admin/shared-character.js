export async function mountSharedCharacter(root, api, notice, confirmAction) {
    const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    let closed=false, generation=0, character='', snapshot, draft, sessions=new Map();
    const base=()=>`api/sync/characters/${encodeURIComponent(character)}`;
    root.innerHTML=`<section class="card"><h2>端末と共有する会話・記憶</h2><p>アプリで共有したキャラクターを選ぶと、このPCで話した内容も次の同期で端末へ届きます。</p><label for="shared-character-choice">キャラクター</label><select id="shared-character-choice"><option value="">選んでください</option></select><div id="shared-character-body"></div></section>`;
    const list=await api('api/sync/characters');
    if(closed||!root.isConnected)return()=>{closed=true;};
    const choice=root.querySelector('#shared-character-choice');
    choice.insertAdjacentHTML('beforeend',list.items.map(c=>`<option value="${esc(c.id)}">${esc(c.name)} · ${esc(c.id.slice(0,6))}</option>`).join(''));
    if(!list.items.length)root.querySelector('#shared-character-body').textContent='アプリからキャラクターを共有すると、ここに表示されます。';
    async function refresh() {
        const ticket=++generation, selected=character;
        const next=await api(base());
        if(closed||!root.isConnected||ticket!==generation||selected!==character)return;
        snapshot=next;draft=null;
        const body=root.querySelector('#shared-character-body');
        const memories=next.items.filter(x=>x.kind==='memory'&&!x.deleted);
        const history=next.items.filter(x=>x.kind==='history'&&!x.deleted).sort((a,b)=>a.value.recorded_utc.localeCompare(b.value.recorded_utc)||((a.value.speaker==='You'?0:1)-(b.value.speaker==='You'?0:1))||a.id.localeCompare(b.id));
        body.innerHTML=`<form id="shared-chat-form"><h3>このキャラクターに話す</h3><label for="shared-message">メッセージ</label><textarea id="shared-message" required maxlength="16000"></textarea><label class="check"><input name="secret" type="checkbox" checked>今回の会話を保存しない</label><p class="subtle">保存する場合はチェックを外してください。共有済みの人格・記憶を使い、会話と個人的な発言を共有記録へ保存します。送信先は設定した会話の提供元です。外部APIでは利用料金が発生する場合があります。</p><button class="primary">送信する</button><p id="shared-chat-result" class="result" aria-live="polite"></p></form><hr><details id="legacy-import"><summary>既存のBackend記録を取り込む</summary><p>選んだ記録だけをコピーします。同じ名前だけで結びつけません。</p><label for="legacy-identity">取り込む利用者・キャラクター</label><select id="legacy-identity"><option value="">選んでください</option></select><button id="legacy-preview" class="quiet">取り込む内容を確認</button><div id="legacy-draft"></div></details><hr><h3>共有した記憶</h3><p>関連する元の発言を2〜8件選び、つながりを確認して記憶を作れます。元の発言は保持します。</p><div id="shared-memories">${memories.length?memories.map(x=>`<article class="row"><div>${x.value.source_ids?'<span class="pill">確認した関連づけ</span>':`<label class="check"><input type="checkbox" data-memory-id="${esc(x.id)}">関連づける元の発言</label>`}<p class="memory-content">${esc(x.value.content)}</p><small>${esc(x.value.recorded_utc||'記録日時不明')}</small>${x.value.source_ids?`<small>元の発言: ${x.value.source_ids.map(esc).join(', ')}</small>`:''}</div></article>`).join(''):'<p>共有した記憶はありません。</p>'}</div><button id="shared-connection-preview" class="quiet">選んだ記憶のつながりを確認</button><div id="shared-connection-editor"></div><details><summary>共有した会話履歴 · ${history.length}件</summary>${history.slice(-40).map(x=>`<article class="row"><div><strong>${esc(x.value.speaker)}</strong><small>${esc(x.value.recorded_utc)}</small><p class="memory-content">${esc(x.value.text)}</p></div></article>`).join('')}${history.length>40?'<p>直近40件を表示しています。端末には同期対象の全履歴が残ります。</p>':''}</details>`;
        const identities=(await api('api/identities')).items;
        if(closed||ticket!==generation||selected!==character||!body.isConnected)return;
        const scopes=[...new Map(identities.map(x=>[JSON.stringify([x.user_id,x.character_id]),{user_id:x.user_id,character_id:x.character_id}])).values()];
        body.querySelector('#legacy-identity').insertAdjacentHTML('beforeend',scopes.map((x,i)=>`<option value="${i}">${esc(x.user_id)} / ${esc(x.character_id||'旧キャラクター（未指定）')}</option>`).join(''));
        body.querySelector('#legacy-preview').onclick=async event=>{
            const index=body.querySelector('#legacy-identity').value;
            if(index===''){notice('取り込む記録の範囲を選んでください。');return;}
            const scope=scopes[Number(index)], url=`api/sync/characters/${encodeURIComponent(selected)}`;
            event.target.disabled=true;
            try{
                const preview=await api(url+'/import-preview',{method:'POST',body:scope});
                if(closed||selected!==character||!body.isConnected)return;
                const editor=body.querySelector('#legacy-draft');
                editor.innerHTML=`<p>${esc(preview.notice)}</p><p>取り込み ${preview.items.length}件 · 取り込み済み ${preview.already_shared}件 · サイズ・形式で除外 ${preview.skipped}件</p><div>${preview.items.map(x=>`<p class="memory-content">${esc(x.value.content||x.value.text)}</p>`).join('')}</div><button class="primary" ${preview.items.length?'':'disabled'}>この範囲を共有記録へ取り込む</button>`;
                editor.querySelector('button').onclick=async e=>{
                    if(!await confirmAction('このBackend記録を共有しますか',`${scope.user_id} / ${scope.character_id||'旧キャラクター'} の ${preview.items.length} 件を、この共有キャラクターへコピーします。次の同期で端末へ届きます。`,'取り込む'))return;
                    if(closed||selected!==character)return;
                    e.target.disabled=true;
                    try{await api(url+'/import',{method:'POST',body:{...scope,preview_hash:preview.preview_hash}});notice('記録を取り込みました。アプリで同期すると反映されます。');await refresh();}catch(error){notice(error.message);}finally{e.target.disabled=false;}
                };
            }catch(error){notice(error.message);}finally{event.target.disabled=false;}
        };
        body.querySelector('#shared-chat-form').onsubmit=async event=>{
            event.preventDefault();const form=event.target, button=form.querySelector('button'), message=form.querySelector('textarea').value, secret=form.elements.secret.checked;
            if(!sessions.has(selected))sessions.set(selected,crypto.randomUUID());
            button.disabled=true;
            try {
                const result=await api(`api/sync/characters/${encodeURIComponent(selected)}/chat`,{method:'POST',body:{request_id:crypto.randomUUID(),session_id:sessions.get(selected),message,secret}});
                if(closed||selected!==character||!form.isConnected)return;
                form.querySelector('#shared-chat-result').textContent=result.text;
                if(!secret){notice('会話を共有記録に保存しました。アプリで同期すると反映されます。');const reply=result.text;await refresh();root.querySelector('#shared-chat-result').textContent=reply;}
            }catch(e){notice(e.message);}finally{button.disabled=false;}
        };
        body.querySelector('#shared-connection-preview').onclick=async event=>{
            const ids=[...body.querySelectorAll('[data-memory-id]:checked')].map(x=>x.dataset.memoryId);
            if(ids.length<2||ids.length>8){notice('元の発言を2〜8件選んでください。');return;}
            event.target.disabled=true;
            try {
                const nextDraft=await api(base()+'/connection-preview',{method:'POST',body:{source_ids:ids}});
                if(closed||selected!==character||!body.isConnected)return;
                draft=nextDraft;
                const editor=body.querySelector('#shared-connection-editor');
                editor.innerHTML=`<form><h3>関連づける記憶を確認</h3><p>${esc(draft.notice)}</p><label for="shared-connection-text">確認した内容</label><textarea id="shared-connection-text" required maxlength="4000">${esc(draft.content)}</textarea><button class="primary">この内容で記憶を保存</button></form>`;
                editor.querySelector('form').onsubmit=async e=>{
                    e.preventDefault();const content=e.target.querySelector('textarea').value, saving=draft;
                    if(!await confirmAction('関連づけた記憶を保存しますか',content,'保存する'))return;
                    if(closed||selected!==character)return;
                    const submit=e.target.querySelector('button');submit.disabled=true;
                    try{await api(base()+'/connections',{method:'POST',body:{source_ids:saving.source_ids,source_versions:saving.source_versions,content}});notice('元の発言と関連づけて保存しました。端末にも同期できます。');await refresh();}catch(error){notice(error.message);}finally{submit.disabled=false;}
                };
            }catch(e){notice(e.message);}finally{event.target.disabled=false;}
        };
    }
    choice.onchange=()=>{character=choice.value;generation++;if(character)refresh().catch(e=>notice(e.message));else root.querySelector('#shared-character-body').innerHTML='';};
    return()=>{closed=true;generation++;};
}
