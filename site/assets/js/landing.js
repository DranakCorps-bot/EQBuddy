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

// Deep-dive TOC highlight (T4 hybrid): same pattern as the dot nav, scoped to
// the dive region. Progressive enhancement only, like everything above.
(function () {
  "use strict";
  var tocLinks = Array.prototype.slice.call(document.querySelectorAll(".dive-toc a"));
  var tocTargets = tocLinks
    .map(function (a) { return document.querySelector(a.getAttribute("href")); })
    .filter(Boolean);
  var tocSpy = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (!e.isIntersecting) return;
      tocLinks.forEach(function (a) {
        a.classList.toggle("active", a.getAttribute("href") === "#" + e.target.id);
      });
    });
  }, { rootMargin: "-30% 0px -55% 0px" });
  tocTargets.forEach(function (t) { tocSpy.observe(t); });
})();
