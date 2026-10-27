using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using DupoLang;

namespace DupoStudio
{
    /// <summary>Одна реплика в чате.</summary>
    public class ChatLine
    {
        public string Who = "вы";      // «вы» или «помощник»
        public string Text = "";
        public string Stamp = DateTime.Now.ToString("HH:mm");

        public bool Mine { get { return string.Equals(Who, "вы", StringComparison.OrdinalIgnoreCase); } }
    }

    /// <summary>Тема, на которую помощник умеет отвечать.</summary>
    class Topic
    {
        public string[] Keys;
        public string Title;
        public string Answer;

        public Topic(string title, string keys, string answer)
        {
            Title = title;
            Keys = keys.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            Answer = answer;
        }
    }

    /// <summary>
    /// Помощник студии. Знает язык DupoLang, C# и .Designer.cs, Java
    /// и связку между ними. Работает без интернета по своей базе знаний,
    /// а если задать адрес и ключ совместимого с OpenAI сервиса —
    /// спрашивает его и отвечает уже оттуда.
    /// </summary>
    public static class Assistant
    {
        public const string FolderName = ".dupo";
        public const string ChatFile = "assistant.json";

        // ------------------------------------------------------------------
        // история чата — своя на каждый проект
        // ------------------------------------------------------------------

        /// <summary>Файл истории для проекта.</summary>
        public static string HistoryPath(string projectDir)
        {
            if (string.IsNullOrEmpty(projectDir)) return null;
            return Path.Combine(projectDir, FolderName, ChatFile);
        }

        /// <summary>Прочитать историю проекта.</summary>
        public static List<ChatLine> Load(string projectDir)
        {
            var res = new List<ChatLine>();
            string path = HistoryPath(projectDir);
            if (path == null || !File.Exists(path)) return res;

            try
            {
                var list = JsonTools.Parse(File.ReadAllText(path, Encoding.UTF8)) as List<object>;
                if (list == null) return res;

                foreach (object item in list)
                {
                    var m = item as DupoMap;
                    if (m == null) continue;

                    res.Add(new ChatLine
                    {
                        Who = Val.Str(m.Get("who", "вы")),
                        Text = Val.Str(m.Get("text", "")),
                        Stamp = Val.Str(m.Get("stamp", ""))
                    });
                }
            }
            catch { }
            return res;
        }

        /// <summary>Записать историю проекта.</summary>
        public static void Save(string projectDir, List<ChatLine> lines)
        {
            string path = HistoryPath(projectDir);
            if (path == null) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var list = new List<object>();
                foreach (var l in lines)
                {
                    var m = new DupoMap();
                    m.Set("who", l.Who);
                    m.Set("text", l.Text);
                    m.Set("stamp", l.Stamp);
                    list.Add(m);
                }

                File.WriteAllText(path, JsonTools.Pretty(list), new UTF8Encoding(false));
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // настройки внешнего сервиса
        // ------------------------------------------------------------------

        public class Online
        {
            public string Endpoint = "";
            public string Key = "";
            public string Model = "";

            public bool Ready
            {
                get { return Endpoint.Trim().Length > 0 && Key.Trim().Length > 0; }
            }
        }

        static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DupoStudio");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "assistant-online.json");
            }
        }

        /// <summary>Настройки внешнего сервиса — лежат у пользователя, не в проекте.</summary>
        public static Online LoadOnline()
        {
            var o = new Online();
            try
            {
                if (!File.Exists(SettingsPath)) return o;

                var m = JsonTools.Parse(File.ReadAllText(SettingsPath, Encoding.UTF8)) as DupoMap;
                if (m == null) return o;

                o.Endpoint = Val.Str(m.Get("endpoint", ""));
                o.Key = Val.Str(m.Get("key", ""));
                o.Model = Val.Str(m.Get("model", ""));
            }
            catch { }
            return o;
        }

