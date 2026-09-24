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
// run. The page draws only the two content facts (DRA-373 D2): the downloads
// and concurrent-users tiles left the hero, so no key here needs a special
// case — a missing or null value paints a dash, never an invented number.
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
