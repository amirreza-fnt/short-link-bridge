# ShortLinkBridge

میکروسرویس سبک برای پردازش صف لینک کوتاه در دیتابیس.

## جریان کار

```
MapPoints INSERT/UPDATE
        │
        ▼
  SQL Trigger ──► ShortLinkQueue (Status=Pending)
        │
        ▼
 SQL Agent Job (هر ۱۰ ثانیه)
        │
        ▼
 POST /api/queue/process  ──►  ShortLinkBridge
        │                           │
        │                           ▼
        │                    POST /api/links/batch
        │                           │
        │                           ▼
        │                      ShortLinks API
        │                           │
        ▼                           ▼
 UPDATE MapPoints.ShortVisitLink ◄───┘
```

## اجرا

```bash
cd short-link-bridge/src/ShortLinkBridge.Api
dotnet run
```

## تنظیمات

| کلید | توضیح |
|---|---|
| `ConnectionStrings:QueueDatabase` | دیتابیسی که جدول `ShortLinkQueue` در آن است |
| `ShortLinks:BaseUrl` | آدرس سرویس کوتاه‌کننده (مثلاً `http://127.0.0.1:5013`) |
| `Security:ApiKey` | کلید احراز هویت برای SQL Agent Job |
| `Queue:BatchSize` | تعداد رکورد در هر اجرا (پیش‌فرض ۵۰) |

## SQL Agent

اسکریپت `location/Scripts/004_CreateSqlAgentJob.sql` را با آدرس و ApiKey واقعی اجرا کنید.

## تست دستی

```bash
curl -X POST "http://localhost:5014/api/queue/process?batchSize=10" \
  -H "X-Api-Key: CHANGE_ME_BRIDGE_API_KEY"
```
