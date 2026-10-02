"""Bounded local CSV work, with durable request identity and downloadable outputs."""
from __future__ import annotations
import csv
from decimal import Decimal, InvalidOperation
from html import escape
import io
import json
from uuid import uuid4

from fastapi import HTTPException
from pydantic import BaseModel, ConfigDict, Field
from app.core.device_sync import SyncStore, encoded, digest


class CsvWork(BaseModel):
    model_config = ConfigDict(extra="forbid")
    request_id: str = Field(default_factory=lambda: uuid4().hex, pattern=r"^[a-zA-Z0-9_-]{1,80}$")
    filename: str = Field(min_length=1, max_length=256)
    csv_text: str = Field(min_length=1, max_length=2*1024*1024)
    group_column: str = Field(min_length=1, max_length=256)
    value_column: str = Field(min_length=1, max_length=256)


class WorkStore:
    def __init__(self, database_url):
        self.db = SyncStore(database_url)
        with self.db.connect() as db:
            db.execute('''CREATE TABLE IF NOT EXISTS csv_work (
                id TEXT PRIMARY KEY, signature TEXT NOT NULL, filename TEXT NOT NULL,
                state TEXT NOT NULL, detail TEXT NOT NULL, result_json TEXT NOT NULL DEFAULT '{}',
                created_at TEXT NOT NULL DEFAULT (datetime('now')))''')

    def get(self, id):
        with self.db.connect() as db:
            row=db.execute('SELECT * FROM csv_work WHERE id=?',(id,)).fetchone()
            if not row: raise HTTPException(404,'作業が見つかりません。')
            result=json.loads(row['result_json'])
            return {'request_id':row['id'],'filename':row['filename'],'state':row['state'],'detail':row['detail'],
                    'created_at':row['created_at'],'rows':result.get('rows'), 'groups':result.get('groups'),
                    'artifacts':list(result.get('artifacts',{}))}

    def list(self):
        with self.db.connect() as db:
            ids=[r['id'] for r in db.execute('SELECT id FROM csv_work ORDER BY created_at DESC,id DESC LIMIT 30')]
        return [self.get(id) for id in ids]

    def run(self, body: CsvWork):
        if len(body.csv_text.encode('utf-8'))>2*1024*1024: raise HTTPException(413,'CSVはUTF-8で2MiBまでです。')
        signature=digest(encoded(body.model_dump()))
        with self.db.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            prior=db.execute('SELECT signature FROM csv_work WHERE id=?',(body.request_id,)).fetchone()
            if prior:
                if prior['signature']!=signature: raise HTTPException(409,'同じ依頼IDでは内容を変更できません。新しい作業として送ってください。')
            else:
                db.execute('INSERT INTO csv_work(id,signature,filename,state,detail) VALUES(?,?,?,?,?)',
                           (body.request_id,signature,body.filename,'running','このPCのCSV集計処理が受理しました。'))
        if prior: return self.get(body.request_id)
        try:
            result=aggregate(body)
        except (ValueError,csv.Error,InvalidOperation) as exc:
            state,detail,result='failed',str(exc),{}
        except Exception:
            state,detail,result='failed','集計を完了できませんでした。元のファイルは変更していません。',{}
        else:
            state,detail='completed',f"{result['rows']}行を{result['groups']}項目に集計しました。元のファイルは変更していません。"
        with self.db.connect() as db:
            db.execute('UPDATE csv_work SET state=?,detail=?,result_json=? WHERE id=?', (state,detail,encoded(result),body.request_id))
        return self.get(body.request_id)

    def artifact(self,id,name):
        with self.db.connect() as db:
            row=db.execute('SELECT result_json FROM csv_work WHERE id=? AND state=?',(id,'completed')).fetchone()
        result=json.loads(row['result_json']) if row else {}
        if name not in result.get('artifacts',{}): raise HTTPException(404,'成果物が見つかりません。')
        return result['artifacts'][name]


def aggregate(body):
    reader=csv.DictReader(io.StringIO(body.csv_text.lstrip('\ufeff'),newline=''))
    fields=reader.fieldnames or []
    if len(fields)>100 or len(set(fields))!=len(fields) or any(not x or len(x)>256 for x in fields):
        raise ValueError('見出しは重複しない100列までにしてください。')
    if body.group_column not in fields or body.value_column not in fields:
        raise ValueError('指定した列がCSVの見出しにありません。')
    sums,counts={},{}; rows=0
    for line in reader:
        rows+=1
        if rows>20000: raise ValueError('CSVは20,000行までです。')
        if None in line or any(x is None for x in line.values()): raise ValueError(f'{rows+1}行目の列数が見出しと一致しません。')
        key=line[body.group_column]
        if len(key)>256: raise ValueError(f'{rows+1}行目の分類名が長すぎます。')
        raw=line[body.value_column].strip()
        try: value=Decimal(raw)
        except InvalidOperation: raise ValueError(f'{rows+1}行目の集計列を数値として読めません。空欄・通貨記号・桁区切りも確認してください。')
        if not value.is_finite() or abs(value)>Decimal('1e18') or len(raw)>64:
            raise ValueError(f'{rows+1}行目の数値が許容範囲を超えています。')
        sums[key]=sums.get(key,Decimal(0))+value; counts[key]=counts.get(key,0)+1
        if len(sums)>100: raise ValueError('分類は100項目までです。')
    if not rows: raise ValueError('CSVに集計できる行がありません。')
    data=sorted(sums.items())
    output=io.StringIO(newline=''); writer=csv.writer(output);writer.writerow(["'"+body.group_column if body.group_column.lstrip().startswith(('=','+','-','@')) else body.group_column,'count','sum'])
    # Prefix formula-like labels so opening the generated CSV cannot run formulas.
    safe=lambda text: "'"+text if text.lstrip().startswith(('=','+','-','@')) or text.startswith(('\t','\r')) else text
    for key,value in data: writer.writerow([safe(key),counts[key],str(value)])
    width=800;height=80+36*len(data);maximum=max(abs(v) for v in sums.values()) or Decimal(1)
    svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img"><title>{escape(body.value_column)}の分類別合計</title><rect width="100%" height="100%" fill="white"/><text x="20" y="28" font-size="18">{escape(body.value_column)}の分類別合計（左: 負、右: 正）</text>']
    for i,(key,value) in enumerate(data):
        y=50+36*i;length=float(abs(value)/maximum)*190;x=480-length if value<0 else 480
        svg.append(f'<text x="20" y="{y+18}" font-size="14">{escape(key[:30])}</text><rect x="{x:.2f}" y="{y}" width="{length:.2f}" height="24" fill="{"#b94a48" if value<0 else "#457b9d"}"/><text x="690" y="{y+18}" font-size="12">{escape(str(value))}</text>')
    svg.append(f'<line x1="480" x2="480" y1="45" y2="{height-20}" stroke="#999"/></svg>')
    chart=''.join(svg)
    table=''.join(f'<tr><td>{escape(k)}</td><td>{counts[k]}</td><td>{escape(str(v))}</td></tr>' for k,v in data)
    html=f'<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>CSV集計</title><h1>{escape(body.filename)}の集計</h1><p>{rows}行。分類: {escape(body.group_column)}、合計: {escape(body.value_column)}。欠損や数値でない行は除外せずエラーにします。</p>{chart}<table><thead><tr><th>分類</th><th>件数</th><th>合計</th></tr></thead><tbody>{table}</tbody></table></html>'
    return {'rows':rows,'groups':len(data),'artifacts':{'summary.csv':output.getvalue(),'chart.svg':chart,'report.html':html}}
