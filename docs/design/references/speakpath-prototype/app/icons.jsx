// SpeakPath line-icon set. 2px stroke, rounded caps. Inherits currentColor.
(function () {
  const S = ({ children, size = 24, sw = 2, fill = "none", style }) =>
    React.createElement(
      "svg",
      {
        width: size, height: size, viewBox: "0 0 24 24", fill,
        stroke: "currentColor", strokeWidth: sw, strokeLinecap: "round",
        strokeLinejoin: "round", style,
      },
      children
    );
  const P = (d, extra) => React.createElement("path", { d, ...(extra || {}) });
  const C = (cx, cy, r, extra) => React.createElement("circle", { cx, cy, r, ...(extra || {}) });
  const L = (x1, y1, x2, y2) => React.createElement("line", { x1, y1, x2, y2 });

  const Icon = {
    home: (p) => React.createElement(S, p, P("M3 10.5 12 3l9 7.5"), P("M5 9.5V20a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V9.5")),
    path: (p) => React.createElement(S, p, C(6, 6, 2.4), C(18, 12, 2.4), C(7, 18, 2.4), P("M8.2 6.6c5 .4 8 1.6 8 5", { strokeDasharray: "0.1 3" }), P("M15.7 13.4c-4 2.2-6.5 1.6-8 2.8", { strokeDasharray: "0.1 3" })),
    grid: (p) => React.createElement(S, p, React.createElement("rect", { x: 3, y: 3, width: 7, height: 7, rx: 2 }), React.createElement("rect", { x: 14, y: 3, width: 7, height: 7, rx: 2 }), React.createElement("rect", { x: 3, y: 14, width: 7, height: 7, rx: 2 }), React.createElement("rect", { x: 14, y: 14, width: 7, height: 7, rx: 2 })),
    chart: (p) => React.createElement(S, p, P("M4 20V4"), P("M4 20h16"), P("M8 16v-3"), P("M13 16V9"), P("M18 16v-6")),
    user: (p) => React.createElement(S, p, C(12, 8, 3.6), P("M5.5 20a6.5 6.5 0 0 1 13 0")),
    pen: (p) => React.createElement(S, p, P("M14 5.5 18.5 10"), P("M4 20l1-4L16 5l3 3L8 19l-4 1Z")),
    mic: (p) => React.createElement(S, p, React.createElement("rect", { x: 9, y: 3, width: 6, height: 11, rx: 3 }), P("M5 11a7 7 0 0 0 14 0"), L(12, 18, 12, 21), L(9, 21, 15, 21)),
    ear: (p) => React.createElement(S, p, P("M7 9a5 5 0 0 1 10 0c0 3-2.5 3.5-2.5 6a2.5 2.5 0 0 1-5 0"), P("M9 9a3 3 0 0 1 5 0")),
    book: (p) => React.createElement(S, p, P("M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5Z"), P("M4 20.5A2.5 2.5 0 0 1 6.5 18H20")),
    sound: (p) => React.createElement(S, p, P("M4 9v6h4l5 4V5L8 9H4Z"), P("M17 9a4 4 0 0 1 0 6")),
    spark: (p) => React.createElement(S, p, P("M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8L12 3Z"), P("M19 16l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7.7-2Z")),
    flame: (p) => React.createElement(S, p, P("M12 3c.5 3-2.5 4-2.5 7a2.5 2.5 0 0 0 5 0c0-1 .5-1.5.5-1.5.8 1 1.5 2 1.5 3.5a4.5 4.5 0 0 1-9 0C8 8.5 11 7 12 3Z")),
    check: (p) => React.createElement(S, p, P("M4 12.5 9 17.5 20 6.5")),
    checkCircle: (p) => React.createElement(S, p, C(12, 12, 9), P("M8.5 12.2 11 14.7 15.7 9.6")),
    lock: (p) => React.createElement(S, p, React.createElement("rect", { x: 5, y: 11, width: 14, height: 9, rx: 2.5 }), P("M8 11V8a4 4 0 0 1 8 0v3")),
    arrowRight: (p) => React.createElement(S, p, P("M5 12h13"), P("M13 6l6 6-6 6")),
    arrowLeft: (p) => React.createElement(S, p, P("M19 12H6"), P("M11 6l-6 6 6 6")),
    chevronDown: (p) => React.createElement(S, p, P("M6 9l6 6 6-6")),
    chevronRight: (p) => React.createElement(S, p, P("M9 6l6 6-6 6")),
    play: (p) => React.createElement(S, p, P("M7 5l12 7-12 7V5Z", { fill: "currentColor", strokeWidth: 0 })),
    target: (p) => React.createElement(S, p, C(12, 12, 8.5), C(12, 12, 4.5), C(12, 12, 1, { fill: "currentColor", strokeWidth: 0 })),
    bulb: (p) => React.createElement(S, p, P("M9 16a5.5 5.5 0 1 1 6 0c-.6.5-1 1-1 2H10c0-1-.4-1.5-1-2Z"), L(10, 21, 14, 21)),
    chat: (p) => React.createElement(S, p, P("M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5v7a2.5 2.5 0 0 1-2.5 2.5H10l-4 3v-3H6.5A2.5 2.5 0 0 1 4 13.5Z")),
    heart: (p) => React.createElement(S, p, P("M12 20S4 14.5 4 8.8A4.2 4.2 0 0 1 12 7a4.2 4.2 0 0 1 8 1.8C20 14.5 12 20 12 20Z")),
    trophy: (p) => React.createElement(S, p, P("M7 4h10v4a5 5 0 0 1-10 0V4Z"), P("M7 5H4v1a3 3 0 0 0 3 3"), P("M17 5h3v1a3 3 0 0 1-3 3"), L(12, 13, 12, 17), P("M8 20h8"), P("M9.5 17h5l.5 3h-6Z")),
    clock: (p) => React.createElement(S, p, C(12, 12, 8.5), P("M12 7.5V12l3 2")),
    bell: (p) => React.createElement(S, p, P("M6 16V10a6 6 0 0 1 12 0v6l1.5 2.5h-15Z"), P("M10 20a2 2 0 0 0 4 0")),
    settings: (p) => React.createElement(S, p, C(12, 12, 3), P("M12 3v2.5M12 18.5V21M21 12h-2.5M5.5 12H3M18.4 5.6l-1.8 1.8M7.4 16.6l-1.8 1.8M18.4 18.4l-1.8-1.8M7.4 7.4 5.6 5.6")),
    globe: (p) => React.createElement(S, p, C(12, 12, 8.5), P("M3.5 12h17"), P("M12 3.5c2.5 2.4 2.5 14.6 0 17M12 3.5c-2.5 2.4-2.5 14.6 0 17")),
    flag: (p) => React.createElement(S, p, P("M6 21V4"), P("M6 4.5h11l-2 3.5 2 3.5H6")),
    quote: (p) => React.createElement(S, p, P("M9 7H6a2 2 0 0 0-2 2v3a2 2 0 0 0 2 2h2v2a2 2 0 0 1-2 2"), P("M19 7h-3a2 2 0 0 0-2 2v3a2 2 0 0 0 2 2h2v2a2 2 0 0 1-2 2")),
    plus: (p) => React.createElement(S, p, L(12, 5, 12, 19), L(5, 12, 19, 12)),
    x: (p) => React.createElement(S, p, L(6, 6, 18, 18), L(18, 6, 6, 18)),
    refresh: (p) => React.createElement(S, p, P("M4 12a8 8 0 0 1 13.5-5.8L20 8"), P("M20 4v4h-4"), P("M20 12a8 8 0 0 1-13.5 5.8L4 16"), P("M4 20v-4h4")),
    star: (p) => React.createElement(S, p, P("M12 4l2.3 4.9 5.2.7-3.8 3.6.9 5.3L12 16.5 7.4 18.4l.9-5.3L4.5 9.6l5.2-.7L12 4Z")),
    calendar: (p) => React.createElement(S, p, React.createElement("rect", { x: 4, y: 5, width: 16, height: 16, rx: 3 }), L(4, 9, 20, 9), L(9, 3, 9, 6), L(15, 3, 15, 6)),
    logout: (p) => React.createElement(S, p, P("M14 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h8"), P("M17 8l4 4-4 4"), L(9, 12, 21, 12)),
    shield: (p) => React.createElement(S, p, P("M12 3l7 3v5c0 5-3.5 8-7 10-3.5-2-7-5-7-10V6l7-3Z"), P("M9 12l2 2 4-4")),
  };
  window.SP = window.SP || {};
  window.SP.Icon = Icon;
})();
