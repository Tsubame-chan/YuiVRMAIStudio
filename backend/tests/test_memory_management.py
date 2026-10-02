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


def test_chat_auto_memory_survives_repository_restart_and_stays_with_character(tmp_path, monkeypatch):
    import asyncio
    from app.api import routes
    from app.core.config import Settings
    from app.db.repositories import ChatRepository
    from app.models.chat import ChatRequest, ChatResponse

    url = 'sqlite:///' + str(tmp_path / 'auto-memory.db')
    initialize_database(url)
    seen = []

    class Provider:
        name = 'test'
        async def generate(self, request, history):
            seen.append((request.context.extra or {}).get('memories', []))
            return ChatResponse(text='remembered', memory_action='save')

    monkeypatch.setattr(routes.ProviderRouter, 'chat', lambda self: Provider())
    asyncio.run(routes.chat(ChatRequest(request_id='save-a', user_id='u', character_id='a', session_id='s', message='I like tea'),
                      Settings(database_url=url), ChatRepository(url), MemoryRepository(url)))
    reopened = MemoryRepository(url)
    assert reopened.list_recent('u', character_id='a')[0].content == 'User said: I like tea'
    assert reopened.list_recent('u', character_id='b') == []
    asyncio.run(routes.chat(ChatRequest(request_id='recall-a', user_id='u', character_id='a', session_id='s2', message='What do I like?'),
                      Settings(database_url=url), ChatRepository(url), reopened))
    assert seen[-1][0]['content'] == 'User said: I like tea'
    count = len(reopened.list_recent('u', character_id='a'))
    asyncio.run(routes.chat(ChatRequest(request_id='secret-a', user_id='u', character_id='a', session_id='s', message='private', secret=True),
                      Settings(database_url=url), ChatRepository(url), reopened))
    assert seen[-1][0]["content"] == "User said: What do I like?"
    assert len(reopened.list_recent('u', character_id='a')) == count


def test_realtime_character_memory_isolation_and_secret_no_write(tmp_path):
    from app.core.config import Settings
    from app.db.repositories import ChatRepository
    from app.providers.openai_realtime import RealtimeProvider
    url = 'sqlite:///' + str(tmp_path / 'realtime.db')
    initialize_database(url)
    memory = MemoryRepository(url)
    memory.save(MemorySaveRequest(user_id='u', character_id='a', content='chicken curry'))
    memory.save(MemorySaveRequest(user_id='u', character_id='b', content='quiet girl'))
    chat = ChatRepository(url)
    provider = RealtimeProvider(Settings(database_url=url), chat, memory)
    context = provider._with_realtime_context('personality A', 'u', 'voice', 'a', True)
    assert 'chicken curry' in context and 'quiet girl' not in context
    provider._save_realtime_turn('u', 'remember secret', 'ok', 'voice', 'a', True)
    assert chat.list_recent_messages('u', character_id='a') == []
    assert len(memory.list_recent('u', character_id='a')) == 1
    provider._save_realtime_turn('u', '覚えて ordinary A', 'ok', 'voice', 'a', False)
    assert len(chat.list_recent_messages('u', character_id='a')) == 2
    assert chat.list_recent_messages('u', character_id='b') == []
    assert len(memory.list_recent('u', character_id='a')) == 2


@pytest.mark.parametrize('provider_name', ['openai', 'lmstudio', 'xai'])
def test_all_chat_providers_receive_existing_character_memory_in_secret_mode(provider_name):
    from app.core.config import Settings
    from app.models.chat import ChatRequest, RequestContext
    from app.providers.openai_chat import OpenAIChatProvider
    from app.providers.lmstudio_chat import LMStudioChatProvider
    from app.providers.xai_chat import XAIChatProvider
    settings = Settings(openai_api_key='test', xai_api_key='test')
    request = ChatRequest(request_id='r', user_id='u', message='What do I like?', secret=True,
                          context=RequestContext(extra={'character_memory': 'I prefer chicken curry'}))
    if provider_name == 'openai':
        provider = OpenAIChatProvider.__new__(OpenAIChatProvider)
        provider.settings = settings
        payload = provider._current_user_input(request)
    else:
        provider = (LMStudioChatProvider if provider_name == 'lmstudio' else XAIChatProvider)(settings)
        payload = provider._current_user_message(request)
    assert 'chicken curry' in str(payload)
