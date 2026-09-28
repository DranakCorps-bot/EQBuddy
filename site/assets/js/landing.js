// Progressive enhancement only — the page must read fully without this file
// (FABLE.md plan T1). Progress bar, dot-nav highlight, reveal-on-scroll.
(function () {
  "use strict";
  document.documentElement.classList.add("js");

  var bar = document.getElementById("progress");
  function onScroll() {
    var doc = document.documentElement;
    var max = doc.scrollHeight - doc.clientHeight;
    if (bar) bar.style.width = (max > 0 ? (100 * doc.scrollTop / max) : 0) + "%";
  }
  addEventListener("scroll", onScroll, { passive: true });
  onScroll();

  var reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;

  var dots = Array.prototype.slice.call(document.querySelectorAll(".dotnav a"));
  var sections = dots
    .map(function (d) { return document.querySelector(d.getAttribute("href")); })
    .filter(Boolean);
  var spy = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (!e.isIntersecting) return;
      dots.forEach(function (d) {
        d.classList.toggle("active", d.getAttribute("href") === "#" + e.target.id);
      });
    });
  }, { rootMargin: "-45% 0px -45% 0px" });
  sections.forEach(function (s) { spy.observe(s); });

  if (!reduced) {
    var reveal = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) { e.target.classList.add("in"); reveal.unobserve(e.target); }
      });
    }, { rootMargin: "0px 0px -8% 0px" });
    document.querySelectorAll(".reveal").forEach(function (el) { reveal.observe(el); });
  }
})();

// Lightbox (DRA-48 refine): click any capture or clip on the page to expand it;
// Esc, the ✕, or a click anywhere dismisses. Progressive enhancement only — the
// overlay exists only once JS has run, and without it the images are plain images.
(function () {
  "use strict";
  var box = document.createElement("div");
  box.className = "lightbox";
  box.hidden = true;
  box.setAttribute("role", "dialog");
  box.setAttribute("aria-modal", "true");
  box.setAttribute("aria-label", "Expanded capture");
  var big = document.createElement("img");
  big.alt = "";
  var close = document.createElement("button");
  close.type = "button";
  close.className = "lb-close";
  close.textContent = "✕ Close";
  box.appendChild(big);
  box.appendChild(close);
  document.body.appendChild(box);

  function dismiss() {
    if (box.hidden) return;
    box.hidden = true;
    big.src = "";                       // a dismissed GIF must not keep animating
    document.body.style.overflow = "";
  }
  document.addEventListener("click", function (e) {
    var el = e.target;
    if (!box.hidden) { dismiss(); return; }   // click-out AND click-on-image both exit
    if (el instanceof HTMLImageElement && el.closest(".shot")) {
      big.src = el.currentSrc || el.src;
      big.alt = el.alt || "";
      box.hidden = false;
      document.body.style.overflow = "hidden";
      close.focus();
    }
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") dismiss();
  });
})();

// Hero KPIs. site/metrics.json is the source of truth; the .n text in the
// HTML is the same snapshot so the strip still reads if this fetch does not
// run. The page draws two content facts (DRA-373); metrics.json keeps its
// other keys, and a key the page does not draw is simply never painted.
// Values are comma-formatted integers; anything else paints a dash.
(function () {
  "use strict";
  var root = document.getElementById("hero-kpis");
  if (!root || !window.fetch) return;

  function formatMetric(value) {
    if (value === null || value === undefined) return "\u2014";
    if (typeof value !== "number" || !isFinite(value)) return "\u2014";
    var n = Math.round(value);
    var sign = n < 0 ? "-" : "";
    var digits = String(Math.abs(n));
    var out = "";
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 === 0) out += ",";
      out += digits.charAt(i);
    }
    return sign + out;
  }

  var url = new URL("metrics.json", document.baseURI);
  fetch(url, { credentials: "same-origin" })
    .then(function (response) {
      if (!response.ok) throw new Error(String(response.status));
      return response.json();
    })
    .then(function (metrics) {
      var nodes = root.querySelectorAll("[data-metric]");
      for (var i = 0; i < nodes.length; i++) {
        var key = nodes[i].getAttribute("data-metric");
        if (!Object.prototype.hasOwnProperty.call(metrics, key)) continue;
        nodes[i].textContent = formatMetric(metrics[key]);
      }
    })
    .catch(function () { /* keep the snapshot painted in the HTML */ });
})();

// Live figures (Founder decision 2026-09-28). live.json is SAME-ORIGIN: the hourly Pages
// deploy writes it into the published site (scripts/landing-telemetry.ps1), so the
// visitor's browser never contacts the telemetry worker or the GitHub API. The committed
// copy is explicitly unavailable. Anything missing, malformed or older than MAX_AGE_HOURS
// leaves the tiles as the dashes the HTML ships with, and says so in the caption — the
// page shows "unavailable", never a stale or invented number.
(function () {
  "use strict";
  var MAX_AGE_HOURS = 6;
  var band = document.getElementById("live-kpis");
  var asof = document.getElementById("live-asof");
  var downloads = document.querySelector('#hero-kpis [data-metric="downloads"]');
  if (!window.fetch || (!band && !downloads)) return;

  var MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  function two(n) { return (n < 10 ? "0" : "") + n; }
  function day(d) { return d.getUTCDate() + " " + MONTHS[d.getUTCMonth()] + " " + d.getUTCFullYear(); }
  function stamp(d) { return day(d) + ", " + two(d.getUTCHours()) + ":" + two(d.getUTCMinutes()) + " UTC"; }

  function count(value) {
    if (typeof value !== "number" || !isFinite(value) || value < 0) return "—";
    var digits = String(Math.round(value));
    var out = "";
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 === 0) out += ",";
      out += digits.charAt(i);
    }
    return out;
  }

  // A half is usable only if it says so, carries a readable time, and that time is recent.
  function fresh(half) {
    if (!half || half.available !== true || typeof half.asOf !== "string") return null;
    var at = new Date(half.asOf);
    if (isNaN(at.getTime())) return null;
    var ageHours = (Date.now() - at.getTime()) / 3600000;
    if (ageHours > MAX_AGE_HOURS || ageHours < -0.25) return null;
    return at;
  }

  fetch(new URL("live.json", document.baseURI), { credentials: "same-origin", cache: "no-cache" })
    .then(function (response) {
      if (!response.ok) throw new Error(String(response.status));
      return response.json();
    })
    .then(function (live) {
      if (!live || live.schema !== 1) return;

      var t = live.telemetry;
      var tAt = fresh(t);
      if (band && tAt) {
        var nodes = band.querySelectorAll("[data-live]");
        for (var i = 0; i < nodes.length; i++) {
          var key = nodes[i].getAttribute("data-live");
          nodes[i].textContent = Object.prototype.hasOwnProperty.call(t, key) ? count(t[key]) : "—";
        }
        if (asof) asof.textContent = "As of " + stamp(tAt) + ".";
      }

      // The hero's all-versions downloads total, re-measured by the same deploy. When it is
      // not fresh, the dated snapshot from metrics.json stays — it says its own date.
      var d = live.downloads;
      var dAt = fresh(d);
      if (downloads && dAt && typeof d.total === "number" && isFinite(d.total) && d.total >= 0) {
        downloads.textContent = count(d.total);
        var note = downloads.parentNode.querySelector(".note");
        if (note) note.textContent = note.textContent.replace(/as of .*$/, "as of " + day(dAt));
      }
    })
    .catch(function () { /* the dashes and "Not available right now." stay */ });
})();