        public static void SaveOnline(Online o)
        {
            try
            {
                var m = new DupoMap();
                m.Set("endpoint", o.Endpoint ?? "");
                m.Set("key", o.Key ?? "");
                m.Set("model", o.Model ?? "");
                File.WriteAllText(SettingsPath, JsonTools.Pretty(m), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>Спросить внешний сервис. Возвращает ответ или бросает исключение.</summary>
        public static string AskOnline(Online o, string projectContext, List<ChatLine> history, string question)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var messages = new List<object>();

            var sys = new DupoMap();
            sys.Set("role", "system");
            sys.Set("content", SystemPrompt(projectContext));
            messages.Add(sys);

            foreach (var l in history.Skip(Math.Max(0, history.Count - 16)))
            {
                var m = new DupoMap();
                m.Set("role", l.Mine ? "user" : "assistant");
                m.Set("content", l.Text);
                messages.Add(m);
            }

            var ask = new DupoMap();
            ask.Set("role", "user");
            ask.Set("content", question);
            messages.Add(ask);

            var body = new DupoMap();
            body.Set("model", o.Model.Trim().Length > 0 ? o.Model.Trim() : "gpt-4o-mini");
            body.Set("messages", messages);
            body.Set("temperature", 0.2);

            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers.Add("Content-Type", "application/json");
                wc.Headers.Add("Authorization", "Bearer " + o.Key.Trim());

                string answer = wc.UploadString(o.Endpoint.Trim(), "POST", JsonTools.Text(body));

                var parsed = JsonTools.Parse(answer) as DupoMap;
                var choices = parsed == null ? null : parsed.Get("choices") as List<object>;
                var first = choices != null && choices.Count > 0 ? choices[0] as DupoMap : null;
                var msg = first == null ? null : first.Get("message") as DupoMap;

                string text = msg == null ? "" : Val.Str(msg.Get("content", ""));
                return text.Trim().Length > 0 ? text : "Сервис ответил пусто.";
            }
        }

        /// <summary>Что помощник рассказывает о себе внешнему сервису.</summary>
        public static string SystemPrompt(string projectContext)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ты помощник в Dupo Studio — среде разработки с собственным языком DupoLang.");
            sb.AppendLine("Отвечай по-русски, коротко и по делу, с примерами кода.");
            sb.AppendLine();
            sb.AppendLine("Язык DupoLang (файлы .dupolang):");
            sb.AppendLine("- подключение модулей: import Dupo.Web или using Dupo.Web — это одно и то же;");
            sb.AppendLine("- объявления: int n = 1, string s = \"текст\", var x = ..., массивы [1, 2, 3];");
            sb.AppendLine("- словари: Data.Map m = new Data.Map(), m[\"имя\"] = 1, либо map(\"имя\", 1);");
            sb.AppendLine("- блоки: func Имя(a, b) { ... } и def Имя(a, b) { ... } — синонимы;");
            sb.AppendLine("- if/else, while, for (int i = 0; i < 10; i = i + 1), break, continue, return;");
            sb.AppendLine("- встроенные: print, println, str, num, int, round, abs, type, len, wait, now,");
            sb.AppendLine("  random, list, push, map, keys, values, has, upper, lower, split.");
            sb.AppendLine();
            sb.AppendLine("Модули: " + string.Join(", ", Modules.Available));
            sb.AppendLine();
            sb.AppendLine("Связка с C#: из C# блок def вызывается так —");
            sb.AppendLine("  using DupoLang;");
            sb.AppendLine("  object r = Dupo.Lang.ExecuteFrom(\"logic.dupolang\", \"ИмяБлока\", арг1, арг2);");
            sb.AppendLine("Рядом есть TextFrom, NumberFrom, TruthFrom, Functions, HasFunction, Arity,");
            sb.AppendLine("Run, Code, Check и Output. Остальной код файла при этом не выполняется.");
            sb.AppendLine("Dupo.CSharp сам подставляет ссылку на DupoLang.dll, если код её зовёт.");
            sb.AppendLine();
            sb.AppendLine("Windows Forms: форма — пара Form1.cs и Form1.Designer.cs.");
            sb.AppendLine("В Designer.cs лежит InitializeComponent с this.имя = new System.Windows.Forms.Тип(),");
            sb.AppendLine("Location, Size, Name, Text, Controls.Add и объявлениями полей внизу.");
            sb.AppendLine("Dupo Studio разбирает и пишет этот файл конструктором, F7 переключает код и конструктор.");
            sb.AppendLine();
            sb.AppendLine("Java: исходники в src, сборка Java.Compile и Java.Jar, JDK ставится Java.Install().");

            if (!string.IsNullOrEmpty(projectContext))
            {
                sb.AppendLine();
                sb.AppendLine("Открытый проект:");
                sb.AppendLine(projectContext);
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // ответы без интернета
        // ------------------------------------------------------------------

        // Собираем лениво: база знаний ссылается на справочник API,
        // который объявлен ниже, — при обычной инициализации он был бы пуст.
        static List<Topic> topics;

        static List<Topic> Topics
        {
            get { return topics ?? (topics = Build()); }
        }

        /// <summary>Ответ по своей базе знаний.</summary>
        public static string Answer(string question, string projectContext)
        {
            string q = (question ?? "").ToLowerInvariant();
            if (q.Trim().Length == 0) return "Спросите что-нибудь — например «как собрать C#» или «что такое ExecuteFrom».";

            var words = q.Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '(', ')', '"', '\'', '\n', '\r', '\t' },
                                StringSplitOptions.RemoveEmptyEntries);

            Topic best = null;
            int bestScore = 0;

            foreach (var t in Topics)
            {
                int score = 0;
                foreach (string key in t.Keys)
                {
                    if (q.IndexOf(key, StringComparison.Ordinal) >= 0) score += 2;
                    else if (words.Any(w => w.StartsWith(key, StringComparison.Ordinal) ||
                                            key.StartsWith(w, StringComparison.Ordinal) && w.Length > 3)) score += 1;
                }
                if (score > bestScore) { bestScore = score; best = t; }
            }

            if (best != null && bestScore >= 2)
                return "**" + best.Title + "**" + Environment.NewLine + Environment.NewLine + best.Answer;

            // Не нашли тему — может, спрашивают про конкретное имя из API.
            string found = Lookup(question);
            if (found != null) return found;

            return Overview(projectContext);
        }

