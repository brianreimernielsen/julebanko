(() => {
  document.querySelectorAll("[data-confirm]").forEach((el) => {
    el.addEventListener("click", (e) => {
      const msg = el.getAttribute("data-confirm");
      if (msg && !window.confirm(msg)) {
        e.preventDefault();
      }
    });
  });

  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const flakeCounts = { 1: 12, 2: 18, 3: 24, 4: 32, 5: 40 };

  document.querySelectorAll("[data-snow]").forEach((layer) => {
    if (reducedMotion) {
      layer.remove();
      return;
    }

    const intensity = Math.min(5, Math.max(1, Number(layer.getAttribute("data-intensity") || "1")));
    layer.setAttribute("data-intensity", String(intensity));
    const count = flakeCounts[intensity] || 12;
    const frag = document.createDocumentFragment();

    for (let i = 0; i < count; i++) {
      const flake = document.createElement("span");
      const size = 2 + ((i * 7) % 4);
      const left = ((i * 37) % 100) + (i % 3) * 0.4;
      const durationScale = 0.75 + ((i * 13) % 50) / 100;
      const delay = -((i * 1.7) % 12);
      flake.style.left = `${left}%`;
      flake.style.width = `${size}px`;
      flake.style.height = `${size}px`;
      flake.style.animationDuration = `calc(var(--snow-duration) * ${durationScale})`;
      flake.style.animationDelay = `${delay}s`;
      frag.appendChild(flake);
    }

    layer.replaceChildren(frag);
  });

  // Periodic Christmas nisse (elf) scenarios on the player page.
  const stage = document.querySelector("[data-nisse]");
  if (stage && !reducedMotion) {
    const actor = stage.querySelector("[data-nisse-actor]");
    const scenes = ["cross", "look", "polish", "porridge"];
    let busy = false;
    let timer = 0;

    const clearScene = () => {
      actor.classList.remove("is-active", "is-flip");
      actor.removeAttribute("data-scene");
      busy = false;
    };

    const playScene = (forced) => {
      if (busy || document.hidden) {
        scheduleNext();
        return;
      }

      busy = true;
      const scene = forced || scenes[Math.floor(Math.random() * scenes.length)];
      const flip = Math.random() > 0.55;
      actor.classList.toggle("is-flip", flip);
      actor.setAttribute("data-scene", scene);
      actor.classList.add("is-active");

      const onEnd = () => {
        actor.removeEventListener("animationend", onEnd);
        clearScene();
        scheduleNext();
      };
      actor.addEventListener("animationend", onEnd);
    };

    const scheduleNext = () => {
      window.clearTimeout(timer);
      const waitMs = 14000 + Math.floor(Math.random() * 16000);
      timer = window.setTimeout(() => playScene(), waitMs);
    };

    // First visit: appear soon so the page feels alive (and screenshots can catch it).
    timer = window.setTimeout(() => playScene(), 1800);

    // Optional forced scene for demos: ?nisse=polish|look|porridge|cross
    const params = new URLSearchParams(window.location.search);
    const forced = params.get("nisse");
    if (forced && scenes.includes(forced)) {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => playScene(forced), 400);
    }
  } else if (stage && reducedMotion) {
    stage.remove();
  }
})();
