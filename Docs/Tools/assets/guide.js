/* =====================================================================
   MS2026 Tool Guides — shared behaviour (no dependencies)
   - Side nav is generated from <section id data-nav="ラベル"> (add data-dev on dev sections)
   - Theme toggle (remembered per browser), scroll spy, reveal-on-scroll
   - Recipe search/filter (#recipeSearch, .chip[data-f], .recipe[data-tags])
   - Screen guide tabs (.tabguide), code tabs (.tabs/.panel), copy buttons
   Component reference: Docs/Tools/DOCS_GUIDE.md
   ===================================================================== */
(function () {
  "use strict";
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch (e) { return null; } }

  /* ---------- theme ---------- */
  var root = document.documentElement;
  var themeBtn = $("#themeBtn");
  function applyTheme(t) {
    root.setAttribute("data-theme", t);
    if (themeBtn) themeBtn.querySelector("[data-label]").textContent = t === "dark" ? "ライト表示" : "ダーク表示";
  }
  var prefersLight = window.matchMedia && matchMedia("(prefers-color-scheme: light)").matches;
  applyTheme(store("ms2026-guide-theme") || (prefersLight ? "light" : "dark"));
  if (themeBtn) themeBtn.addEventListener("click", function () {
    var n = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
    applyTheme(n); store("ms2026-guide-theme", n);
  });

  /* ---------- side nav from sections ---------- */
  var toc = $("#toc");
  if (toc) {
    var n = 0, devStarted = false, html = "";
    $$("section[data-nav]").forEach(function (s) {
      if (s.hasAttribute("data-dev") && !devStarted) { html += '<li class="devsep">プログラマー向け</li>'; devStarted = true; }
      n++;
      html += '<li><a class="nl" href="#' + s.id + '"><span>' + (n < 10 ? "0" : "") + n + "</span>" + s.getAttribute("data-nav") + "</a></li>";
    });
    toc.innerHTML = html;
  }

  /* ---------- marquee ---------- */
  var mq = $("#mq");
  if (mq && mq.dataset.words) {
    var words = mq.dataset.words.split("|"), h = "";
    for (var i = 0; i < 2; i++) words.forEach(function (w) { h += "<span>" + w + "</span>"; });
    mq.innerHTML = h;
  }

  /* ---------- recipe search / filter ---------- */
  var search = $("#recipeSearch"), chips = $$("#recipeChips .chip"), recipes = $$(".recipe"), noRes = $(".noresult");
  var activeF = "all";
  function filterRecipes() {
    var q = search ? search.value.trim().toLowerCase() : "", shown = 0;
    recipes.forEach(function (r) {
      var okF = activeF === "all" || (r.getAttribute("data-tags") || "").split(" ").indexOf(activeF) >= 0;
      var okQ = !q || r.textContent.toLowerCase().indexOf(q) >= 0;
      var show = okF && okQ;
      r.classList.toggle("hide", !show);
      if (show) shown++;
    });
    if (noRes) noRes.style.display = shown ? "none" : "block";
  }
  if (search) search.addEventListener("input", filterRecipes);
  chips.forEach(function (c) {
    c.addEventListener("click", function () {
      activeF = c.getAttribute("data-f");
      chips.forEach(function (x) { x.setAttribute("aria-pressed", x === c ? "true" : "false"); });
      filterRecipes();
    });
  });

  /* ---------- tab groups (screen guide + code) ---------- */
  function tabGroup(buttons, getPanel, onClass) {
    function sel(b) {
      buttons.forEach(function (x) {
        var on = x === b;
        x.setAttribute("aria-selected", on ? "true" : "false");
        x.tabIndex = on ? 0 : -1;
        var p = getPanel(x); if (p) p.classList.toggle(onClass, on);
      });
    }
    buttons.forEach(function (b, i) {
      b.addEventListener("click", function () { sel(b); });
      b.addEventListener("keydown", function (e) {
        var j = null;
        if (e.key === "ArrowRight" || e.key === "ArrowDown") j = (i + 1) % buttons.length;
        if (e.key === "ArrowLeft" || e.key === "ArrowUp") j = (i - 1 + buttons.length) % buttons.length;
        if (j !== null) { e.preventDefault(); buttons[j].focus(); sel(buttons[j]); }
      });
    });
    if (buttons.length) sel(buttons.filter(function (b) { return b.getAttribute("aria-selected") === "true"; })[0] || buttons[0]);
  }
  $$(".tabguide").forEach(function (g) {
    tabGroup($$(".tg-list button", g), function (b) { return document.getElementById(b.getAttribute("aria-controls")); }, "on");
  });
  $$(".tabs").forEach(function (t) {
    tabGroup($$(".tab", t), function (b) { return document.getElementById(b.getAttribute("aria-controls")); }, "on");
  });

  /* ---------- syntax highlight (tiny) + copy ---------- */
  function hl(src) {
    var esc = src.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    var out = "", last = 0, m;
    var re = /(\/\/[^\n]*)|("(?:[^"\\\n]|\\.)*")|\b(using|var|new|in|await|async|public|private|static|void|if|else|return|float|int|bool|string|class|sealed|namespace|foreach|for|null|true|false|this|readonly)\b|\b([A-Z][A-Za-z0-9_]*)\b|\b(\d+(?:\.\d+)?f?)\b/g;
    while ((m = re.exec(esc))) {
      out += esc.slice(last, m.index);
      if (m[1]) out += '<span class="tk-c">' + m[1] + "</span>";
      else if (m[2]) out += '<span class="tk-s">' + m[2] + "</span>";
      else if (m[3]) out += '<span class="tk-k">' + m[3] + "</span>";
      else if (m[4]) out += '<span class="tk-t">' + m[4] + "</span>";
      else out += '<span class="tk-n">' + m[5] + "</span>";
      last = re.lastIndex;
    }
    return out + esc.slice(last);
  }
  $$(".codebox").forEach(function (box) {
    var pre = $("pre", box); if (!pre) return;
    var raw = pre.textContent.replace(/^\n/, "");
    box._raw = raw;
    if (!box.hasAttribute("data-plain")) pre.innerHTML = hl(raw);
    var btn = $(".copy", box);
    if (!btn) return;
    btn.addEventListener("click", function () {
      function ok() { btn.textContent = "copied ✓"; btn.classList.add("done"); setTimeout(function () { btn.textContent = "copy"; btn.classList.remove("done"); }, 1400); }
      function fb() {
        var ta = document.createElement("textarea"); ta.value = box._raw; ta.style.position = "fixed"; ta.style.opacity = "0";
        document.body.appendChild(ta); ta.select();
        try { document.execCommand("copy"); ok(); } catch (e) { }
        document.body.removeChild(ta);
      }
      if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(box._raw).then(ok, fb); else fb();
    });
  });

  /* ---------- reveal + scrollspy ---------- */
  var rvs = $$(".rv");
  if ("IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); } });
    }, { rootMargin: "0px 0px -6% 0px", threshold: 0.04 });
    rvs.forEach(function (r) { io.observe(r); });
    var links = $$("#toc a");
    var so = new IntersectionObserver(function (es) {
      es.forEach(function (e) {
        if (!e.isIntersecting) return;
        var id = "#" + e.target.id;
        links.forEach(function (a) {
          var on = a.getAttribute("href") === id;
          a.classList.toggle("on", on);
          if (on && window.innerWidth <= 920 && toc) toc.scrollLeft = a.offsetLeft - toc.clientWidth / 2 + a.offsetWidth / 2;
        });
      });
    }, { rootMargin: "-45% 0px -50% 0px" });
    links.forEach(function (a) { var s = document.getElementById(a.getAttribute("href").slice(1)); if (s) so.observe(s); });
  } else {
    rvs.forEach(function (r) { r.classList.add("in"); });
  }
})();