        /// <summary>Поиск по именам API.</summary>
        static string Lookup(string question)
        {
            string q = (question ?? "").Trim();
            if (q.Length < 3) return null;

            var hits = new List<string>();
            foreach (var pair in Api)
                foreach (string member in pair.Value)
                    if (member.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        q.IndexOf(member, StringComparison.OrdinalIgnoreCase) >= 0 && member.Length > 3)
                        hits.Add(pair.Key + "." + member);

            if (hits.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("**Нашлось в API**");
            sb.AppendLine();
            foreach (string h in hits.Distinct().Take(20)) sb.AppendLine("• " + h);
            sb.AppendLine();
            sb.AppendLine("Напишите «что такое " + hits[0] + "» или название модуля, расскажу подробнее.");
            return sb.ToString();
        }

        static string Overview(string projectContext)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Чем могу помочь**");
            sb.AppendLine();
            sb.AppendLine("Я знаю язык DupoLang, C# вместе с Designer.cs, Java и связку между ними.");
            sb.AppendLine("Спросите, например:");
            sb.AppendLine();
            sb.AppendLine("• как вызвать def из C#");
            sb.AppendLine("• что такое Web.Server");
            sb.AppendLine("• как собрать C# или C++");
            sb.AppendLine("• как сделать службу");
            sb.AppendLine("• как устроен Designer.cs");
            sb.AppendLine("• как добавить ресурсы");
            sb.AppendLine("• список команд");
            sb.AppendLine();
            sb.AppendLine("Если задать адрес и ключ в «Настроить сервис», буду отвечать через него,");
            sb.AppendLine("а без интернета — по своей базе знаний.");

            if (!string.IsNullOrEmpty(projectContext))
            {
                sb.AppendLine();
                sb.AppendLine("**Открытый проект**");
                sb.AppendLine();
                sb.Append(projectContext);
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // справочник API
        // ------------------------------------------------------------------

        /// <summary>Модуль → его члены. Источник для поиска и для списка команд.</summary>
        public static readonly Dictionary<string, string[]> Api =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Web", new[] { "Server", "Server.Php", "Open", "Get", "Download",
                             "Start", "Stop", "Url", "Port", "Root", "Running", "SetIndex" } },
            { "Files", new[] { "Create", "Write", "Append", "AppendLine", "Read", "ReadLines",
                               "ReplaceIn", "Contains", "Exists", "Delete", "Copy", "Move", "Rename",
                               "Size", "Modified", "Name", "NameOnly", "Extension", "Folder", "Full",
                               "Join", "Temp", "MakeDir", "DirExists", "DeleteDir", "CopyDir",
                               "List", "Dirs", "Find", "Tree", "Count", "Zip", "Unzip", "Open",
                               "ProjectDir", "AppData", "RoamingData", "Desktop", "StartMenu",
                               "ProgramFiles", "Documents", "Shortcut" } },
            { "Java", new[] { "Install", "Uninstall", "IsInstalled", "Version", "Folder",
                              "Compile", "Jar", "Run", "RunJar", "Build", "New" } },
            { "CSharp", new[] { "Build", "Forms", "Library", "Compile", "Run", "Launch",
                                "IsInstalled", "Version", "Folder", "Install", "CscPath" } },
            { "Cpp", new[] { "Install", "Uninstall", "Build", "Window", "Compile", "Run",
                             "Launch", "IsInstalled", "Version", "Folder", "GppPath" } },
            { "Services", new[] { "Service", "ServiceLevel", "Exists", "State", "List", "Levels",
                                  "LevelName", "IsAdmin", "Install", "Uninstall", "Start", "Stop",
                                  "Restart", "Installed", "Running", "NeedsAdmin", "Info", "Program" } },
            { "Lang", new[] { "ExecuteFrom", "TextFrom", "NumberFrom", "TruthFrom", "Functions",
                              "HasFunction", "Arity", "Run", "Code", "Check", "Output", "Reset", "Version" } },
            { "Json", new[] { "Parse", "Read", "Text", "Pretty", "Save" } },
            { "Text", new[] { "Trim", "Upper", "Lower", "Replace", "Contains", "StartsWith",
                              "EndsWith", "IndexOf", "Cut", "Left", "Right", "Split", "Lines",
                              "Words", "Join", "Repeat", "Reverse", "PadLeft", "PadRight", "Capital",
                              "CountOf", "Format", "Fill", "Match", "Find", "ReplaceAll",
                              "ToNumber", "Round", "ToBase64", "FromBase64", "UrlEncode", "UrlDecode" } },
            { "Data", new[] { "Map", "Get", "Set", "Has", "Remove", "Clear", "Copy", "Keys", "Values", "Count" } },
            { "Resources", new[] { "Folder", "Count", "Text", "Lines", "Size", "FileName", "Extension", "Path" } },
            { "Tools", new[] { "Install", "Reinstall", "Uninstall", "IsInstalled", "Version",
                               "Folder", "Build", "ToCSharp", "Info" } },
            { "App", new[] { "Export", "Import", "Unpack", "Info", "Files", "Package" } },
            { "IO", new[] { "ReadFile", "WriteFile", "AppendFile", "Exists", "MakeDir",
                            "Delete", "Files", "Dirs", "ProjectDir" } },
            { "Sys", new[] { "Run", "Open", "Env", "Sleep", "Time", "Date" } },
        };

        /// <summary>Полный список команд по модулям.</summary>
        public static string Commands()
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Все команды по модулям**");
            sb.AppendLine();
            foreach (var pair in Api)
            {
                sb.AppendLine("**" + pair.Key + "** — " + string.Join(", ", pair.Value));
                sb.AppendLine();
            }
            sb.AppendLine("Встроенные функции: print, println, str, num, int, round, abs, type, len,");
            sb.AppendLine("wait, now, random, list, push, map, keys, values, has, upper, lower, split.");
            sb.AppendLine();
            sb.AppendLine("Модули для import/using: " + string.Join(", ", Modules.Available));
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // база знаний
        // ------------------------------------------------------------------

        static List<Topic> Build()
        {
            var t = new List<Topic>();

            t.Add(new Topic("Вызов def из C#",
                "executefrom def блок вызвать c# cs из csharp интеграц связк мост dupo.lang",
@"В файле `.dupolang` объявляете блок:

```
def Привет(имя) {
    return ""Привет, "" + имя + ""!""
}
```

Из C# зовёте его так:

```csharp
using DupoLang;

object r = Dupo.Lang.ExecuteFrom(""logic.dupolang"", ""Привет"", ""Лилия"");
string s = Dupo.Lang.TextFrom(""logic.dupolang"", ""Привет"", ""Лилия"");
double n = Dupo.Lang.NumberFrom(""logic.dupolang"", ""Сумма"", 2, 3);
```

Остальной код файла при этом не выполняется: подключаются модули,
объявляются блоки, запускается только названный. Рядом есть
`TruthFrom`, `Functions`, `HasFunction`, `Arity`, `Run`, `Code`,
`Check`, `Output` и `Reset`.

Ссылку на `DupoLang.dll` подставлять вручную не надо — `Dupo.CSharp`
видит обращение к библиотеке, добавляет её сам и кладёт рядом с exe."));

            t.Add(new Topic("def и func",
                "def func функц блок объявить метод процедур",
@"`def` и `func` — одно и то же, пишите как привычнее:

```
def Сумма(a, b) { return a + b }
func Сумма2(a, b) { return a + b }
```

Параметры без типов, возвращаемое значение — любое.
Блоки видны друг другу и объявляются до запуска кода,
поэтому порядок в файле не важен."));

            t.Add(new Topic("Устройство Designer.cs",
                "designer.cs designer конструктор initializecomponent winforms форм разметк",
@"Форма Windows Forms — это две половины:

* `Form1.Designer.cs` — разметка. Внутри `InitializeComponent()`:
  создание `this.имя = new System.Windows.Forms.Тип();`, затем
  `Location`, `Name`, `Size`, `TabIndex`, `Text`, потом
  `this.Controls.Add(this.имя);`, а в конце файла — объявления полей.
* `Form1.cs` — ваш код: конструктор с `InitializeComponent()`
  и обработчики вида `button1_Click`.

Студия разбирает и пишет `Designer.cs` конструктором. **F7**
переключает код и конструктор, двойной щелчок по кнопке создаёт
обработчик и открывает `Form1.cs`. Форма целиком правится и мышью,
и руками — файл остаётся обычным C#."));

            t.Add(new Topic("Создать форму Windows Forms",
                "созда нов форм winforms окно кнопк добавить элемент тулбокс",
@"Проект → **Новая форма Windows Forms...** — появляется пара
`Form1.cs` и `Form1.Designer.cs`, а если точки входа ещё нет, то и
`Program.cs`.

Дальше: выбираете элемент в **Панели элементов** и щёлкаете по форме.
Двигать — мышью, менять свойства — в **Окне свойств**, удалять —
Delete. Ctrl+S пишет изменения прямо в `Designer.cs`.

Галочка «Нажатие (Click)» в свойствах создаёт обработчик
`имя_Click` в `Form1.cs`."));

            t.Add(new Topic("Сборка C#",
                "csharp c# собрать компил csc exe библиотек dll",
@"```
using Dupo.CSharp

print(CSharp.Version)

string exe = CSharp.Build(""src"", ""build/App.exe"")     // консоль
string win = CSharp.Forms(""src"", ""build/App.exe"")     // WinForms, без консоли
string dll = CSharp.Library(""src"", ""build/App.dll"")   // библиотека

CSharp.Launch(exe, """")
```

Компилятор `csc.exe` входит в .NET Framework 4.8, который есть на
любой Windows, — ставить ничего не нужно. Папка берётся со всеми
вложенными `.cs`, кроме `bin` и `obj`."));

            t.Add(new Topic("Сборка C++",
                "cpp c++ g++ mingw компил собрать окно winapi",
@"```
using Dupo.Cpp

if (Cpp.IsInstalled == false) { Cpp.Install() }

string exe = Cpp.Build(""src"", ""build/App.exe"")      // консоль
string win = Cpp.Window(""src"", ""build/App.exe"")     // окно, без консоли
Cpp.Launch(exe, """")
```

`Cpp.Install()` скачивает MinGW-w64 в `runtime\mingw`. Сборка идёт
с `-std=c++17 -O2 -static`, поэтому получается один exe без лишних dll."));

            t.Add(new Topic("Java",
                "java jar jdk javac компил класс",
@"```
using Dupo.Java

if (Java.IsInstalled == false) { Java.Install() }

Java.Compile(""src"", ""build/classes"")
string jar = Java.Jar(""build/classes"", ""build/App.jar"", ""Main"")
Java.RunJar(jar)
```

`Java.Install()` скачивает Temurin JDK в `runtime\jdk`. Пакет Java
для проекта задаётся в свойствах проекта (Alt+Enter) и попадает
в шаблоны классов."));

            t.Add(new Topic("Веб-сервер",
                "web server сервер сайт php html порт index nginx",
@"```
using Dupo.Web

Web.Server site = new Web.Server(8080, ""www"", ""index"")

if (Web.Server.Php.IsInstalled == false) { Web.Server.Php.Install() }

site.Start()
Web.Open(site.Url)
```

Третий параметр — имя главного файла **без расширения**. Если в адресе
указана папка, сервер отдаёт первый найденный `index.html`, `index.php`
или `index.js` — как nginx, а не ошибку 404. PHP работает через
`php-cgi.exe` по CGI."));

            t.Add(new Topic("Службы и сервисы",
                "сервис служб service servicelevel фонов автозапуск sc агент",
@"```
using Dupo.Services

Services.Service agent = new Services.Service(
    ""MyAgent"", ""описание"", Services.ServiceLevel.Background)

agent.Program(""worker.bat"")
agent.Install()
agent.Start()
print(agent.State)
```

Типы: `SystemProcess`, `LocalService`, `NetworkService`, `Delayed`,
`Manual`, `Interactive`, `Disabled`, `UserProcess`, `Background`,
`Process`. Службы системы ставятся через `sc.exe` и требуют
администратора; `Background` и `UserProcess` прописываются
в автозапуск пользователя и прав не требуют.

У самой студии тоже есть фоновый сервис: Инструменты → Фоновый сервис."));

            t.Add(new Topic("Ресурсы проекта",
                "ресурс resources картинк файл setimage добавить",
@"Файлы кладутся в папку `Resources` проекта — через свойства
проекта (Alt+Enter) или вручную. Дальше:

```
using Dupo.Resources

print(Resources.Folder)
print(Resources.readme.Text)
print(Resources.readme.Size)
```

После точки студия подсказывает имена добавленных файлов. Ресурс сам
приводится к пути, поэтому его можно передать куда угодно:
`Files.Copy(Resources.readme, ""копия.txt"")`."));

            t.Add(new Topic("Словари, JSON и текст",
                "словар map json текст text строк parse pretty",
@"```
using Dupo.Data
using Dupo.Json
using Dupo.Text

Data.Map m = new Data.Map()
m[""имя""] = ""Акварелька""
print(m[""имя""])

var short = map(""a"", 1, ""b"", 2)
print(keys(short))

string text = Json.Pretty(m)
var back = Json.Parse(text)

print(Text.Upper(Text.Trim(""  тише едешь  "")))
print(Text.Fill(""{имя} — молодец"", m))
```

`len()`, `keys()`, `values()` и `has()` работают и со словарями."));

            t.Add(new Topic("Сборка в один exe",
                "exe собрать один файл dapp build dupobuild упаков",
@"Запуск → **Собрать в один .exe**: проект упаковывается в `.dapp`
и дописывается в хвост рантайма, получается один исполняемый файл.
Галочка «Собирать в один exe» живёт в свойствах проекта (Alt+Enter).

Из командной строки то же делает `DupoBuild.exe`:

```
DupoBuild.exe <папка> [итог.exe] name=Имя version=1.0 author=Вы console=off
DupoBuild.exe --info app.exe
```

А `DupoLang.exe` переводит `.dupolang` в C#:

```
DupoLang.exe файл.dupolang [итог.cs] namespace=App class=Program
DupoLang.exe --check файл.dupolang
```"));

            t.Add(new Topic("Установщик приложения",
                "установщик setup installer payload ярлык инсталл",
@"Создайте проект по шаблону **Установщик приложения**. Файлы
приложения кладутся в папку `payload`, а собранный `.exe` ставит их
пользователю:

```
using Dupo.System.Files

string target = Files.Join(Files.AppData(), ""МоёПриложение"")
Files.CopyDir(""payload"", target)
Files.Shortcut(Files.Join(target, ""App.exe""), Files.Join(Files.Desktop(), ""Моё""))
```

Системные папки: `AppData`, `RoamingData`, `Desktop`, `StartMenu`,
`ProgramFiles`, `Documents`."));

            t.Add(new Topic("Основы языка",
                "синтакс основы переменн цикл услов массив if while for",
@"```
int n = 5
string s = ""текст""
var любое = [1, 2, 3]

if (n > 3) {
    print(""больше"")
} else {
    print(""меньше"")
}

for (int i = 0; i < 3; i = i + 1) { print(i) }

while (n > 0) { n = n - 1 }

def Удвоить(x) { return x * 2 }
print(Удвоить(21))
```

`import` и `using` равноправны. Комментарии — `//`, `#` и `/* ... */`.
Имена могут быть по-русски."));

            t.Add(new Topic("Панели и окна студии",
                "панел докинг отцепить вид solution обозреватель свойств вывод окно",
@"Панели живут в зонах слева, справа и снизу. В заголовке каждой —
кнопки «отцепить в окно» и «спрятать»; окно возвращается на место,
когда его закрывают. Меню **Вид** включает и выключает
Обозреватель решений, Окно свойств, Панель элементов, Вывод
и Помощника, а «Вернуть все окна на место» собирает отцепленные обратно.

Горячие клавиши: F5 — запуск, Shift+F5 — остановка, F7 — конструктор
и код, Alt+Enter — свойства проекта, Ctrl+N — новый проект,
Ctrl+S — сохранить, Ctrl+Space — подсказки."));

            t.Add(new Topic("Свойства проекта",
                "свойств проект namespace пакет автор версия alt+enter",
@"Alt+Enter или Проект → Свойства проекта. Задаются название, продукт,
автор, версия, описание, пространство имён для C#, пакет для Java
и галочка «Собирать в один exe». Там же добавляются ресурсы.

Всё это лежит в файле `project.dupo` в папке проекта и попадает
в манифест собранного приложения."));

            t.Add(new Topic("Типы проектов",
                "тип проект шаблон новый создать консол библиотек",
@"Ctrl+N открывает окно **Новый проект**: разделы слева, шаблоны
в середине, описание справа.

* **DupoLang** — веб-приложение, консоль, фоновый сервис
* **C#** — консоль, Windows Forms, библиотека `.dll`
* **C++** — консоль, окно на WinAPI
* **Java** — приложение с `.jar`
* **Установщик** — установщик приложения с папкой `payload`"));

            t.Add(new Topic("Фоновый сервис студии",
                "агент agent фонов очеред журнал собрать в фоне",
@"`DupoStudio.exe --agent` поднимает фоновый сервис студии: он берёт
задания из очереди и выполняет их, даже когда студия закрыта.

Меню Инструменты → **Фоновый сервис**: установить, запустить,
остановить, состояние с журналом и «Собрать проект в фоне».
Очередь и журнал лежат в `%LOCALAPPDATA%\DupoStudio\agent`.

Ключи: `--agent`, `--agent --once`, `--agent --stop`, `--agent --status`."));

            t.Add(new Topic("Подсказки в редакторе",
                "подсказк автодополнен intellisense ctrl+space",
@"Подсказки появляются после точки за известным модулем и по
**Ctrl+Space**. Они знают члены всех модулей, имена файлов из папки
`Resources`, а для переменных смотрят, каким типом те объявлены:
после `Services.Service agent = ...` по `agent.` покажутся члены
сервиса."));

            t.Add(new Topic("Список команд", "команд список api справочник всё что умеет", Commands()));

            return t;
        }
    }
}
