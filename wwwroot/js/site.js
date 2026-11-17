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

  // Periodic Christmas nisse — 4-dir sprite, facing + hop walk, sit for julegrød.
  const stage = document.querySelector("[data-nisse]");
  if (stage && !reducedMotion) {
    const actor = stage.querySelector("[data-nisse-actor]");
    const rag = actor.querySelector(".nisse-rag");
    const bowl = actor.querySelector(".nisse-bowl");
    const scenes = ["cross", "look", "polish", "porridge"];
    let busy = false;
    let timer = 0;
    let raf = 0;

    const vw = () => window.innerWidth || 1200;
    const setPos = (x, y) => {
      actor.style.setProperty("--nisse-x", `${x}px`);
      actor.style.setProperty("--nisse-y", `${y}px`);
    };
    const setFace = (face) => actor.setAttribute("data-face", face);
    const setPose = (pose) => actor.setAttribute("data-pose", pose);
    const faceFromDelta = (dx, dy) => {
      if (Math.abs(dx) >= Math.abs(dy) * 0.55) {
        return dx >= 0 ? "right" : "left";
      }
      return dy < 0 ? "back" : "front";
    };
    const wait = (ms) => new Promise((resolve) => window.setTimeout(resolve, ms));
    const easeInOut = (t) => (t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2);

    const clearMotionClasses = () => {
      actor.classList.remove(
        "is-walking",
        "is-landing",
        "is-peeking",
        "is-polishing",
        "is-eating",
        "is-sitting"
      );
      if (rag) rag.hidden = true;
      if (bowl) bowl.hidden = true;
      setPose("stand");
    };

    const clearScene = () => {
      window.cancelAnimationFrame(raf);
      clearMotionClasses();
      actor.classList.remove("is-active");
      actor.removeAttribute("data-scene");
      setFace("front");
      setPos(-140, 0);
      busy = false;
    };

    const hopTo = (fromX, fromY, toX, toY, durationMs) =>
      new Promise((resolve) => {
        const hops = Math.max(3, Math.round(durationMs / 380));
        const start = performance.now();
        actor.classList.add("is-walking");
        actor.classList.remove("is-landing", "is-sitting");
        setPose("stand");

        const step = (now) => {
          const t = Math.min(1, (now - start) / durationMs);
          const e = easeInOut(t);
          const x = fromX + (toX - fromX) * e;
          const y = fromY + (toY - fromY) * e;
          // Soft multi-hop arc timed with travel (less mechanical than linear slide).
          const hopPhase = (t * hops) % 1;
          const hopY = -Math.sin(hopPhase * Math.PI) * 10;
          setPos(x, y + hopY);
          setFace(faceFromDelta(toX - fromX, toY - fromY));
          if (t < 1) {
            raf = window.requestAnimationFrame(step);
          } else {
            actor.classList.remove("is-walking");
            actor.classList.add("is-landing");
            setPos(toX, toY);
            window.setTimeout(() => actor.classList.remove("is-landing"), 220);
            resolve();
          }
        };
        raf = window.requestAnimationFrame(step);
      });

    const runCross = async () => {
      const startX = -140;
      const endX = vw() + 140;
      const y = 8 + Math.random() * 24;
      setPos(startX, y);
      actor.classList.add("is-active");
      await hopTo(startX, y, endX, y, 7800 + Math.random() * 1200);
    };

    const runLook = async () => {
      const enterX = vw() * 0.22;
      const peekX = vw() * 0.3;
      const exitX = vw() + 140;
      setPos(-140, 20);
      actor.classList.add("is-active");
      await hopTo(-140, 20, enterX, 8, 2200);
      await hopTo(enterX, 8, peekX, -6, 1400);
      setFace("front");
      actor.classList.add("is-peeking");
      await wait(3200);
      actor.classList.remove("is-peeking");
      await hopTo(peekX, -6, exitX, 12, 2800);
    };

    const runPolish = async () => {
      const spotX = vw() * 0.36;
      const exitX = vw() + 140;
      setPos(-140, 16);
      actor.classList.add("is-active");
      await hopTo(-140, 16, spotX, 14, 2600);
      setFace("front");
      if (rag) rag.hidden = false;
      actor.classList.add("is-polishing");
      await wait(3600);
      actor.classList.remove("is-polishing");
      if (rag) rag.hidden = true;
      await hopTo(spotX, 14, exitX, 10, 2600);
    };

    const runPorridge = async () => {
      // Walk in, sit with julegrød, eat, stand, walk out.
      const sitX = vw() * 0.42;
      const exitX = vw() + 140;
      setPos(-140, 18);
      actor.classList.add("is-active");
      await hopTo(-140, 18, sitX, 18, 2800);
      setFace("front");
      actor.classList.add("is-landing");
      await wait(180);
      actor.classList.remove("is-landing");
      setPose("sit");
      actor.classList.add("is-sitting");
      if (bowl) bowl.hidden = false;
      await wait(280);
      actor.classList.add("is-eating");
      await wait(4800);
      actor.classList.remove("is-eating");
      if (bowl) bowl.hidden = true;
      actor.classList.remove("is-sitting");
      setPose("stand");
      actor.classList.add("is-landing");
      await wait(260);
      actor.classList.remove("is-landing");
      await hopTo(sitX, 18, exitX, 12, 2800);
    };

    const runners = {
      cross: runCross,
      look: runLook,
      polish: runPolish,
      porridge: runPorridge,
    };

    const playScene = async (forced) => {
      if (busy || document.hidden) {
        scheduleNext();
        return;
      }

      busy = true;
      const scene = forced || scenes[Math.floor(Math.random() * scenes.length)];
      clearMotionClasses();
      actor.setAttribute("data-scene", scene);
      setFace("front");
      setPose("stand");

      try {
        await runners[scene]();
      } catch (_) {
        /* ignore aborted motion */
      }

      clearScene();
      scheduleNext();
    };

    const scheduleNext = () => {
      window.clearTimeout(timer);
      const waitMs = 14000 + Math.floor(Math.random() * 16000);
      timer = window.setTimeout(() => playScene(), waitMs);
    };

    timer = window.setTimeout(() => playScene(), 1800);

    // ?nisse=polish|look|porridge|cross
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
