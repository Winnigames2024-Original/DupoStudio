<div align="center">

<img src="docs/assets/logo.svg" width="88" alt="Dupo Studio">

# Dupo Studio

**Конструктор приложений на собственном языке DupoLang**

[![ci](https://github.com/Winnigames2024-Original/DupoStudio/actions/workflows/ci.yml/badge.svg)](https://github.com/Winnigames2024-Original/DupoStudio/actions/workflows/ci.yml)
[![release](https://img.shields.io/github/v/release/Winnigames2024-Original/DupoStudio?label=релиз)](https://github.com/Winnigames2024-Original/DupoStudio/releases)
[![license](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[Сайт](https://winnigames2024-original.github.io/DupoStudio/) ·
[Релизы](https://winnigames2024-original.github.io/DupoStudio/releases.html) ·
[История версий](CHANGELOG.md)

</div>

---

Пишете три строки на DupoLang — получаете готовый `.exe`.
Веб-сервер, PHP и JDK ставятся сами, файлами управляет `Dupo.System.Files`,
проект пакуется в собственный формат `.dapp` и собирается в один
самостоятельный файл. C# WinForms, .NET Framework 4.8.

```
C:\DupoApp\
├─ DupoApp.sln          решение Visual Studio
├─ DupoLang\            язык: лексер, парсер, интерпретатор, модули
│  └─ Web\              Web.Server и установщик PHP
├─ DupoStudio\          IDE (WinForms): редактор, дерево, консоль
├─ DupoRun\             консольный запускатель — из него собираются exe
├─ Projects\HelloWeb\   пример проекта
└─ runtime\php\         сюда скачивается PHP
```

## Сборка

`build.bat` или открыть `DupoApp.sln` в Visual Studio → Build.
Запуск студии — `run.bat`.

## Язык

```dupolang
import Dupo.Web

Web.Server site = new Web.Server(8080, "www", "index.html")

if (Web.Server.Php.IsInstalled == false) {
    Web.Server.Php.Install()
}

site.Start()
Web.Open(site.Url)
```

Подключение модулей: `import Dupo.Web` или `using Dupo.Web` — это одно и то же.
Переменные: `var x = 1`, либо с типом — `string s = "привет"`, `Web.Server site = ...`
Операторы: `if / else`, `while`, `for`, `func`, `return`, `break`, `continue`
Типы значений: число, строка, bool, null, массив `[1, 2, 3]`
Комментарии: `//`, `#`, `/* ... */`

### Встроенные функции

`print` `str` `num` `int` `round` `abs` `len` `type` `wait` `now` `random`
`list` `push` `upper` `lower` `split`

### Модуль Dupo.Web

| Что | Зачем |
|---|---|
| `new Web.Server(порт, "папка", "главный файл")` | создать сервер |
| `site.Start()` / `site.Stop()` | запустить / остановить |
| `site.Url`, `site.Port`, `site.Root`, `site.Running` | свойства |
| `Web.Server.Php.Install()` | скачать и поставить PHP |
| `Web.Server.Php.IsInstalled`, `.Version`, `.Folder` | состояние PHP |
| `Web.Server.Php.Uninstall()` | удалить PHP |
| `Web.Open(url)` | открыть в браузере |
| `Web.Get(url)` | скачать страницу текстом |
| `Web.Download(url, файл)` | скачать файл |

Сервер раздаёт статику сам, а `.php` отдаёт через `php-cgi.exe` по протоколу CGI —
работают `$_GET`, `$_POST`, заголовки, редиректы.

### Модуль Dupo.App — формат .dapp

```dupolang
import Dupo.App

string file = App.Export("HelloWeb.dapp", "HelloWeb", "1.0", "Автор")
print(App.Info(file))
App.Import("HelloWeb.dapp", "C:/Games/HelloWeb")
```

| Что | Зачем |
|---|---|
| `App.Export(файл, [имя], [версия], [автор])` | упаковать текущий проект |
| `App.Import(файл, [папка])` | распаковать, вернуть путь к главному скрипту |
| `App.Unpack(файл)` | распаковать во временную папку |
| `App.Info(файл)` | паспорт архива текстом |
| `App.Files(файл)` | список файлов внутри |

### Модуль Dupo.System.Files — файлы и папки

```dupolang
using Dupo.System.Files

Files.Write("data/notes.txt", "Первая строка\n")
Files.AppendLine("data/notes.txt", "Вторая строка")

var lines = Files.ReadLines("data/notes.txt")
print("Строк: " + len(lines))

Files.ReplaceIn("data/notes.txt", "строка", "запись")
Files.Copy("data/notes.txt", "data/backup.txt")
Files.Zip("data", "data.zip")
```

| Группа | Функции |
|---|---|
| Создание и запись | `Create` `Write` `Append` `AppendLine` |
| Чтение | `Read` `ReadLines` `Contains` `ReplaceIn` |
| Файлы | `Exists` `Delete` `Copy` `Move` `Rename` `Size` `Modified` |
| Пути | `Name` `NameOnly` `Extension` `Folder` `Full` `Join` `Temp` |
| Папки | `MakeDir` `DirExists` `DeleteDir` `CopyDir` `ProjectDir` |
| Списки | `List` `Dirs` `Find` `Tree` `Count` |
| Архивы | `Zip` `Unzip` |
| Система | `Open` |

`Find(папка, маска)` ищет во вложенных папках. Относительные пути считаются
от папки скрипта.

### Модуль Dupo.Java — разработка на Java

```dupolang
using Dupo.Java

if (Java.IsInstalled == false) {
    Java.Install()
}

Java.Compile("src", "build/classes")
string jar = Java.Jar("build/classes", "build/MyApp.jar", "Main")
Java.RunJar(jar)
```

| Что | Зачем |
|---|---|
| `Java.Install()` | скачать Temurin JDK с adoptium.net (около 190 МБ) |
| `Java.IsInstalled`, `Java.Version`, `Java.Folder` | состояние JDK |
| `Java.Uninstall()` | удалить JDK |
| `Java.New(папка, класс)` | создать заготовку `.java` |
| `Java.Compile(исходники, папка классов)` | javac с UTF-8 |
| `Java.Jar(классы, файл.jar, главный класс)` | собрать исполняемый jar |
| `Java.Build(исходники, файл.jar, класс)` | компиляция и упаковка одним шагом |
| `Java.Run(классы, класс)`, `Java.RunJar(файл)` | запустить и вернуть вывод |

JDK ставится в `runtime\jdk` рядом со студией и не трогает систему —
`JAVA_HOME` и `PATH` остаются как были.

### Модуль Dupo.IO

`IO.ReadFile` `IO.WriteFile` `IO.AppendFile` `IO.Exists` `IO.MakeDir`
`IO.Delete` `IO.Files` `IO.Dirs` `IO.ProjectDir`

### Модуль Dupo.Sys

`Sys.Run` `Sys.Open` `Sys.Env` `Sys.Sleep` `Sys.Time` `Sys.Date`

## Студия

- **F5** — запустить, **Shift+F5** — остановить
- **Инструменты → Установить PHP** — качает PHP с windows.php.net в `runtime\php`
- **Запуск → Собрать в один .exe** (Ctrl+B) — один самостоятельный файл
- **Запуск → Собрать папку с файлами** — exe + `DupoLang.dll` + скрипт + `www` россыпью

Относительные пути внутри скрипта считаются от папки самого `.dupolang`.


## Формат .dapp

Свой контейнер приложения — не переименованный zip, а собственная структура:

```
"DAPP"      4 байта сигнатуры
uint16      версия формата (сейчас 1)
string      манифест UTF-8 (длина 7-битным префиксом)
int32       количество записей
записи:
    string  путь относительно корня, всегда через '/'
    int32   исходный размер
    uint32  CRC32 исходных байтов
    byte    способ: 0 — как есть, 1 — deflate
    int32   размер в архиве
    byte[]  данные
"DEND"      4 байта завершителя
```

Каждый файл жмётся Deflate и кладётся тем способом, который вышел меньше, —
картинки и архивы не раздуваются. При распаковке считается CRC32: не сошлось —
архив объявляется повреждённым с указанием, на каком файле. Пути с `..` и
абсолютные отбрасываются, так что архив не может записать что-то вне своей папки.

Манифест — обычные строки `ключ: значение`:

```
name: HelloWeb
version: 1.0
main: main.dupolang
author: Lilia
description:
created: 27.10.2026 11:20
runtime: DupoLang 1.0
```

Незнакомые ключи не теряются — попадают в `Extra`, так что формат расширяется
без слома старых архивов.

### Как пользоваться

- **Файл → Экспорт в .dapp** (Ctrl+E) — упаковать проект; спросит версию.
- **Файл → Импорт .dapp** — распаковать в `Projects\<имя>` и сразу открыть.
- `DupoRun.exe MyApp.dapp` — запускает архив напрямую: распаковывает во временную
  папку и выполняет главный скрипт из манифеста. Разворачивать вручную не нужно.


## Сборка проекта в один .exe

**Запуск → Собрать в один .exe** (Ctrl+B) кладёт в `build\ИмяПроекта.exe` один файл.
Никаких DLL рядом, никакой распаковки руками — пользователю нужен только
.NET Framework 4.8, который уже есть в Windows 10 и 11.

Как это устроено:

```
[ обычный PE-образ DupoHost.exe ][ байты .dapp ][ int64 длина ][ uint32 CRC32 ][ "DAPPBND1" ]
```

`DupoHost` — рантайм, в который язык вкомпилирован исходниками, поэтому
`DupoLang.dll` ему не нужна. Студия копирует хост, дописывает архив в хвост
и ставит метку. Windows лишние байты после PE-образа игнорирует, так что exe
остаётся обычным исполняемым файлом.

При старте хост читает сам себя, находит метку, сверяет CRC32, разворачивает
приложение во временную папку (один раз — дальше берёт готовое) и запускает
главный скрипт из манифеста.

Ключ `console` в манифесте прячет консольное окно — удобно, когда приложение
просто поднимает сервер и открывает браузер:

```
console: off
```

### Три способа отдать приложение

| Способ | Что получается | Чем запускать |
|---|---|---|
| Собрать в один .exe | `MyApp.exe` | двойной клик |
| Экспорт в .dapp | `MyApp.dapp` | `DupoRun.exe MyApp.dapp` |
| Собрать папку с файлами | exe + dll + исходники | двойной клик, код видно и можно править |
