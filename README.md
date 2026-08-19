# ShortLinkBridge

میکروسرویس پردازش صف لینک کوتاه. کنار `short-links` روی همان سرور لینوکس اجرا می‌شود.

جاب داخل خود سرویس است (هر ۱۰ ثانیه صف را می‌خواند). SQL Agent لازم نیست.

## جریان کار

```
ثبت نقطه در MapPoints
        │
        ▼
VisitLink خودکار (ستون محاسباتی)
        │
        ▼
Trigger → ShortLinkQueue
        │
        ▼
ShortLinkBridge (جاب داخلی، هر ۱۰ ثانیه)
        │
        ▼
ShortLinks API → ShortVisitLink ذخیره می‌شود
```

## اجرا در توسعه

```bash
cd short-link-bridge/src/ShortLinkBridge.Api
dotnet run
```

## تنظیمات

| کلید | توضیح |
|---|---|
| `ConnectionStrings:QueueDatabase` | دیتابیس نقشه (`apiweb-locationsmap`) |
| `ShortLinks:BaseUrl` | آدرس سرویس کوتاه‌کننده (`http://127.0.0.1:5013`) |
| `Security:ApiKey` | کلید احراز هویت برای فراخوانی دستی |
| `Queue:BatchSize` | تعداد رکورد در هر اجرا |
| `Queue:PollIntervalSeconds` | فاصله جاب داخلی (پیش‌فرض ۱۰) |

## تست دستی

```bash
curl -X POST "http://127.0.0.1:5014/api/queue/process?batchSize=10" \
  -H "X-Api-Key: CHANGE_ME_BRIDGE_API_KEY"

curl http://127.0.0.1:5014/api/queue/health
```
