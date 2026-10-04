"""Send opt-in Companion Work Events from the durable Activity outbox.

Run separately from the MCP HTTP listener. Only IDs reach OpenAI callbacks;
the client fetches current request text with its scoped OAuth credential.
"""
from __future__ import annotations

import os
import time

from app.core.companion_v2 import CompanionStore
from app.core.config import get_settings
from tools.companion_events import CompanionEvents


def main() -> None:
    if (os.environ.get("YUI_COMPANION_EVENTS_TESTING_ENABLED") != "true" or
            os.environ.get("YUI_COMPANION_MCP_TESTING_ENABLED") != "true" or
            not get_settings().companion_v2_testing_enabled):
        raise RuntimeError("Companion Events require explicit testing flags")
    dispatcher = CompanionEvents(CompanionStore(get_settings().database_url), None)
    while True:
        dispatcher.deliver_once()
        time.sleep(3)


if __name__ == "__main__":
    main()
