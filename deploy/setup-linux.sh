#!/bin/bash
# استقرار ShortLinkBridge روی AlmaLinux / RHEL
# اجرا با root از روی سرور لینوکس کنار short-links
set -euo pipefail

SRC=/opt/short-link-bridge
APP=/var/www/shortlinkbridge
REPO=https://github.com/amirreza-fnt/short-link-bridge.git

if [[ -z "${DB_PASSWORD:-}" ]]; then
  echo "DB_PASSWORD را ست کنید. مثال:"
  echo "  DB_PASSWORD='YOUR_LOCATION_DB_PASSWORD' bash $0"
  exit 1
fi

BRIDGE_API_KEY="${BRIDGE_API_KEY:-$(openssl rand -hex 16)}"

if [[ ! -d "$SRC/.git" ]]; then
  git clone "$REPO" "$SRC"
else
  git -C "$SRC" pull --ff-only
fi

mkdir -p "$APP"
dotnet publish "$SRC/src/ShortLinkBridge.Api/ShortLinkBridge.Api.csproj" \
  -c Release --self-contained false -o "$APP"

cat > "$APP/appsettings.Production.json" <<EOF
{
  "ConnectionStrings": {
    "QueueDatabase": "Server=185.255.91.242,2019;Database=apiweb-locationsmap;User Id=apiweblocationsmapuser;Password=${DB_PASSWORD};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true"
  },
  "ShortLinks": {
    "BaseUrl": "http://127.0.0.1:5013",
    "GroupName": null,
    "TimeoutSeconds": 30
  },
  "Queue": {
    "BatchSize": 50,
    "MaxAttempts": 5,
    "PollIntervalSeconds": 10
  },
  "Security": {
    "ApiKey": "${BRIDGE_API_KEY}"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://127.0.0.1:5014"
      }
    }
  }
}
EOF
chmod 600 "$APP/appsettings.Production.json"

if ! id shortlinks >/dev/null 2>&1; then
  useradd --system --home /var/www/shortlinks --shell /sbin/nologin shortlinks
fi
chown -R shortlinks:shortlinks "$APP"

cp "$SRC/deploy/shortlinkbridge.service" /etc/systemd/system/shortlinkbridge.service
systemctl daemon-reload
systemctl enable --now shortlinkbridge.service
systemctl restart shortlinkbridge.service

echo "Bridge API key: $BRIDGE_API_KEY"
systemctl --no-pager --full status shortlinkbridge.service || true
curl -sS http://127.0.0.1:5014/api/queue/health || true
