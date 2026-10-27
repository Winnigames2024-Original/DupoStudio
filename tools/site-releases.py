#!/usr/bin/env python3
"""
Собирает страницы версий для сайта: docs/release/<версия>/index.html.

На каждой странице — все файлы выпуска, исходники zip и tar.gz
и заметки к выпуску. Запускается при сборке сайта, поэтому страницы
появляются сами, как только выходит новая версия.

Запуск: python3 tools/site-releases.py [--repo owner/name] [--docs docs]
"""

import argparse
import html
import json
import os
import re
import sys
import urllib.error
import urllib.request

REPO = "Winnigames2024-Original/DupoStudio"
GITHUB = "https://github.com"


# ----------------------------------------------------------------------
# данные
# ----------------------------------------------------------------------

def fetch(repo):
    """Выпуски с GitHub. Пустой список, если не достучались."""
    url = "https://api.github.com/repos/%s/releases?per_page=100" % repo
    req = urllib.request.Request(url, headers={
        "Accept": "application/vnd.github+json",
        "User-Agent": "DupoStudio-site",
    })

    token = os.environ.get("GITHUB_TOKEN", "").strip()
    if token:
        req.add_header("Authorization", "Bearer " + token)

    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            data = json.load(r)
    except (urllib.error.URLError, urllib.error.HTTPError, ValueError, OSError) as e:
        print("Выпуски не прочитались: %s" % e, file=sys.stderr)
        return []

    return [x for x in data if not x.get("draft")]


def slug(tag):
    """v0.0.3 → 0.0.3. В адресе буквы v нет."""
    return re.sub(r"^v", "", str(tag or ""), flags=re.I)


def size(n):
    if not n:
        return ""
    mb = n / 1048576.0
    return "%.1f МБ" % mb if mb >= 1 else "%d КБ" % round(n / 1024.0)


def when(iso):
    months = ["января", "февраля", "марта", "апреля", "мая", "июня",
              "июля", "августа", "сентября", "октября", "ноября", "декабря"]
    m = re.match(r"(\d{4})-(\d{2})-(\d{2})", str(iso or ""))
    if not m:
        return ""
    year, mon, day = m.group(1), int(m.group(2)), int(m.group(3))
    return "%d %s %s" % (day, months[mon - 1], year)


ICONS = [
    (r"Setup\.exe$", "🖥"),
    (r"\.exe$", "⚙"),
    (r"\.zip$", "📦"),
    (r"\.tar\.gz$", "📦"),
    (r"\.msi$", "🖥"),
]


def icon(name):
    for pattern, ch in ICONS:
        if re.search(pattern, name, re.I):
            return ch
    return "📄"


def about(name):
    """Что это за файл — человеческими словами."""
    n = name.lower()
    if n.endswith("setup.exe"):
        return "установщик, BuildTools внутри"
    if n == "dupostudio.exe":
        return "студия одним файлом, без установки"
    if n == "buildtools.zip":
        return "DupoBuild.exe и DupoLang.exe отдельно"
    if n.endswith(".zip"):
        return "архив"
    if n.endswith(".tar.gz"):
        return "архив"
    if n.endswith(".exe"):
        return "исполняемый файл"
    return ""


# ----------------------------------------------------------------------
# разметка заметок
# ----------------------------------------------------------------------

def inline(text):
    """`код`, **жирный**, ссылки."""
    out = html.escape(text, quote=False)
    out = re.sub(r"`([^`]+)`", r"<code>\1</code>", out)
    out = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", out)
    out = re.sub(r"(?<!\w)(https?://[^\s<)]+)", r'<a href="\1">\1</a>', out)
    return out


