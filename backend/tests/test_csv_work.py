import csv
import io
import pytest
from fastapi.testclient import TestClient
from app.main import app
from app.core.config import get_settings

@pytest.fixture
def client(tmp_path,monkeypatch):
    monkeypatch.setenv('DATABASE_URL','sqlite:///'+str(tmp_path/'work.db'))
    monkeypatch.setenv('YUI_BACKEND_SETTINGS_PATH',str(tmp_path/'settings.json'));get_settings.cache_clear()
    with TestClient(app,base_url='http://localhost',client=('127.0.0.1',123)) as c:
        c.post('/admin/session',headers={'X-Yui-Admin':'1'});c.headers['X-Yui-Admin']='1';yield c
    get_settings.cache_clear()

def body(**kwargs):
    return {'request_id':'sales','filename':'sales.csv','csv_text':'商品,金額\n紅茶,12.50\nコーヒー,7\n紅茶,-2\n','group_column':'商品','value_column':'金額',**kwargs}

def test_csv_roundtrip_has_durable_identity_exact_sum_and_three_outputs(client):
    first=client.post('/admin/api/work/csv',json=body());assert first.status_code==200
    result=first.json();assert result['state']=='completed' and result['rows']==3 and result['groups']==2
    assert set(result['artifacts'])=={'summary.csv','chart.svg','report.html'}
    csv_result=client.get('/admin/api/work/sales/artifacts/summary.csv')
    rows=list(csv.DictReader(io.StringIO(csv_result.text)))
    assert next(x for x in rows if x['商品']=='紅茶')['sum']=='10.50'
    assert client.post('/admin/api/work/csv',json=body()).json()==result
    assert len(client.get('/admin/api/work').json()['items'])==1
    assert client.post('/admin/api/work/csv',json=body(csv_text='商品,金額\n紅茶,100')).status_code==409
    for artifact in result['artifacts']:
        r=client.get('/admin/api/work/sales/artifacts/'+artifact)
        assert r.status_code==200 and 'attachment;' in r.headers['content-disposition']
        assert 'sandbox' in r.headers['content-security-policy']

@pytest.mark.parametrize('text',['商品,金額\n紅茶,NaN','商品,金額\n紅茶,','商品,金額\n紅茶,1,2','商品,商品\n紅茶,1'])
def test_bad_rows_fail_without_partial_output(client,text):
    result=client.post('/admin/api/work/csv',json=body(csv_text=text)).json()
    assert result['state']=='failed' and result['artifacts']==[]
    assert client.get('/admin/api/work/sales/artifacts/summary.csv').status_code==404

def test_csv_formula_and_html_labels_are_inert(client):
    result=client.post('/admin/api/work/csv',json=body(csv_text='商品,金額\n=1+1,2\n<script>alert(1)</script>,3')).json()
    assert result['state']=='completed'
    csv_result=client.get('/admin/api/work/sales/artifacts/summary.csv').text
    assert "'=1+1" in csv_result
    html=client.get('/admin/api/work/sales/artifacts/report.html').text
    assert '<script>' not in html and '&lt;script&gt;' in html
    with TestClient(app,base_url='http://localhost',client=('100.100.0.1',123)) as remote:
        assert remote.get('/admin/api/work').status_code==403
        assert remote.post('/admin/api/work/csv',json=body()).status_code==403
