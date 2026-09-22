"""Exercise the checked-in distribution entrypoints with isolated sample assets."""
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]


class DistributionEntrypointTests(unittest.TestCase):
    def test_compose_context_contains_every_docker_copy_source(self):
        compose = ROOT / "deploy/docker-compose.server.yml"
        text = compose.read_text()
        context = compose.parent / re.search(r"context:\s*(\S+)", text).group(1)
        dockerfile = context / re.search(r"dockerfile:\s*(\S+)", text).group(1)
        self.assertTrue(dockerfile.is_file())
        copies = [shlex.split(line)[1:-1] for line in dockerfile.read_text().splitlines() if line.startswith("COPY ")]
        self.assertTrue(copies)
        for sources in copies:
            for source in sources:
                self.assertTrue((context / source).exists(), f"Docker build context is missing {source}")

    @unittest.skipUnless(sys.platform == "darwin", "uses the macOS package tool")
    def test_minimum_archive_contains_standard_model_and_no_optional_private_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "scripts").mkdir()
            script = root / "scripts/package_minimum_local_ai_assets_macos.sh"
            shutil.copy2(ROOT / "scripts" / script.name, script)
            assets = root / "unity/Assets/StreamingAssets/YuiLocalAI"
            files = {
                "local_ai_model_packs.json": "{}",
                "Models/gemma-4-E2B-it.litertlm": "synthetic standard model",
                "Models/gemma-4-E4B-it.litertlm": "optional model must not ship",
                "Models/private-model.litertlm": "private test sentinel",
                "Voicevox/Models/meimei_himari_1.vvm": "synthetic standard voice",
                "Voicevox/Models/metan_zundamon_0.vvm": "synthetic reviewed voice",
                "Voicevox/Models/kyushu_sora_2.vvm": "synthetic reviewed voice",
                "Voicevox/Models/sayo_15.vvm": "synthetic reviewed voice",
                "Voicevox/Licenses/VOICEVOX_VVM_TERMS.txt": "synthetic license",
                "Voicevox/Models/private.vvm": "private test sentinel",
                "Voicevox/open_jtalk_dic_utf_8-1.11/sys.dic": "synthetic dictionary",
            }
            for name, content in files.items():
                path = assets / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content)
            env = dict(os.environ, YUI_RELEASE_VERSION="test", YUI_RELEASE_SPLIT="0")
            result = subprocess.run(["bash", str(script)], env=env, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            with zipfile.ZipFile(root / "releases/test/YuiVRMAIStudio_LocalAIAssets_Minimum_test.zip") as archive:
                names = archive.namelist()
                self.assertTrue(any(name.endswith("/gemma-4-E2B-it.litertlm") for name in names))
                self.assertTrue(any(name.endswith("/meimei_himari_1.vvm") for name in names))
                for required in ("metan_zundamon_0.vvm", "kyushu_sora_2.vvm", "sayo_15.vvm", "VOICEVOX_VVM_TERMS.txt"):
                    self.assertTrue(any(name.endswith("/" + required) for name in names))
                self.assertFalse(any("private" in name or "E4B" in name for name in names))


if __name__ == "__main__":
    unittest.main()
