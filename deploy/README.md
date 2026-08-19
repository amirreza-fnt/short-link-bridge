# استقرار ShortLinkBridge روی همان سرور short-links

جاب صف داخل خود سرویس است (هر ۱۰ ثانیه). SQL Agent لازم نیست.

## مسیرها

| چیز | مقدار |
|---|---|
| سورس | `/opt/short-link-bridge` |
| اجرا | `/var/www/shortlinkbridge` |
| پورت | `5014` (فقط لوکال) |
| دیتابیس صف | `apiweb-locationsmap` روی `185.255.91.242,2019` |
| سرویس short-links | `http://127.0.0.1:5013` |
