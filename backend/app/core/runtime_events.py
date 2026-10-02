"""Private, short-lived diagnostic metadata. Never retain request/response bodies."""
from collections import deque
from contextvars import ContextVar
from datetime import datetime, timedelta, timezone
from threading import Lock
import errno
import httpx

TRACE = ContextVar('yui_runtime_trace', default='')
EVENTS = deque(maxlen=1000)  # Memory safety bound, not a UI page size.
LOCK = Lock()
sequence = 0
omitted = False


def record(stage: str, *, operation: str = '', provider: str = '', counts: dict | None = None,
           status: int | None = None, elapsed_ms: int | None = None, code: str = '', model: str = ''):
    global sequence, omitted
    now = datetime.now(timezone.utc)
    with LOCK:
        cutoff = (now - timedelta(hours=24)).isoformat()
        while EVENTS and EVENTS[0]['time'] < cutoff:
            EVENTS.popleft(); omitted = True
        if len(EVENTS) == EVENTS.maxlen: omitted = True
        sequence += 1
        item = {'sequence': sequence, 'time': now.isoformat(), 'trace': TRACE.get(),
                'stage': stage, 'operation': operation, 'provider': provider}
        if code: item['code'] = code
        if model: item['model'] = model[:120]
        if counts is not None:
            item['counts'] = {k: int(counts[k]) for k in ('history', 'memories', 'local_memory_chars', 'screen_chars') if k in counts}
        if status is not None: item['status'] = status
        if elapsed_ms is not None: item['elapsed_ms'] = elapsed_ms
        EVENTS.append(item)


def classify_error(exc: Exception) -> str:
    """Inspect typed causes and provider codes; do not return exception text."""
    current = exc
    for _ in range(8):
        if isinstance(current, httpx.TimeoutException) or type(current).__name__ == 'APITimeoutError': return 'timeout'
        if isinstance(current, httpx.ConnectError) or type(current).__name__ == 'APIConnectionError': return 'unreachable'
        if isinstance(current, PermissionError): return 'permission'
        if isinstance(current, OSError) and current.errno == errno.ENOSPC: return 'disk_full'
        code = getattr(current, 'code', None)
        if code in {'insufficient_quota', 'billing_hard_limit_reached'}: return 'quota'
        if code == 'model_not_found': return 'model_not_found'
        response = getattr(current, 'response', None)
        status = getattr(current, 'status_code', None) or getattr(response, 'status_code', None)
        if status in (401, 403): return 'authentication'
        if status == 429: return 'rate_limit'
        if status == 404: return 'not_found'
        if isinstance(status, int) and status >= 500: return 'provider_unavailable'
        # Known configuration messages originate in Yui adapters, never exported.
        message = str(current)
        if 'API_KEY is not configured' in message or type(current).__name__ == 'ProviderConfigurationError': return 'missing_key'
        if 'HTTP_TTS_BASE_URL is required' in message: return 'missing_endpoint'
        if type(current).__name__ == 'ProviderNotImplementedError': return 'unsupported'
        next_cause = current.__cause__ or current.__context__
        if next_cause is None or next_cause is current: break
        current = next_cause
    return 'provider_error'


def diagnose(exc: Exception, *, operation: str = '', provider: str = ''):
    record('issue', operation=operation, provider=provider, code=classify_error(exc))


def events_since(after: int = 0) -> dict:
    with LOCK:
        reset = after > sequence
        if reset: after = 0
        cutoff = (datetime.now(timezone.utc) - timedelta(hours=24)).isoformat()
        retained = [x for x in EVENTS if x['time'] >= cutoff]
        items = [dict(x) for x in retained if x['sequence'] > after]
        return {'items': items, 'cursor': sequence, 'reset': reset,
                'truncated': omitted or bool(retained and after and after < retained[0]['sequence'] - 1)}
