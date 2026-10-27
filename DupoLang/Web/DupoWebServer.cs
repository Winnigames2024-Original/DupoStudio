using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace DupoLang
{
    /// <summary>
    /// Web.Server из модуля Dupo.Web — статика + PHP через CGI.
    /// new Web.Server(8080, "www", "index")  — имя главного файла без расширения.
    /// </summary>
    public class DupoWebServer : IDisposable
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };
        public static int LastPort = 8080;

        public int Port { get; private set; }
        public string Root { get; private set; }

        /// <summary>Как главный файл задали в конструкторе — "index" или "index.html".</summary>
        public string MainFile { get; private set; }

        /// <summary>Имя главного файла без расширения — обычно "index".</summary>
        public string IndexName { get; private set; }

        /// <summary>Расширения, которые ищутся по порядку: index.html, index.php, index.js...</summary>
        public string[] IndexExtensions = { ".html", ".htm", ".php", ".js", ".json", ".txt", ".xml" };

        /// <summary>Отдавать корневой index вместо 404 — для приложений на JavaScript.</summary>
        public bool Fallback;

        /// <summary>
        /// Своя страница для ненайденного пути — как error_page в nginx.
        /// Пусто: ищем 404.html (или 404.php и остальные расширения) в корне
        /// сайта, и только если его нет, показываем встроенную страницу.
        /// </summary>
        public string ErrorPage = "";

        public bool Running { get; private set; }
        public string Url
        {
            get
            {
                // Если в своём файле имён на этот сервер заведено имя,
                // показываем его: ссылка получается человеческой.
                string name = HostName;
                return "http://" + (name.Length > 0 ? name : "localhost") + ":" + Port + "/";
            }
        }

        /// <summary>Имя из файла dupo-Dupo\hosts.txt, указывающее на этот сервер.</summary>
        public string HostName
        {
            get
            {
                try
                {
                    foreach (object line in DupoHosts.List())
                    {
                        var parts = Val.Str(line).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;

                        string address = parts[0];
                        if (address == "127.0.0.1" || address == "localhost" || address == "::1")
                            return parts[1];
                    }
                }
                catch { }
                return "";
            }
        }

        HttpListener listener;
        Thread worker;

        /// <summary>Доступ к PHP: Web.Server.Php.Install()</summary>
        public static PhpTools Php { get { return PhpTools.Instance; } }

        public DupoWebServer(int port) : this(port, "www", "index") { }
        public DupoWebServer(int port, string root) : this(port, root, "index") { }

        public DupoWebServer(int port, string root, string mainFile)
        {
            Port = port <= 0 ? 8080 : port;
            Root = DupoRuntime.Resolve(string.IsNullOrEmpty(root) ? "www" : root);
            SetIndex(string.IsNullOrEmpty(mainFile) ? "index" : mainFile);
            LastPort = Port;
        }

        /// <summary>
        /// Задаёт главный файл. Можно именем без расширения ("index") — тогда
        /// перебираются все известные расширения, — или с расширением
        /// ("index.php"), и тогда оно становится первым в очереди.
        /// Принимает и список: "index.php, index.html".
        /// </summary>
        public void SetIndex(string value)
        {
            if (string.IsNullOrEmpty(value)) value = "index";
            MainFile = value;

            var order = new List<string>();
            string name = null;

            foreach (string raw in value.Split(',', ';', ' '))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;

                if (token[0] == '.')
                {
                    if (!order.Contains(token)) order.Add(token.ToLowerInvariant());
                    continue;
                }

                string ext = Path.GetExtension(token);
                if (name == null) name = Path.GetFileNameWithoutExtension(token);
                if (ext.Length > 0 && !order.Contains(ext)) order.Add(ext.ToLowerInvariant());
            }

            IndexName = string.IsNullOrEmpty(name) ? "index" : name;

            // то, что назвали явно, идёт первым; остальное — следом
            foreach (string ext in IndexExtensions)
                if (!order.Contains(ext)) order.Add(ext);

            IndexExtensions = order.ToArray();
        }

        /// <summary>Перечень имён, которые сервер ищет в папке.</summary>
        public string IndexFiles
        {
            get
            {
                var names = new List<string>();
                foreach (string ext in IndexExtensions) names.Add(IndexName + ext);
                return string.Join(", ", names.ToArray());
            }
        }

        public void Start()
        {
            if (Running) { Log("Сервер уже работает: " + Url); return; }

            if (!Directory.Exists(Root))
            {
                Directory.CreateDirectory(Root);
                Log("Создана папка сайта: " + Root);
            }

            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:" + Port + "/");
            listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");

            // Имена из своего файла dupo-Dupo\hosts.txt, которые смотрят
            // на эту машину, сервер слушает тоже — как будто их прописали
            // в системном HOSTS, только систему мы не трогаем.
            foreach (string name in LocalNames())
            {
                try { listener.Prefixes.Add("http://" + name + ":" + Port + "/"); }
                catch { }
            }

            try { listener.Start(); }
            catch (Exception ex)
            {
                listener = null;
                throw new Exception("Не удалось занять порт " + Port + ". " + ex.Message);
            }

            Running = true;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Start();

            Log("Сервер запущен: " + Url);
            Log("Папка: " + Root);
            Log("Главный файл: " + IndexName + ".*  →  " + IndexFiles);

            var names = LocalNames();
            if (names.Count > 0)
                Log("Имена из " + DupoHosts.FileName + ": " + string.Join(", ", names.ToArray()));
            if (PhpTools.Instance.IsInstalled) Log("PHP включён: " + PhpTools.Instance.Version);
        }

        public void Stop() { Dispose(); }

        public void Dispose()
        {
            if (!Running && listener == null) return;
            Running = false;
            try { if (listener != null) listener.Stop(); } catch { }
            try { if (listener != null) listener.Close(); } catch { }
            listener = null;
            Log("Сервер на порту " + Port + " остановлен.");
        }

        void Loop()
        {
            while (Running)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(delegate (object o) { SafeHandle((HttpListenerContext)o); }, ctx);
            }
        }

        /// <summary>Имена из своего файла, указывающие на эту машину.</summary>
        List<string> LocalNames()
        {
            var res = new List<string>();
            try
            {
                foreach (object line in DupoHosts.List())
                {
                    var parts = Val.Str(line).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    string address = parts[0];
                    if (address != "127.0.0.1" && address != "localhost" && address != "::1") continue;

                    for (int i = 1; i < parts.Length; i++)
                        if (!res.Contains(parts[i])) res.Add(parts[i]);
                }
            }
            catch { }
            return res;
        }

        void SafeHandle(HttpListenerContext ctx)
        {
            try { Handle(ctx); }
            catch (Exception ex)
            {
                Log("Ошибка запроса: " + ex.Message);
                try { SendError(ctx, 500, "Ошибка сервера: " + ex.Message); } catch { }
            }
            finally { try { ctx.Response.Close(); } catch { } }
        }

        void Handle(HttpListenerContext ctx)
        {
            string urlPath = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath);
            string rel = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

            string full = Path.GetFullPath(string.IsNullOrEmpty(rel) ? Root : Path.Combine(Root, rel));
            string rootFull = Path.GetFullPath(Root);

            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                Log(ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery + "  → 403");
                SendError(ctx, 403, "Выход за пределы папки сайта запрещён");
                return;
            }

            // Папка без завершающей косой черты: перенаправляем, иначе ссылки
            // вроде href="style.css" внутри страницы будут считаться от родителя.
            if (Directory.Exists(full) && rel.Length > 0 && !urlPath.EndsWith("/"))
            {
                ctx.Response.StatusCode = 301;
                ctx.Response.RedirectLocation = urlPath + "/" + ctx.Request.Url.Query;
                Log(ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery + "  → 301 " + urlPath + "/");
                return;
            }

            string file = Resolve(full);

            if (file == null && Fallback) file = FindIndex(rootFull);

            Log(ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery +
                (file == null ? "  → 404" : "  → " + Short(file)));

            if (file == null)
            {
                SendMissing(ctx, urlPath);
                return;
            }

            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".php") ServePhp(ctx, file);
            else SendFile(ctx, file, ext);
        }

        /// <summary>
        /// Ищет, что отдать по пути: сам файл, index в папке или файл
        /// с дописанным расширением. null — отдавать нечего.
        /// </summary>
        string Resolve(string full)
        {
            if (Directory.Exists(full)) return FindIndex(full);
            if (File.Exists(full)) return full;

            // /about → about.html, about.php, about.js...
            if (Path.GetExtension(full).Length == 0)
            {
                foreach (string ext in IndexExtensions)
                {
                    string candidate = full + ext;
                    if (File.Exists(candidate)) return candidate;
                }
            }

            return null;
        }

        /// <summary>Первый существующий index.* в папке.</summary>
        string FindIndex(string dir)
        {
            foreach (string ext in IndexExtensions)
            {
                string candidate = Path.Combine(dir, IndexName + ext);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        string Short(string file)
        {
            string rootFull = Path.GetFullPath(Root);
            return file.Length > rootFull.Length
                ? file.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/')
                : Path.GetFileName(file);
        }

        // ---------- статика ----------

        static readonly Dictionary<string, string> Mime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" }, { ".htm", "text/html; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },   { ".js", "application/javascript; charset=utf-8" },
            { ".mjs", "application/javascript; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" },
            { ".txt", "text/plain; charset=utf-8" }, { ".xml", "application/xml; charset=utf-8" },
            { ".svg", "image/svg+xml" }, { ".png", "image/png" }, { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" }, { ".gif", "image/gif" }, { ".webp", "image/webp" },
            { ".ico", "image/x-icon" }, { ".mp3", "audio/mpeg" }, { ".mp4", "video/mp4" },
            { ".wav", "audio/wav" }, { ".ogg", "audio/ogg" }, { ".woff", "font/woff" },
            { ".woff2", "font/woff2" }, { ".ttf", "font/ttf" }, { ".pdf", "application/pdf" },
            { ".zip", "application/zip" }, { ".wasm", "application/wasm" }
        };

        void SendFile(HttpListenerContext ctx, string file, string ext)
        {
            SendFile(ctx, file, ext, 200);
        }

        void SendFile(HttpListenerContext ctx, string file, string ext, int code)
        {
            string type;
            if (!Mime.TryGetValue(ext, out type)) type = "application/octet-stream";

            byte[] data = File.ReadAllBytes(file);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = type;
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
        }

        /// <summary>Встроенная страница ошибки — её видно, пока своей нет.</summary>
        static string ErrorHtml(int code, string message)
        {
            string title = Title(code);
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.Append("<title>").Append(code).Append(" — ").Append(title).Append("</title><style>");
            sb.Append("*{box-sizing:border-box}");
            sb.Append("body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;");
            sb.Append("padding:24px;font:15px/1.6 'Segoe UI',system-ui,sans-serif;color:#e6eaf2;");
            sb.Append("background:#0f1218;background-image:radial-gradient(900px 600px at 15% -10%,");
            sb.Append("rgba(59,130,246,.18),transparent 70%)}");
            sb.Append(".box{width:100%;max-width:560px;background:#161a23;border:1px solid #262c3a;");
            sb.Append("border-radius:14px;padding:38px 36px}");
            sb.Append(".num{font:600 76px/1 'Segoe UI',sans-serif;letter-spacing:-2px;margin:0;");
            sb.Append("background:linear-gradient(135deg,#3b82f6,#22c7a9);-webkit-background-clip:text;");
            sb.Append("background-clip:text;color:transparent}");
            sb.Append("h1{font:600 20px/1.3 'Segoe UI',sans-serif;margin:12px 0 0}");
            sb.Append("p{color:#8d97aa;margin:14px 0 0;white-space:pre-wrap}");
            sb.Append("a{display:inline-block;margin-top:26px;padding:10px 18px;border-radius:8px;");
            sb.Append("background:#3b82f6;color:#fff;text-decoration:none;font-weight:600}");
            sb.Append("a:hover{background:#2f6fd8}");
            sb.Append(".foot{margin-top:28px;padding-top:18px;border-top:1px solid #262c3a;");
            sb.Append("color:#5a6478;font-size:12.5px}");
            sb.Append(".foot code{color:#8d97aa}");
            sb.Append("</style></head><body><div class=\"box\">");

            sb.Append("<p class=\"num\">").Append(code).Append("</p>");
            sb.Append("<h1>").Append(title).Append("</h1>");
            sb.Append("<p>").Append(WebUtility.HtmlEncode(message ?? "")).Append("</p>");
            sb.Append("<a href=\"/\">На главную</a>");

            sb.Append("<div class=\"foot\">");
            if (code == 404)
                sb.Append("Чтобы заменить эту страницу своей, положите <code>404.html</code> ")
                  .Append("в папку сайта — сервер отдаст её сам.<br>");
            sb.Append("DupoLang Web.Server</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        /// <summary>Человеческое название кода ошибки.</summary>
        static string Title(int code)
        {
            switch (code)
            {
                case 400: return "Запрос не разобрался";
                case 403: return "Доступа нет";
                case 404: return "Страница не найдена";
                case 405: return "Такой способ запроса не поддерживается";
                case 500: return "Сервер не справился";
                case 501: return "Так сервер не умеет";
                case 503: return "Сервер занят";
                default: return code >= 500 ? "Ошибка сервера" : "Запрос не выполнен";
            }
        }

        /// <summary>
        /// Ничего не нашлось. Если у сайта есть своя страница 404 — отдаём её
        /// (с правильным кодом 404, а не 200), иначе встроенную.
        /// </summary>
        void SendMissing(HttpListenerContext ctx, string urlPath)
        {
            string page = NotFoundPage();

            if (page != null)
            {
                string ext = Path.GetExtension(page).ToLowerInvariant();

                // Код именно 404: страница своя, но поисковики и клиенты
                // должны понимать, что ничего не нашлось.
                if (ext == ".php")
                {
                    ctx.Response.StatusCode = 404;
                    ServePhp(ctx, page);
                }
                else SendFile(ctx, page, ext, 404);

                return;
            }

            SendError(ctx, 404, "Не найдено: " + urlPath +
                "\nВ папке нет ни одного из файлов: " + IndexFiles);
        }

        /// <summary>Своя страница 404, если она есть: из ErrorPage или 404.* в корне.</summary>
        string NotFoundPage()
        {
            try
            {
                string rootFull = Path.GetFullPath(Root);

                // Путь задали руками — он может быть и относительным от корня.
                if (!string.IsNullOrEmpty(ErrorPage))
                {
                    string own = Path.IsPathRooted(ErrorPage)
                        ? ErrorPage
                        : Path.Combine(rootFull, ErrorPage.TrimStart('/', '\\'));

                    return File.Exists(own) ? own : null;
                }

                // Иначе ищем 404.html, 404.php и так далее — как index.
                foreach (string ext in IndexExtensions)
                {
                    string candidate = Path.Combine(rootFull, "404" + ext);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            return null;
        }

        void SendError(HttpListenerContext ctx, int code, string message)
        {
            string html = ErrorHtml(code, message);
            byte[] data = Encoding.UTF8.GetBytes(html);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
        }

        // ---------- PHP через CGI ----------

        void ServePhp(HttpListenerContext ctx, string file)
        {
            string cgi = PhpTools.CgiPath;
            if (!File.Exists(cgi))
            {
                SendError(ctx, 500, "PHP не установлен. Выполните Web.Server.Php.Install() или кнопку «Установить PHP».");
                return;
            }

            byte[] body = new byte[0];
            if (ctx.Request.HasEntityBody)
            {
                using (var ms = new MemoryStream())
                {
                    ctx.Request.InputStream.CopyTo(ms);
                    body = ms.ToArray();
                }
            }

            var psi = new ProcessStartInfo(cgi);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = Path.GetDirectoryName(file);

            var ev = psi.EnvironmentVariables;
            ev["REDIRECT_STATUS"] = "200";
            ev["GATEWAY_INTERFACE"] = "CGI/1.1";
            ev["SERVER_PROTOCOL"] = "HTTP/1.1";
            ev["SERVER_SOFTWARE"] = "DupoWebServer/1.0";
            ev["SERVER_NAME"] = "localhost";
            ev["SERVER_PORT"] = Port.ToString();
            ev["DOCUMENT_ROOT"] = Root;
            ev["SCRIPT_FILENAME"] = file;
            ev["SCRIPT_NAME"] = ctx.Request.Url.AbsolutePath;
            ev["REQUEST_URI"] = ctx.Request.Url.PathAndQuery;
            ev["REQUEST_METHOD"] = ctx.Request.HttpMethod;
            ev["QUERY_STRING"] = ctx.Request.Url.Query.TrimStart('?');
            ev["CONTENT_TYPE"] = ctx.Request.ContentType ?? "";
            ev["CONTENT_LENGTH"] = body.Length.ToString();
            ev["REMOTE_ADDR"] = ctx.Request.RemoteEndPoint == null
                ? "127.0.0.1" : ctx.Request.RemoteEndPoint.Address.ToString();

            foreach (string h in ctx.Request.Headers.AllKeys)
            {
                if (h == null) continue;
                string key = "HTTP_" + h.ToUpperInvariant().Replace("-", "_");
                try { ev[key] = ctx.Request.Headers[h]; } catch { }
            }

            using (var proc = Process.Start(psi))
            {
                string stderrText = "";
                var errThread = new Thread(delegate () {
                    try { stderrText = proc.StandardError.ReadToEnd(); } catch { }
                });
                errThread.IsBackground = true;
                errThread.Start();

                try
                {
                    if (body.Length > 0) proc.StandardInput.BaseStream.Write(body, 0, body.Length);
                    proc.StandardInput.BaseStream.Flush();
                }
                catch { }
                try { proc.StandardInput.Close(); } catch { }

                byte[] output;
                using (var ms = new MemoryStream())
                {
                    proc.StandardOutput.BaseStream.CopyTo(ms);
                    output = ms.ToArray();
                }

                proc.WaitForExit(60000);
                errThread.Join(1000);

                if (output.Length == 0)
                {
                    SendError(ctx, 500, "PHP ничего не вернул. " + stderrText);
                    return;
                }

                WriteCgiResponse(ctx, output);
                if (!string.IsNullOrEmpty(stderrText)) Log("PHP stderr: " + stderrText.Trim());
            }
        }

        void WriteCgiResponse(HttpListenerContext ctx, byte[] data)
        {
            int split = -1, skip = 0;
            for (int k = 0; k + 1 < data.Length; k++)
            {
                if (k + 3 < data.Length && data[k] == 13 && data[k + 1] == 10 && data[k + 2] == 13 && data[k + 3] == 10)
                { split = k; skip = 4; break; }
                if (data[k] == 10 && data[k + 1] == 10)
                { split = k; skip = 2; break; }
            }

            byte[] bodyBytes;
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html; charset=utf-8";

            if (split < 0)
            {
                bodyBytes = data;
            }
            else
            {
                string headerText = Encoding.UTF8.GetString(data, 0, split);
                bodyBytes = new byte[data.Length - split - skip];
                Array.Copy(data, split + skip, bodyBytes, 0, bodyBytes.Length);

                foreach (string raw in headerText.Split('\n'))
                {
                    string lineText = raw.Trim('\r', ' ');
                    if (lineText.Length == 0) continue;
                    int colon = lineText.IndexOf(':');
                    if (colon <= 0) continue;

                    string name = lineText.Substring(0, colon).Trim();
                    string value = lineText.Substring(colon + 1).Trim();

                    if (string.Equals(name, "Status", StringComparison.OrdinalIgnoreCase))
                    {
                        int code;
                        if (int.TryParse(value.Split(' ')[0], out code)) ctx.Response.StatusCode = code;
                    }
                    else if (string.Equals(name, "Content-type", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.ContentType = value;
                    }
                    else if (string.Equals(name, "Location", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 302;
                        ctx.Response.RedirectLocation = value;
                    }
                    else if (!string.Equals(name, "Content-length", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(name, "Transfer-encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        try { ctx.Response.Headers.Add(name, value); } catch { }
                    }
                }
            }

            ctx.Response.ContentLength64 = bodyBytes.Length;
            ctx.Response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
        }
    }
}
