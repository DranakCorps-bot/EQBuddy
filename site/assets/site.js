// Progressive enhancement only: scroll progress bar, dot-nav highlight,
// reveal-on-scroll. The page is fully readable with this file blocked —
// CSS hides nothing unless <html> carries the "js" class set here.
document.documentElement.classList.add('js');

var progress = document.getElementById('progress');
var sections = [].slice.call(document.querySelectorAll('main section'));
var nav = [].slice.call(document.querySelectorAll('.nav a'));
var reveals = [].slice.call(document.querySelectorAll('.reveal'));

function update() {
  var max = document.documentElement.scrollHeight - innerHeight;
  progress.style.width = (max ? scrollY / max * 100 : 0) + '%';
}
addEventListener('scroll', update, { passive: true });
addEventListener('resize', update);
update();

var so = new IntersectionObserver(function (es) {
  es.forEach(function (e) {
    if (e.isIntersecting) nav.forEach(function (a) {
      a.classList.toggle('active', a.getAttribute('href') === '#' + e.target.id);
    });
  });
}, { rootMargin: '-42% 0px -42% 0px' });
sections.forEach(function (s) { so.observe(s); });

var ro = new IntersectionObserver(function (es) {
  es.forEach(function (e) {
    if (e.isIntersecting) { e.target.classList.add('visible'); ro.unobserve(e.target); }
  });
}, { threshold: 0.1 });
reveals.forEach(function (r) { ro.observe(r); });