def notes(body):
    """Заметки выпуска в HTML: заголовки, списки, таблицы и блоки кода."""
    rows = str(body or "").replace("\r\n", "\n").split("\n")
    out = []
    mode = None          # None, 'ul', 'code', 'table'

    def close():
        nonlocal mode
        if mode == "ul":
            out.append("</ul>")
        elif mode == "code":
            out.append("</code></pre>")
        elif mode == "table":
            out.append("</tbody></table>")
        mode = None

    i = 0
    while i < len(rows):
        row = rows[i]
        bare = row.strip()

        if bare.startswith("```"):
            if mode == "code":
                close()
            else:
                close()
                out.append('<pre class="code"><code>')
                mode = "code"
            i += 1
            continue

        if mode == "code":
            out.append(html.escape(row, quote=False))
            i += 1
            continue

        if not bare:
            close()
            i += 1
            continue

        head = re.match(r"^(#{1,6})\s+(.*)$", bare)
        if head:
            close()
            level = min(3, max(2, len(head.group(1))))
            out.append("<h%d>%s</h%d>" % (level, inline(head.group(2)), level))
            i += 1
            continue

        # Таблица: строка с | и следующая из дефисов.
        if bare.startswith("|") and i + 1 < len(rows) and re.match(r"^\|[\s:|-]+\|$", rows[i + 1].strip()):
            close()
            cells = [c.strip() for c in bare.strip("|").split("|")]
            out.append('<table class="rel-table"><thead><tr>')
            for c in cells:
                out.append("<th>%s</th>" % inline(c))
            out.append("</tr></thead><tbody>")
            mode = "table"
            i += 2
            continue

        if mode == "table":
            if not bare.startswith("|"):
                close()
                continue
            cells = [c.strip() for c in bare.strip("|").split("|")]
            out.append("<tr>")
            for c in cells:
                out.append("<td>%s</td>" % inline(c))
            out.append("</tr>")
            i += 1
            continue

        item = re.match(r"^[-*]\s+(.*)$", bare)
        if item:
            if mode != "ul":
                close()
                out.append("<ul>")
                mode = "ul"
            out.append("<li>%s</li>" % inline(item.group(1)))
            i += 1
            continue

        close()
        out.append("<p>%s</p>" % inline(bare))
        i += 1

    close()
    return "\n".join(out)


# ----------------------------------------------------------------------
# страницы
# ----------------------------------------------------------------------

HEAD = """<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>%(title)s</title>
<meta name="description" content="%(description)s">
<link rel="icon" href="%(up)sassets/logo.svg">
<link rel="stylesheet" href="%(up)sassets/style.css">
<style>
.rel-table { width:100%%; border-collapse:collapse; margin:18px 0 }
.rel-table th, .rel-table td { text-align:left; padding:8px 12px; border-bottom:1px solid var(--line,#2a3140) }
.rel-table th { font-size:13px; letter-spacing:.04em; text-transform:uppercase; opacity:.65 }
pre.code { overflow:auto; padding:14px 16px; border-radius:10px; background:rgba(0,0,0,.28); line-height:1.5 }
pre.code code { background:none; padding:0 }
.crumbs { font-size:14px; opacity:.7; margin-bottom:10px }
.crumbs a { color:inherit }
</style>
</head>
<body>

<header class="top">
  <div class="wrap">
    <a class="brand" href="%(up)s" style="text-decoration:none;color:inherit">
      <img src="%(up)sassets/logo.svg" alt=""> Dupo Studio
    </a>
    <nav>
      <a href="%(up)s#features">Возможности</a>
      <a href="%(up)s#lang">Язык</a>
      <a href="%(up)s#dapp">Формат .dapp</a>
      <a href="%(up)sreleases/"%(relactive)s>Релизы</a>
      <a href="https://github.com/%(repo)s">GitHub</a>
    </nav>
  </div>
</header>
"""

FOOT = """
<footer>
  <div class="wrap">
    <div>Dupo Studio · Winnigames2024-Original</div>
    <div><a href="https://github.com/%(repo)s">GitHub</a></div>
  </div>
</footer>

</body>
</html>
"""


def page_version(rel, repo, newest):
    tag = rel.get("tag_name") or ""
    ver = slug(tag)
    name = rel.get("name") or tag
    up = "../../"

    files = []
    for a in rel.get("assets", []):
        files.append((a.get("name", ""), a.get("browser_download_url", ""),
                      size(a.get("size")), about(a.get("name", ""))))

    sources = [
        ("Исходники (zip)", rel.get("zipball_url") or
         "%s/%s/archive/refs/tags/%s.zip" % (GITHUB, repo, tag), "", "код этой версии"),
        ("Исходники (tar.gz)", rel.get("tarball_url") or
         "%s/%s/archive/refs/tags/%s.tar.gz" % (GITHUB, repo, tag), "", "код этой версии"),
    ]

    def cards(items):
        out = []
        for nm, url, sz, note in items:
            out.append(
                '<a class="asset" href="%s">'
                '<span class="ic">%s</span>'
                '<span class="nm">%s</span>'
                '<span class="meta">%s</span></a>'
                % (html.escape(url, quote=True), icon(nm), html.escape(nm),
                   html.escape(" · ".join([x for x in (sz, note) if x]))))
        return "\n".join(out)

    head = HEAD % {
        "title": "%s — Dupo Studio" % html.escape(name),
        "description": html.escape("Dupo Studio %s: файлы выпуска и исходники." % ver),
        "up": up,
        "relactive": "",
        "repo": repo,
    }

    body = []
    body.append('<div class="hero" style="padding-bottom:24px"><div class="wrap">')
    body.append('<div class="crumbs"><a href="%sreleases/">Релизы</a> / %s</div>'
                % (up, html.escape(tag)))
    body.append('<div class="badge">%s%s</div>'
                % ("Выпущено " + when(rel.get("published_at")) if rel.get("published_at") else "Выпуск",
                   " · последняя версия" if newest else ""))
    body.append('<h1 style="font-size:clamp(32px,5vw,46px)">%s</h1>' % html.escape(name))
    body.append('<p class="lead" style="margin-bottom:0">Файлы этого выпуска и исходники. '
                'Установщик ставит студию без прав администратора, '
                'BuildTools едут внутри него.</p>')
    body.append('</div></div>')

    body.append('<section style="padding-top:26px"><div class="wrap">')

    if files:
        body.append("<h2>Файлы</h2>")
        body.append('<div class="assets">%s</div>' % cards(files))

    body.append("<h2>Исходники</h2>")
    body.append('<div class="assets">%s</div>' % cards(sources))

    notes_html = notes(rel.get("body"))
    if notes_html.strip():
        body.append('<h2 style="margin-top:34px">Что изменилось</h2>')
        body.append(notes_html)

    body.append('<p style="margin-top:30px">'
                '<a class="btn" href="%s">Страница выпуска на GitHub</a> '
                '<a class="btn" href="%sreleases/">Все версии</a></p>'
                % (html.escape(rel.get("html_url") or "", quote=True), up))
    body.append('</div></section>')

    return head + "\n".join(body) + (FOOT % {"repo": repo})


