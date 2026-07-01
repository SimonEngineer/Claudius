namespace Weaver.Scraping.Proxy;

/// <summary>
/// JS injected into every proxied page so the parent React app can turn mouse hovers/clicks
/// into CSS selectors. Runs inside the iframe's (proxied-origin) document; talks to the
/// parent purely via postMessage since the two are on different origins.
/// </summary>
public static class OverlayScript
{
    public const string Source = """
    (function () {
      var highlightBox = document.createElement('div');
      highlightBox.id = '__weaver_highlight';
      Object.assign(highlightBox.style, {
        position: 'absolute', pointerEvents: 'none', zIndex: 2147483647,
        border: '2px solid #6366f1', background: 'rgba(99,102,241,0.15)',
        transition: 'all 60ms ease-out', display: 'none'
      });
      document.documentElement.appendChild(highlightBox);

      var label = document.createElement('div');
      Object.assign(label.style, {
        position: 'absolute', zIndex: 2147483647, background: '#111827', color: '#fff',
        font: '11px monospace', padding: '2px 6px', borderRadius: '4px', pointerEvents: 'none',
        display: 'none', whiteSpace: 'nowrap'
      });
      document.documentElement.appendChild(label);

      // All navigation is blocked -- this is a picker, not a browser; the parent app changes
      // the preview URL (and re-requests the proxy) instead of letting the iframe navigate itself.
      document.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); }, true);
      document.addEventListener('submit', function (e) { e.preventDefault(); e.stopPropagation(); }, true);

      function exactSelector(el) {
        if (!(el instanceof Element)) return null;
        if (el.id && document.querySelectorAll('#' + CSS.escape(el.id)).length === 1) {
          return '#' + CSS.escape(el.id);
        }
        var path = [];
        var node = el;
        while (node && node.nodeType === 1 && node !== document.documentElement) {
          var selector = node.tagName.toLowerCase();
          if (node.id && document.querySelectorAll('#' + CSS.escape(node.id)).length === 1) {
            selector = '#' + CSS.escape(node.id);
            path.unshift(selector);
            break;
          }
          var parent = node.parentElement;
          if (parent) {
            var sameTag = Array.prototype.filter.call(parent.children, function (c) { return c.tagName === node.tagName; });
            if (sameTag.length > 1) {
              selector += ':nth-of-type(' + (sameTag.indexOf(node) + 1) + ')';
            }
          }
          path.unshift(selector);
          var candidate = path.join(' > ');
          if (document.querySelectorAll(candidate).length === 1) {
            return candidate;
          }
          node = parent;
        }
        return path.join(' > ');
      }

      // Walks up from the clicked element looking for the nearest ancestor that repeats as a
      // sibling (same tag + class list) -- that ancestor is almost always the "row" of a list/table,
      // so generalizing to its tag+class (dropping nth-of-type) yields a selector matching every row.
      function repeatingContainerSelector(el) {
        var node = el;
        while (node && node.nodeType === 1 && node !== document.body) {
          var parent = node.parentElement;
          if (parent) {
            var siblings = Array.prototype.filter.call(parent.children, function (c) {
              return c.tagName === node.tagName && c.className === node.className;
            });
            if (siblings.length >= 2) {
              var tag = node.tagName.toLowerCase();
              var classSelector = node.className && typeof node.className === 'string'
                ? '.' + node.className.trim().split(/\s+/).filter(Boolean).map(CSS.escape).join('.')
                : '';
              return tag + classSelector;
            }
          }
          node = parent;
        }
        return exactSelector(el);
      }

      // When the parent knows the current "item container" selector, this computes a selector
      // for `el` scoped to just inside that container (e.g. ".price" instead of
      // "body > div > ul > li:nth-of-type(3) > .price"), which is what the backend's per-item
      // field extraction actually needs -- one that works identically for every repeated item.
      var currentContainerSelector = null;
      window.addEventListener('message', function (e) {
        if (e.data && e.data.type === 'weaver:setContainerSelector') {
          currentContainerSelector = e.data.selector || null;
        }
      });

      function relativeSelector(el, containerSelector) {
        if (!containerSelector) return null;
        var containers;
        try { containers = Array.prototype.slice.call(document.querySelectorAll(containerSelector)); }
        catch (err) { return null; }

        var container = null;
        var node = el;
        while (node && node !== document.body) {
          if (containers.indexOf(node) !== -1) { container = node; break; }
          node = node.parentElement;
        }
        if (!container) return null;
        if (container === el) return '';

        var path = [];
        var cur = el;
        while (cur && cur !== container) {
          var seg = cur.tagName.toLowerCase();
          var parent = cur.parentElement;
          if (parent) {
            var sameTag = Array.prototype.filter.call(parent.children, function (c) { return c.tagName === cur.tagName; });
            if (sameTag.length > 1) seg += ':nth-of-type(' + (sameTag.indexOf(cur) + 1) + ')';
          }
          path.unshift(seg);
          cur = parent;
        }
        return path.join(' > ');
      }

      function describe(el) {
        var attrs = {};
        for (var i = 0; i < el.attributes.length; i++) {
          attrs[el.attributes[i].name] = el.attributes[i].value;
        }
        return {
          tagName: el.tagName.toLowerCase(),
          text: (el.textContent || '').trim().slice(0, 200),
          attributes: attrs
        };
      }

      function elementAt(e) {
        return document.elementFromPoint(e.clientX, e.clientY);
      }

      document.addEventListener('mousemove', function (e) {
        var el = elementAt(e);
        if (!el || el === highlightBox || el === label) return;
        var rect = el.getBoundingClientRect();
        var scrollX = window.scrollX, scrollY = window.scrollY;
        highlightBox.style.display = 'block';
        highlightBox.style.left = (rect.left + scrollX) + 'px';
        highlightBox.style.top = (rect.top + scrollY) + 'px';
        highlightBox.style.width = rect.width + 'px';
        highlightBox.style.height = rect.height + 'px';

        var listSelector = repeatingContainerSelector(el);
        var matchCount = 0;
        try { matchCount = document.querySelectorAll(listSelector).length; } catch (err) {}

        label.style.display = 'block';
        label.style.left = (rect.left + scrollX) + 'px';
        label.style.top = Math.max(0, rect.top + scrollY - 18) + 'px';
        label.textContent = el.tagName.toLowerCase() + (matchCount > 1 ? ' (' + matchCount + ' similar)' : '');
      }, true);

      document.addEventListener('click', function (e) {
        var el = elementAt(e);
        if (!el) return;
        var payload = {
          type: 'weaver:elementPicked',
          exactSelector: exactSelector(el),
          repeatingContainerSelector: repeatingContainerSelector(el),
          relativeSelector: relativeSelector(el, currentContainerSelector),
          element: describe(el)
        };
        window.parent.postMessage(payload, '*');
      }, true);

      window.parent.postMessage({ type: 'weaver:ready', url: window.location.href }, '*');
    })();
    """;
}
