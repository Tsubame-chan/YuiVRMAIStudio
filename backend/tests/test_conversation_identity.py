import sqlite3

from app.db.sqlite import initialize_database
from app.db.repositories import ChatRepository
from app.models.chat import ChatResponse


def test_migration_preserves_legacy_rows_and_is_repeatable(tmp_path):
    path = tmp_path / 'legacy.db'
    with sqlite3.connect(path) as db:
        db.execute('CREATE TABLE conversations (id INTEGER PRIMARY KEY, request_id TEXT UNIQUE, user_id TEXT, role TEXT, message TEXT, provider TEXT, model TEXT, face TEXT, animation TEXT, metadata_json TEXT, created_at TEXT)')
        db.execute("INSERT INTO conversations(id,user_id,role,message) VALUES (1,'local_user','user','old text')")
    url = 'sqlite:///' + str(path)
    initialize_database(url)
    initialize_database(url)
    repo = ChatRepository(url)
    assert repo.list_recent_messages('local_user') == [{'role': 'user', 'content': 'old text'}]
    assert repo.list_recent_messages('local_user', character_id='avatar-a', session_id='session-a') == []


def test_identity_isolation_and_duplicate_retry(tmp_path):
    url = 'sqlite:///' + str(tmp_path / 'new.db')
    initialize_database(url)
    repo = ChatRepository(url)
    args = dict(request_id='request-a', user_id='alice', user_message='hello', response=ChatResponse(text='answer'), provider='test', model='test', character_id='avatar-a', session_id='session-a', task_id='task-a')
    from concurrent.futures import ThreadPoolExecutor
    with ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(lambda _: repo.save_chat_turn(**args), range(8)))
    assert len(repo.list_recent_messages('alice', character_id='avatar-a', session_id='session-a')) == 2
    assert repo.list_recent_messages('bob', character_id='avatar-a', session_id='session-a') == []
    assert repo.list_recent_messages('alice', character_id='avatar-b', session_id='session-a') == []
    assert repo.get_cached_response('request-a', 'bob', 'avatar-a', 'session-a', 'task-a') is None
    assert repo.get_cached_response('request-a', 'alice', 'avatar-a', 'session-b', 'task-a') is None
    assert repo.get_cached_response('request-a', 'alice', 'avatar-a', 'session-a', 'task-a').text == 'answer'


def test_recent_history_restoration_is_scoped(tmp_path):
    url = 'sqlite:///' + str(tmp_path / 'recent.db')
    initialize_database(url)
    repo = ChatRepository(url)
    for i, (user, character, session) in enumerate([('u', 'a', 's'), ('u', 'b', 's'), ('u', 'a', 'other'), ('other', 'a', 's')]):
        repo.save_chat_turn(request_id=str(i), user_id=user, character_id=character, session_id=session,
                            user_message=f'question-{i}', response=ChatResponse(text=f'answer-{i}'), provider='test', model='test')
    rows = repo.list_recent_conversations('u', character_id='a', session_id='s')
    assert [r.message for r in rows] == ['question-0', 'answer-0']
    assert repo.list_recent_conversations('u') == []


def test_history_pages_reach_oldest_without_crossing_identity(tmp_path):
    url = 'sqlite:///' + str(tmp_path / 'paged.db')
    initialize_database(url)
    repo = ChatRepository(url)
    for i in range(80):
        repo.save_chat_turn(request_id=str(i), user_id='u', character_id='a', session_id='s',
                            user_message=f'question-{i}', response=ChatResponse(text=f'answer-{i}'), provider='test', model='test')
    repo.save_chat_turn(request_id='other', user_id='u', character_id='b', session_id='s',
                        user_message='other', response=ChatResponse(text='other'), provider='test', model='test')
    pages = [repo.list_recent_conversations('u', limit=6, offset=i, character_id='a', session_id='s') for i in range(0, 162, 6)]
    messages = [r.message for page in pages for r in reversed(page)]
    assert len(messages) == len(set(messages)) == 160
    assert messages[0] == 'answer-79' and messages[-1] == 'question-0'
    assert repo.list_recent_conversations('u', limit=6, offset=162, character_id='a', session_id='s') == []
