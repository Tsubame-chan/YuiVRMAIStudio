# BackendをDockerで起動する

Dockerに慣れている方向けの起動方法です。通常のPC利用では、[同梱Backendの起動手順](../docs/BACKEND_CONSOLE.md)を使ってください。

1. リポジトリ直下で `.env.example` を `.env` へコピーし、必要なBackend設定を入力します。APIキーを含む `.env` を共有しないでください。
2. Dockerを起動し、次のコマンドを実行します。

```sh
docker compose -f deploy/docker-compose.server.yml up --build
```

PCの `http://127.0.0.1:8000/health` でBackendの起動状態を確認できます。VOICEVOXは `http://127.0.0.1:50021/version` です。

このComposeは8000番と50021番をホストへ公開します。起動前にファイアウォール・待受アドレスを確認し、信頼できる端末以外から到達できないようにしてください。アプリ用の会話APIは認証必須ではないため、HTTPS化だけでインターネットへ公開してよい構成にはなりません。

Consoleは接続元をlocalhostに限定します。Dockerのネットワーク構成によってはPCからのアクセスも拒否されます。Consoleで設定・同期・CSV作業を使う場合は、[PC上で起動するBackend](../docs/BACKEND_CONSOLE.md)を使ってください。

会話DBはDocker volume `yui-backend-data` に保存されます。停止するには次を実行します。記録を残したい場合はvolumeを削除しないでください。

```sh
docker compose -f deploy/docker-compose.server.yml down
```

アプリでDirect APIを使う場合は、アプリ側にも別途キーを設定します。Backendのキーをアプリへ転送することはありません。
