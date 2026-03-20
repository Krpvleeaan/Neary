# Neary

**Лёгкий self-hosted трекер геолокации:** минимальный ASP.NET Core API + Android-клиент (.NET MAUI). Данные в RAM, карта в браузере без платных ключей.

---

## Что внутри

| Часть | Стек | Назначение |
|--------|------|------------|
| **Neary.Server** | .NET 9, Minimal API | `POST /api/location` — приём JSON (`UserId`, `Lat`, `Lon`, `Battery`); хранение в `ConcurrentDictionary`; веб-страница с картой (Leaflet + OSM) на `/` |
| **Neary.Mobile** | .NET 9 MAUI (Android) | Тёмный UI, WebView с картой, foreground service — отправка координат и батареи по интервалу |

---

## Быстрый старт (сервер)

```bash
cd Neary.Server
dotnet run
```

По умолчанию слушает **`http://0.0.0.0:5000`** — задайте свой хост/порт через переменные окружения или правку `Program.cs`, если нужно.

Публикация под Linux (пример):

```bash
dotnet publish -c Release -o ./publish
```

---

## Мобильное приложение

1. Откройте **`Neary.Mobile/Resources/Raw/server.json`** и укажите **базовый URL вашего API** (без завершающего `/`), например:
   ```json
   { "baseUrl": "http://192.168.1.10:5000" }
   ```
   На физическом устройстве `localhost` указывает на сам телефон — используйте IP машины в LAN или публичный адрес сервера. Для эмулятора Android к хосту часто используют `http://10.0.2.2:5000`.

2. Соберите Release и установите APK:
   ```bash
   dotnet build Neary.Mobile -c Release -f net9.0-android
   ```
   Подписанный пакет: `Neary.Mobile/bin/Release/net9.0-android/com.neary.tracker-Signed.apk`

Файл `server.json` **не должен** содержать секретов — только URL; при необходимости добавьте локальный `server.local.json` в `.gitignore` (шаблон уже учтён).

---

## API (кратко)

| Метод | Путь | Описание |
|--------|------|----------|
| `POST` | `/api/location` | Тело: `{ "userId", "lat", "lon", "battery" }` |
| `GET` | `/api/locations` | Все записи |
| `GET` | `/api/location/{userId}` | Одна запись по ID |
| `GET` | `/` | Веб-карта по ID |

---

## Требования

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Для Android: workload `maui-android` (`dotnet workload install maui`)

---

## Лицензия

MIT — используйте свободно; для продакшена настройте HTTPS, firewall и резервное копирование по своим правилам.

---

<p align="center">
  <sub>Neary — минимум зависимостей, максимум контроля у себя на сервере.</sub>
</p>
