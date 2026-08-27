// Ambient particle background, sits behind everything (#particles-background is position:fixed,
// z-index:-1 - see site.css). Kept subtle and slow since every panel on top of it is glass/blurred.
(function () {
    if (typeof particlesJS === "undefined" || !document.getElementById("particles-background")) return;

    function buildConfig() {
        return {
            particles: {
                number: { value: 70, density: { enable: true, value_area: 900 } },
                color: { value: "#ffffff" },
                shape: { type: "circle" },
                opacity: {
                    value: 0.5,
                    random: true,
                    anim: { enable: true, speed: 0.5, opacity_min: 0.2, sync: false }
                },
                size: { value: 4, random: true },
                line_linked: { enable: true, distance: 140, color: "#4dabf7", opacity: 0.35, width: 1 },
                move: { enable: true, speed: 1, direction: "none", random: true, straight: false, out_mode: "out", bounce: false }
            },
            interactivity: {
                detect_on: "window",
                events: {
                    onhover: { enable: true, mode: "grab" },
                    onclick: { enable: false },
                    resize: true
                },
                modes: {
                    grab: { distance: 160, line_linked: { opacity: 0.5 } }
                }
            },
            retina_detect: true
        };
    }

    function init() {
        particlesJS("particles-background", buildConfig());

        // particles.js measures the container's size the instant it's called, and occasionally catches
        // it before layout has settled (canvas ends up with width/height 0 despite the container itself
        // being sized correctly). Firing a synthetic resize right after forces its own resize handler -
        // already wired up via interactivity.events.resize above - to re-measure and self-correct.
        window.dispatchEvent(new Event("resize"));
    }

    // Wait for full page load (fonts/CDN stylesheets settled, not just DOM parsed) before the first
    // measurement, then the resize-dispatch above is a safety net for anything that still slips through.
    if (document.readyState === "complete") {
        init();
    } else {
        window.addEventListener("load", init);
    }
})();
