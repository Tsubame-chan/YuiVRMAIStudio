"""Create the editable Companion screen comparison for M3E Canvas."""

import copy
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/design/settings_20260922/yui-settings.m3e.json"
OUTPUT = ROOT / "docs/design/companion_core_20261003/companion-entry.m3e.json"
base = json.loads(SOURCE.read_text())
base.update(title="Yui · Talk / Work / AI 担当", brief="二つの入口を維持し、返答担当と作業担当を設定のAI欄で分ける。未接続の外部担当は選べない。")
base["frames"] = [
    dict(id="talk", name="トーク · 通常", x=0, y=0, w=900, h=760),
    dict(id="work", name="ワーク · 依頼中", x=960, y=0, w=900, h=760),
    dict(id="settings", name="設定 · AI", x=1920, y=0, w=900, h=760),
]
base["groups"] = []
base["promptEdit"] = "M3E comparison original. Implement in existing Unity uGUI, preserve Talk/Work and current avatar. No third mode or WebView. External choices stay unavailable until real connection verification."


def add(x, y, kind, label, width=820, **more):
    idx = len(base["groups"])
    item = dict(id=f"companion-item-{idx}", kind=kind, label=label, icon=more.pop("icon", None), variant=more.pop("variant", "tonal"), size=width)
    item.update(more)
    base["groups"].append(dict(id=f"companion-group-{idx}", x=x, y=y, axis="y", items=[item]))


add(40, 30, "text", "トーク", 32, bold=True)
add(40, 98, "tabs", "", tabs=[dict(label="トーク", icon=""), dict(label="ワーク", icon="")], selected=0)
add(40, 183, "text", "返答担当: Yui", 20)
add(40, 230, "text", "キャラクターとの会話", 16)
add(40, 315, "listItem", "今日もお話ししましょう", supporting="いつものキャラクターの声で返答します。", icon="chat_bubble", fill="surfaceContainer")
add(40, 650, "textField", "メッセージを入力", supporting="会話はそのまま続けられます")

add(1000, 30, "text", "ワーク", 32, bold=True)
add(1000, 98, "tabs", "", tabs=[dict(label="トーク", icon=""), dict(label="ワーク", icon="")], selected=1)
add(1000, 183, "text", "作業担当: Yui", 20)
add(1000, 230, "text", "依頼と結果を読みやすく表示", 16)
add(1000, 310, "listItem", "資料を調べて要点をまとめて", supporting="進行中 · 完了したらこの画面に報告します", icon="assignment", fill="surfaceContainer")
add(1000, 420, "listItem", "別の会話もできます", supporting="作業中でもトークに戻れます", icon="chat", fill="surfaceContainer")
add(1000, 650, "textField", "追加の依頼を入力")

add(1960, 30, "text", "設定", 32, bold=True)
add(1960, 94, "tabs", "", tabs=[dict(label="AI", icon=""), dict(label="音声", icon=""), dict(label="キャラクター", icon=""), dict(label="表示", icon=""), dict(label="詳細", icon="")], selected=0)
add(1960, 180, "text", "AI の担当", 24, bold=True)
add(1960, 225, "text", "入口はトークとワークのままです。", 16)
add(1960, 285, "listItem", "トークの返答担当 · Yui", supporting="キャラクターとの会話を返します", icon="chat_bubble", icon2="chevron_right", fill="surfaceContainer")
add(1960, 390, "listItem", "ワークの作業担当 · Yui", supporting="調べ物や作業を担当します", icon="assignment", icon2="chevron_right", fill="surfaceContainer")
add(1960, 510, "text", "外部の担当", 20)
add(1960, 560, "listItem", "dot · 未接続", supporting="接続確認が完了すると選べます", icon="link_off", fill="surfaceContainer")

OUTPUT.write_text(json.dumps(base, ensure_ascii=False, indent=2) + "\n")
print(OUTPUT)
