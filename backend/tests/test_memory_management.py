import sqlite3
import pytest
from app.db.sqlite import initialize_database
from app.db.repositories import MemoryRepository
from app.models.memory import MemorySaveRequest, MemorySearchRequest, MemoryScope
from app.api.routes import memory_update, memory_delete
from fastapi import HTTPException


def test_memory_scope_edit_delete_and_legacy_migration(tmp_path):
    path = tmp_path / 'memories.db'
    with sqlite3.connect(path) as db:
        db.execute("CREATE TABLE memories (id INTEGER PRIMARY KEY, user_id TEXT, content TEXT, importance INTEGER, tags_json TEXT, created_at TEXT, updated_at TEXT)")
        db.execute("INSERT INTO memories VALUES (1,'alice','legacy',3,'[]','old','old')")
    url = 'sqlite:///' + str(path)
    initialize_database(url)
    initialize_database(url)
    repo = MemoryRepository(url)
    assert repo.list_recent('alice')[0].content == 'legacy'
    assert repo.list_recent('alice', character_id='avatar-a') == []
    item = repo.save(MemorySaveRequest(user_id='alice', character_id='avatar-a', content='tea'))
    for user, avatar in [('bob', 'avatar-a'), ('alice', 'avatar-b'), ('alice', None)]:
        assert repo.search(MemorySearchRequest(user_id=user, character_id=avatar, query='tea')) == []
        with pytest.raises(HTTPException) as error:
            memory_update(int(item.id), MemorySaveRequest(user_id=user, character_id=avatar, content='overwrite'), repo)
        assert error.value.status_code == 404
        with pytest.raises(HTTPException):
            memory_delete(int(item.id), MemoryScope(user_id=user, character_id=avatar), repo)
    updated = memory_update(int(item.id), MemorySaveRequest(user_id='alice', character_id='avatar-a', content='coffee', importance=5, tags=['drink']), repo)
    assert updated.content == 'coffee'
    assert repo.list_recent('alice', character_id='avatar-a')[0].tags == ['drink']
    assert memory_delete(int(item.id), MemoryScope(user_id='alice', character_id='avatar-a'), repo) == {'deleted': True}
    assert repo.list_recent('alice', character_id='avatar-a') == []
    assert repo.list_recent('alice')[0].content == 'legacy'


def test_memory_pages_do_not_omit_or_repeat_rows(tmp_path):
    url = 'sqlite:///' + str(tmp_path / 'pages.db')
    initialize_database(url)
    repo = MemoryRepository(url)
    for i in range(25):
        repo.save(MemorySaveRequest(user_id='u', character_id='c', content=f'item-{i}'))
    first = repo.search(MemorySearchRequest(user_id='u', character_id='c', query='', limit=20))
    second = repo.search(MemorySearchRequest(user_id='u', character_id='c', query='', limit=20, offset=20))
    assert len(first) == 20 and len(second) == 5
    assert len({item.id for item in first + second}) == 25
