import json
from pathlib import Path
import pytest
from app.core.config import Settings
from app.core.release_version import read_release_versions


def test_backend_version_matches_shared_desktop_metadata():
    assert Settings(_env_file=None).app_version == read_release_versions()["desktop_tag"][1:]


def test_release_metadata_fails_on_missing_or_invalid_fields(tmp_path: Path):
    path = tmp_path / "release.json"
    path.write_text(json.dumps({"desktop_tag": "../wrong", "ios_version": "0.2.6", "ios_build": "20"}))
    with pytest.raises(ValueError, match="desktop_tag"):
        read_release_versions(path)


def test_backend_app_version_environment_override_remains_available(monkeypatch):
    monkeypatch.setenv("APP_VERSION", "test-deployment")
    assert Settings(_env_file=None).app_version == "test-deployment"
