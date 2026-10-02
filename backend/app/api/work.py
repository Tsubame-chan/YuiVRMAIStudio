from fastapi import APIRouter, Depends
from fastapi.responses import Response
from app.api.admin import require_admin
from app.core.config import get_settings
from app.core.csv_work import WorkStore, CsvWork

router=APIRouter(prefix='/admin/api/work',dependencies=[Depends(require_admin)])
def store(): return WorkStore(get_settings().database_url)

@router.get('')
def listing(db=Depends(store)): return {'items':db.list()}

@router.post('/csv')
def run(body: CsvWork, db=Depends(store)): return db.run(body)

@router.get('/{id}')
def status(id: str, db=Depends(store)): return db.get(id)

@router.get('/{id}/artifacts/{name}')
def artifact(id: str,name: str, db=Depends(store)):
    content=db.artifact(id,name)
    mime={'summary.csv':'text/csv','chart.svg':'image/svg+xml','report.html':'text/html'}[name]
    return Response(content,media_type=mime,headers={'Content-Disposition':f'attachment; filename="{name}"',
        'Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; sandbox"})
