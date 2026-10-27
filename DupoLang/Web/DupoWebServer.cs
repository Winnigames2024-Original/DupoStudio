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
    /// new Web.Server(8080, "www", "index.html")
    /// </summary>
    public class DupoWebServer : IDisposable
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };
        public static int LastPort = 8080;

        public int Port { get; private set; }
        public string Root { get; private set; }
        public string MainFile { get; private set; }
        public bool Running { get; private set; }
        public string Url { get { return "http://localhost:" + Port + "/"; } }

        HttpListener listener;
        Thread worker;

        /// <summary>Доступ к PHP: Web.Server.Php.Install()</summary>
        public static PhpTools Php { get { return PhpTools.Instance; } }

        public DupoWebServer(int port) : this(port, "www", "index.html") { }
        public DupoWebServer(int port, string root) : this(port, root, "index.html") { }

        public DupoWebServer(int port, string root, string mainFile)
        {
            Port = port <= 0 ? 8080 : port;
            Root = DupoRuntime.Resolve(string.IsNullOrEmpty(root) ? "www" : root);
            MainFile = string.IsNullOrEmpty(mainFile) ? "index.html" : mainFile;
            LastPort = Port;
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
            Log("Главный файл: " + MainFile + (PhpTools.Instance.IsInstalled ? "   (PHP включён)" : ""));
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
            string rel = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath).TrimStart('/');
            rel = rel.Replace('/', Path.DirectorySeparatorChar);

            string full = string.IsNullOrEmpty(rel) ? Path.Combine(Root, MainFile) : Path.Combine(Root, rel);
            if (Directory.Exists(full)) full = Path.Combine(full, MainFile);
            full = Path.GetFullPath(full);

            if (!full.StartsWith(Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))
            {
                SendError(ctx, 403, "Выход за пределы папки сайта запрещён");
                return;
            }

            Log(ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery +
                (File.Exists(full) ? "" : "  → 404"));

            if (!File.Exists(full)) { SendError(ctx, 404, "Не найдено: /" + rel.Replace('\\', '/')); return; }

            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext == ".php") ServePhp(ctx, full);
            else SendFile(ctx, full, ext);
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
            string type;
            if (!Mime.TryGetValue(ext, out type)) type = "application/octet-stream";

            byte[] data = File.ReadAllBytes(file);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = type;
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
        }

        void SendError(HttpListenerContext ctx, int code, string message)
        {
            string html = "<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">" +
                          "<title>" + code + "</title></head><body style=\"font-family:Segoe UI,sans-serif;" +
                          "background:#141821;color:#e6e9ef;padding:48px\"><h1 style=\"font-size:56px;margin:0\">" +
                          code + "</h1><p style=\"color:#9aa4b8\">" + WebUtility.HtmlEncode(message) +
                          "</p><p style=\"color:#4a5569\">DupoLang Web.Server</p></body></html>";
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
