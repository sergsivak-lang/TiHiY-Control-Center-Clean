# Модуль FPS / FrameGraph для TiHiY StreamControl Center

Цей пакет додає вкладку **ПРОДУКТИВНІСТЬ** з живим графіком:

- FPS;
- FRAME, ms;
- CPU FRAME, ms;
- GPU FRAME, ms;
- графік останніх 60 секунд;
- експорт статистики у CSV.

## Як установити

1. Розпакуйте ZIP у корінь репозиторію `TiHiY-StreamControl-Center` із заміною файлу `Program.cs`.
2. Завантажте консольний файл PresentMon x64 з офіційних релізів:
   `https://github.com/GameTechDev/PresentMon/releases/latest`
3. Покладіть `PresentMon-...-x64.exe` поряд із готовим `TiHiY.StreamControlCenter.exe` або виберіть його кнопкою **ОБРАТИ EXE**.
4. Запустіть Star Citizen.
5. У вкладці **ПРОДУКТИВНІСТЬ** натисніть **ПОЧАТИ МОНІТОРИНГ**.

## Доступ Windows

Якщо PresentMon повідомляє `access denied`, запустіть TiHiY StreamControl Center від адміністратора. Альтернативно користувач Windows має входити до локальної групи **Performance Log Users**.

## Важливо

Модуль не втручається у файли та пам’ять Star Citizen. Дані кадрів надходять через Windows ETW, тому значення можуть трохи відрізнятися від внутрішньої команди гри `r_displayFrameGraph 1`.
