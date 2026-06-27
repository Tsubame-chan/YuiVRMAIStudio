#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
IOS_PROJECT_DIR="${IOS_PROJECT_DIR:-$REPO_ROOT/builds/YuiVRMAIStudio_iOSPersonalAlpha_v0.1.0-alpha.1}"
IOS_PROJECT="$IOS_PROJECT_DIR/Unity-iPhone.xcodeproj"
SCHEME="${SCHEME:-Unity-iPhone}"
CONFIGURATION="${CONFIGURATION:-Debug}"
SDK="${SDK:-iphoneos}"
if [[ -n "${DEVICE_ID:-}" && -z "${DESTINATION:-}" ]]; then
  DESTINATION="id=$DEVICE_ID"
else
  DESTINATION="${DESTINATION:-generic/platform=iOS}"
fi
XCODEBUILD_JOBS="${XCODEBUILD_JOBS:-1}"
COMPILER_INDEX_STORE_ENABLE="${COMPILER_INDEX_STORE_ENABLE:-NO}"
CODE_SIGNING_ALLOWED="${CODE_SIGNING_ALLOWED:-NO}"
CODE_SIGN_STYLE="${CODE_SIGN_STYLE:-Automatic}"
DERIVED_DATA_PATH="${DERIVED_DATA_PATH:-$IOS_PROJECT_DIR/DerivedData}"
ALLOW_PROVISIONING_UPDATES="${ALLOW_PROVISIONING_UPDATES:-NO}"
ALLOW_DEVICE_REGISTRATION="${ALLOW_DEVICE_REGISTRATION:-NO}"
INSTALL_DEVICE_ID="${INSTALL_DEVICE_ID:-${DEVICE_ID:-}}"

if [[ ! -d "$IOS_PROJECT" ]]; then
  echo "Xcode project not found: $IOS_PROJECT" >&2
  echo "Export it from Unity first: Yui/Build/Build iOS Personal Alpha Xcode Project" >&2
  exit 1
fi

echo "[Yui iOS] Xcode project: $IOS_PROJECT"
echo "[Yui iOS] Scheme       : $SCHEME"
echo "[Yui iOS] Configuration: $CONFIGURATION"
echo "[Yui iOS] SDK          : $SDK"
echo "[Yui iOS] Destination  : $DESTINATION"
echo "[Yui iOS] Jobs         : $XCODEBUILD_JOBS"
echo "[Yui iOS] DerivedData  : $DERIVED_DATA_PATH"
echo "[Yui iOS] Code signing : $CODE_SIGNING_ALLOWED"
if [[ -n "${DEVELOPER_DIR:-}" ]]; then
  echo "[Yui iOS] DEVELOPER_DIR: $DEVELOPER_DIR"
fi
if [[ -n "${DEVELOPMENT_TEAM:-}" ]]; then
  echo "[Yui iOS] Development team: $DEVELOPMENT_TEAM"
fi

args=(
  -project "$IOS_PROJECT"
  -scheme "$SCHEME"
  -configuration "$CONFIGURATION"
  -sdk "$SDK"
  -destination "$DESTINATION"
  -derivedDataPath "$DERIVED_DATA_PATH"
  -jobs "$XCODEBUILD_JOBS"
)

if [[ "$ALLOW_PROVISIONING_UPDATES" == "YES" ]]; then
  args+=(-allowProvisioningUpdates)
fi
if [[ "$ALLOW_DEVICE_REGISTRATION" == "YES" ]]; then
  args+=(-allowProvisioningDeviceRegistration)
fi

xcodebuild \
  "${args[@]}" \
  CODE_SIGNING_ALLOWED="$CODE_SIGNING_ALLOWED" \
  CODE_SIGN_STYLE="$CODE_SIGN_STYLE" \
  COMPILER_INDEX_STORE_ENABLE="$COMPILER_INDEX_STORE_ENABLE" \
  DEVELOPMENT_TEAM="${DEVELOPMENT_TEAM:-}" \
  build

APP_PATH="${APP_PATH:-$DERIVED_DATA_PATH/Build/Products/$CONFIGURATION-iphoneos/YuiVRMAIStudioPersonal.app}"
if [[ "${INSTALL_AFTER_BUILD:-NO}" == "YES" ]]; then
  if [[ -z "$INSTALL_DEVICE_ID" ]]; then
    echo "INSTALL_AFTER_BUILD=YES requires DEVICE_ID or INSTALL_DEVICE_ID." >&2
    exit 1
  fi
  if [[ ! -d "$APP_PATH" ]]; then
    echo "Built app was not found: $APP_PATH" >&2
    exit 1
  fi
  xcrun devicectl device install app --device "$INSTALL_DEVICE_ID" "$APP_PATH"
fi
