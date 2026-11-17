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

  // Christmas nisse — Brian 8×3 sprite ark: continuous bottom patrol.
  const stage = document.querySelector("[data-nisse]");
  if (stage && !reducedMotion) {
    const actor = stage.querySelector("[data-nisse-actor]");
    const sprite = actor.querySelector(".nisse-sprite");
    const anims = {
      // Slow cozy walk — longer frame hold + low px/s travel
      walk: { frames: [[0, 0], [1, 0], [2, 0], [3, 0]], ms: 280 },
      eat: { frames: [[4, 0], [5, 0], [6, 0], [7, 0]], ms: 320 },
      wave: { frames: [[3, 1], [4, 1], [5, 1], [6, 1], [7, 1]], ms: 220 },
      polish: { frames: [[5, 2], [6, 2], [7, 2], [6, 2]], ms: 200 },
    };
    // Keep eat + stop-and-wave + back-view polish
    const actions = ["eat", "wave", "polish"];
    let frameTimer = 0;
    let raf = 0;
    let running = true;
    let x = null; // set after layout
    let dir = 1;
    let mode = "walk";
    let frameIdx = 0;
    let actionUntil = 0;
    let nextActionAt = 0;
    let actionAnchorX = 0;
    const speed = 14; // px/s — cozy curious visit, not a rush
    let approachBoost = 0; // temporary faster approach for demos only (still continuous)

    const vw = () => window.innerWidth || 1200;
    const nisseW = () => actor.offsetWidth || 55;

    const setFrame = (col, row) => {
      sprite.style.setProperty("--nisse-col", String(col));
      sprite.style.setProperty("--nisse-row", String(row));
    };

    const setFlip = (goingLeft) => {
      actor.style.setProperty("--nisse-flip", goingLeft ? "-1" : "1");
    };

    const setPos = (nx) => {
      x = nx;
      actor.style.setProperty("--nisse-x", `${Math.round(x)}px`);
    };

    const playFrames = (name) => {
      const anim = anims[name];
      frameIdx = 0;
      const tick = () => {
        if (!running || mode !== name) return;
        const [c, r] = anim.frames[frameIdx % anim.frames.length];
        setFrame(c, r);
        frameIdx += 1;
        frameTimer = window.setTimeout(tick, anim.ms);
      };
      window.clearTimeout(frameTimer);
      tick();
    };

    const startWalk = () => {
      mode = "walk";
      actor.setAttribute("data-scene", "walk");
      setFlip(dir < 0);
      // Keep current x — never teleport when resuming walk
      if (x != null) setPos(x);
      playFrames("walk");
    };

    const startAction = (name) => {
      mode = name;
      actor.setAttribute("data-scene", name);
      // Freeze world X while action plays (eat frames stay planted).
      actionAnchorX = x;
      setPos(actionAnchorX);
      if (name === "eat" || name === "wave") {
        actor.style.setProperty("--nisse-flip", "1");
      }
      playFrames(name);
      // Linger on wave/polish so the stop reads clearly
      const cycles = name === "eat" ? 6 : name === "wave" ? 7 : 10;
      actionUntil = performance.now() + anims[name].ms * anims[name].frames.length * cycles;
    };

    const scheduleAction = (now) => {
      nextActionAt = now + 7000 + Math.random() * 9000;
    };

    const trudge = (now, dt) => {
      const minX = -nisseW() * 0.4;
      const maxX = vw() - nisseW() * 0.6;
      const pxPerSec = approachBoost > 0 ? approachBoost : speed;
      let nx = x + dir * pxPerSec * (dt / 1000);
      if (nx >= maxX) {
        nx = maxX;
        dir = -1;
        setFlip(true);
      } else if (nx <= minX) {
        nx = minX;
        dir = 1;
        setFlip(false);
      }
      setPos(nx);

      if (now >= nextActionAt) {
        const pick = actions[Math.floor(Math.random() * actions.length)];
        startAction(pick);
      }
    };

    let last = performance.now();
    const loop = (now) => {
      if (!running) return;
      const dt = Math.min(48, now - last);
      last = now;

      if (mode === "walk") {
        trudge(now, dt);
      } else {
        // Hold exact sit/wave/polish position — no drift while frames cycle.
        setPos(actionAnchorX);
        if (now >= actionUntil) {
          startWalk();
          scheduleAction(now);
        }
      }

      raf = window.requestAnimationFrame(loop);
    };

    // Boot: enter from off-screen left, walk continuously (no mid-screen teleport).
    actor.classList.add("is-active");
    setPos(-nisseW() - 8);
    dir = 1;
    setFlip(false);
    startWalk();
    scheduleAction(performance.now() + 4500);
    raf = window.requestAnimationFrame(loop);

    // Demo: ?nisse=eat|wave|polish|walk — continue from current X (or walk in first).
    const params = new URLSearchParams(window.location.search);
    const forced = params.get("nisse");
    const map = {
      porridge: "eat",
      eat: "eat",
      look: "wave",
      wave: "wave",
      polish: "polish",
      cross: "walk",
      walk: "walk",
    };
    if (forced && map[forced]) {
      const want = map[forced];
      // Walk in continuously to ~40% then start action — never snap.
      window.setTimeout(() => {
        if (want === "walk") {
          scheduleAction(performance.now() + 25000);
          return;
        }
        const target = vw() * 0.4;
        approachBoost = 70; // continuous, just quicker for demos
        const arrive = () => {
          if (Math.abs(x - target) < 8) {
            approachBoost = 0;
            dir = 1;
            setFlip(false);
            setPos(target);
            startAction(want);
            const resume = () => {
              if (mode !== want) return;
              if (performance.now() < actionUntil) {
                window.setTimeout(resume, 200);
                return;
              }
              startWalk();
              scheduleAction(performance.now() + 8000);
            };
            window.setTimeout(resume, 200);
            return;
          }
          // Steer toward target without teleport
          dir = x < target ? 1 : -1;
          setFlip(dir < 0);
          if (mode !== "walk") startWalk();
          nextActionAt = performance.now() + 60000;
          window.setTimeout(arrive, 100);
        };
        nextActionAt = performance.now() + 60000;
        arrive();
      }, 200);
    }

    document.addEventListener("visibilitychange", () => {
      if (document.hidden) {
        running = false;
        window.cancelAnimationFrame(raf);
        window.clearTimeout(frameTimer);
      } else {
        running = true;
        last = performance.now();
        if (mode === "walk") startWalk();
        else playFrames(mode);
        raf = window.requestAnimationFrame(loop);
      }
    });
  } else if (stage && reducedMotion) {
    stage.remove();
  }
})();
