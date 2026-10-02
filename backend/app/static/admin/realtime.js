// Browser PCM transport uses the existing Unity WebSocket protocol.
let socket, context, stream, source, processor, silence, nextPlay=0;
export async function stopLive(){
    if(socket){if(socket.readyState===1)socket.send(JSON.stringify({type:'close'}));socket.close();socket=null;}
    processor?.disconnect();source?.disconnect();silence?.disconnect();stream?.getTracks().forEach(t=>t.stop());
    if(context&&context.state!=='closed')await context.close();
    stream=processor=source=context=silence=null;nextPlay=0;
}
export async function startLive(mode,instructions,onText,onStatus){
    await stopLive();let transcript='';
    try{
        stream=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true},video:false});
        context=new AudioContext({sampleRate:24000});await context.resume();
        source=context.createMediaStreamSource(stream);
        // ScriptProcessor is broadly available in the supported desktop WebViews;
        // resample explicitly if the device ignores the requested sample rate.
        processor=context.createScriptProcessor(2048,1,1);silence=context.createGain();silence.gain.value=0;
        const ws=new WebSocket(`${location.protocol==='https:'?'wss':'ws'}://${location.host}/admin/api/run/realtime/stream`);socket=ws;
        onStatus('OpenAIに接続しています…');
        let ready=false;
        ws.onopen=()=>ws.send(JSON.stringify({type:'start',mode,instructions,user_id:'console-preview',character_id:'console-preview',secret:true}));
        processor.onaudioprocess=e=>{if(!ready||ws.readyState!==1||ws.bufferedAmount>1000000)return;const input=e.inputBuffer.getChannelData(0),ratio=context.sampleRate/24000,count=Math.floor(input.length/ratio),bytes=new Uint8Array(count*2),view=new DataView(bytes.buffer);for(let i=0;i<count;i++){const f=Math.max(-1,Math.min(1,input[Math.min(input.length-1,Math.floor(i*ratio))]));view.setInt16(i*2,f<0?f*32768:f*32767,true);}let binary='';for(const b of bytes)binary+=String.fromCharCode(b);ws.send(JSON.stringify({type:'audio',audio:btoa(binary)}));};
        source.connect(processor);processor.connect(silence);silence.connect(context.destination);
        ws.onmessage=e=>{const event=JSON.parse(e.data);if(event.type==='ready'){ready=true;onStatus('接続中 · マイク音声をOpenAIへ送信しています。終了ボタンで切断できます。');}
            if(event.type==='text_delta'){transcript+=event.delta||'';onText(transcript);}
            if(event.type==='event'&&event.event==='conversation.item.input_audio_transcription.completed'){transcript+='\nあなた: '+(event.transcript||'')+'\n';onText(transcript);}
            if(event.type==='audio_delta'&&context){const raw=atob(event.audio||''),buffer=context.createBuffer(1,Math.floor(raw.length/2),24000),out=buffer.getChannelData(0);for(let i=0;i<out.length;i++){let n=raw.charCodeAt(i*2)|(raw.charCodeAt(i*2+1)<<8);if(n>32767)n-=65536;out[i]=n/32768;}const audio=context.createBufferSource();audio.buffer=buffer;audio.connect(context.destination);nextPlay=Math.max(context.currentTime+.03,nextPlay);audio.start(nextPlay);nextPlay+=buffer.duration;}
            if(event.type==='error'){onStatus(event.message||'音声接続でエラーが発生しました。');stopLive();}}
        ws.onerror=()=>{onStatus('音声接続に失敗しました。キーと接続先を確認してください。');stopLive();};
        ws.onclose=()=>{ready=false;onStatus('ライブ接続が終了しました。');if(socket===ws)stopLive();};
    }catch(e){await stopLive();throw e;}
}
