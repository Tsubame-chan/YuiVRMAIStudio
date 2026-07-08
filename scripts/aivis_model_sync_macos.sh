#!/usr/bin/env bash

aivis_addon_script_dir() {
  cd "$(dirname "${BASH_SOURCE[0]}")" && pwd
}

aivis_addon_repo_root() {
  local script_dir
  script_dir="$(aivis_addon_script_dir)"
  printf '%s\n' "${REPO_ROOT:-$(cd "$script_dir/.." && pwd)}"
}

aivis_link_or_copy() {
  local source="$1"
  local target="$2"
  local current_link

  if [[ -L "$target" ]]; then
    current_link="$(readlink "$target" || true)"
    [[ "$current_link" == "$source" ]] && return 0
    rm -f "$target"
  elif [[ -e "$target" ]]; then
    rm -f "$target"
  fi

  ln -s "$source" "$target" 2>/dev/null || cp -p "$source" "$target"
}

sync_aivis_addon_models() {
  local repo_root
  repo_root="$(aivis_addon_repo_root)"
  local source_dir="${AIVIS_MODELS_SOURCE_DIR:-$repo_root/tools/tts/aivis-models/selected}"
  local target_root="${AIVIS_MODELS_DIR:-$HOME/Library/Application Support/AivisSpeech-Engine/Models}"

  [[ -d "$source_dir" ]] || return 0

  local previous_nullglob
  previous_nullglob="$(shopt -p nullglob || true)"
  shopt -s nullglob
  local models=("$source_dir"/*.aivm "$source_dir"/*.aivmx)
  if [[ -n "$previous_nullglob" ]]; then
    eval "$previous_nullglob"
  else
    shopt -u nullglob
  fi

  [[ ${#models[@]} -gt 0 ]] || return 0

  mkdir -p "$target_root"

  local model base target_file
  for model in "${models[@]}"; do
    base="$(basename "$model")"
    target_file="$target_root/$base"
    aivis_link_or_copy "$model" "$target_file"
  done

  echo "[Yui services] AivisSpeech add-on models are available in: $target_root"
}

sync_aivis_bert_cache() {
  local repo_root
  repo_root="$(aivis_addon_repo_root)"
  local source_dir="${AIVIS_BERT_SOURCE_DIR:-$repo_root/tools/tts/aivis-engine/extracted/macOS-arm64/engine_internal/style_bert_vits2/bert/deberta-v2-large-japanese-char-wwm-onnx}"
  local user_data_dir="${AIVIS_USER_DATA_DIR:-$HOME/Library/Application Support/AivisSpeech-Engine}"
  local cache_root="${AIVIS_BERT_CACHE_DIR:-$user_data_dir/BertModelCaches}"
  local repo_cache="$cache_root/models--tsukumijima--deberta-v2-large-japanese-char-wwm-onnx"
  local revisions="${AIVIS_BERT_REVISIONS:-d701ec67708287b20d2063270f6b535e6eed09ab 5e5cc2b628d083d0a815c4d4a4c3fe84a414f8ed}"

  [[ -f "$source_dir/model_fp16.onnx" ]] || return 0

  mkdir -p "$repo_cache/refs"

  local revision snapshot_dir file first_revision
  first_revision="${revisions%% *}"
  printf '%s\n' "$first_revision" > "$repo_cache/refs/main"

  for revision in $revisions; do
    snapshot_dir="$repo_cache/snapshots/$revision"
    mkdir -p "$snapshot_dir"
    for file in \
      config.json \
      model_fp16.onnx \
      special_tokens_map.json \
      tokenizer.json \
      tokenizer_config.json \
      vocab.txt; do
      [[ -f "$source_dir/$file" ]] || continue
      aivis_link_or_copy "$source_dir/$file" "$snapshot_dir/$file"
    done
  done

  export HF_HUB_OFFLINE="${HF_HUB_OFFLINE:-1}"
  export TRANSFORMERS_OFFLINE="${TRANSFORMERS_OFFLINE:-1}"
  echo "[Yui services] AivisSpeech BERT cache is available in: $cache_root"
}

prepare_aivis_addon_runtime() {
  sync_aivis_addon_models
  sync_aivis_bert_cache
}