def page_index(releases, repo):
    up = "../"
    head = HEAD % {
        "title": "Версии — Dupo Studio",
        "description": "Все версии Dupo Studio: файлы и исходники.",
        "up": up,
        "relactive": ' class="active"',
        "repo": repo,
    }

    body = ['<div class="hero" style="padding-bottom:24px"><div class="wrap">',
            '<div class="badge">Страница на каждую версию</div>',
            '<h1 style="font-size:clamp(32px,5vw,46px)">Версии</h1>',
            '<p class="lead" style="margin-bottom:0">Файлы выпуска и исходники '
            'лежат на странице версии.</p>',
            '</div></div>',
            '<section style="padding-top:26px"><div class="wrap">']

    if releases:
        body.append('<table class="rel-table"><thead><tr>'
                    '<th>Версия</th><th>Выпущена</th><th>Файлов</th><th></th>'
                    '</tr></thead><tbody>')
        for i, rel in enumerate(releases):
            tag = rel.get("tag_name") or ""
            ver = slug(tag)
            body.append("<tr><td><b>%s</b>%s</td><td>%s</td><td>%d</td>"
                        '<td><a href="%s/">открыть</a></td></tr>'
                        % (html.escape(tag),
                           " <span class=\"tag new\">последняя</span>" if i == 0 else "",
                           when(rel.get("published_at")),
                           len(rel.get("assets", [])),
                           html.escape(ver, quote=True)))
        body.append("</tbody></table>")
    else:
        body.append('<p>Список не прочитался — посмотрите '
                    '<a href="https://github.com/%s/releases">выпуски на GitHub</a>.</p>' % repo)

    body.append('<p style="margin-top:24px"><a class="btn" href="%sreleases/">К загрузкам</a></p>' % up)
    body.append('</div></section>')

    return head + "\n".join(body) + (FOOT % {"repo": repo})


def page_redirect(target, up):
    return (
        '<!DOCTYPE html>\n<html lang="ru">\n<head>\n<meta charset="utf-8">\n'
        '<meta http-equiv="refresh" content="0; url=%s">\n'
        '<link rel="canonical" href="%s">\n'
        '<title>Переход — Dupo Studio</title>\n</head>\n<body>\n'
        '<p>Страница переехала: <a href="%s">%s</a></p>\n'
        '<script>location.replace(%s)</script>\n</body>\n</html>\n'
        % (target, target, target, target, json.dumps(target))
    )


# ----------------------------------------------------------------------

def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("записано: %s" % path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY") or REPO)
    ap.add_argument("--docs", default="docs")
    args = ap.parse_args()

    releases = fetch(args.repo)
    root = os.path.join(args.docs, "release")

    if not releases:
        # Без списка всё равно оставляем вход, чтобы адрес не ломался.
        write(os.path.join(root, "index.html"), page_index([], args.repo))
        return 0

    for i, rel in enumerate(releases):
        ver = slug(rel.get("tag_name"))
        if not ver:
            continue
        write(os.path.join(root, ver, "index.html"),
              page_version(rel, args.repo, i == 0))

    write(os.path.join(root, "index.html"), page_index(releases, args.repo))

    newest = slug(releases[0].get("tag_name"))
    if newest:
        write(os.path.join(root, "latest", "index.html"),
              page_redirect("../" + newest + "/", "../../"))

    print("страниц версий: %d" % len(releases))
    return 0


if __name__ == "__main__":
    sys.exit(main())
