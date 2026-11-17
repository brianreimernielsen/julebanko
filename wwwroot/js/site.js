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

  // Christmas nisse — Brian 8×3 sprite ark: bottom patrol + walk/eat/wave/polish.
  const stage = document.querySelector("[data-nisse]");
  if (stage && !reducedMotion) {
    const actor = stage.querySelector("[data-nisse-actor]");
    const sprite = actor.querySelector(".nisse-sprite");
    const anims = {
      walk: { frames: [[0, 0], [1, 0], [2, 0], [3, 0]], ms: 160 },
      eat: { frames: [[4, 0], [5, 0], [6, 0], [7, 0]], ms: 320 },
      wave: { frames: [[3, 1], [4, 1], [5, 1], [6, 1], [7, 1]], ms: 220 },
      polish: { frames: [[5, 2], [6, 2], [7, 2], [6, 2]], ms: 200 },
    };
    const actions = ["eat", "wave", "polish"];
    let frameTimer = 0;
    let raf = 0;
    let running = true;
    let x = -120;
    let dir = 1; // 1 = right, -1 = left
    let mode = "walk";
    let frameIdx = 0;
    let actionUntil = 0;
    let nextActionAt = 0;

    const vw = () => window.innerWidth || 1200;
    const nisseW = () => actor.offsetWidth || 95;

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
      playFrames("walk");
    };

    const startAction = (name) => {
      mode = name;
      actor.setAttribute("data-scene", name);
      // eat/wave face forward (no flip); polish is back-view baked
      if (name === "eat" || name === "wave") {
        actor.style.setProperty("--nisse-flip", "1");
      }
      playFrames(name);
      const cycles = name === "eat" ? 6 : name === "wave" ? 5 : 8;
      actionUntil = performance.now() + anims[name].ms * anims[name].frames.length * cycles;
    };

    const scheduleAction = (now) => {
      nextActionAt = now + 9000 + Math.random() * 11000;
    };

    const trudge = (now, dt) => {
      // Slow ground trudge (~28–38 px/s), no hop arcs.
      const speed = 30 + Math.random() * 4;
      const minX = -nisseW() * 0.35;
      const maxX = vw() - nisseW() * 0.65;
      let nx = x + dir * speed * (dt / 1000);
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
      } else if (now >= actionUntil) {
        startWalk();
        scheduleAction(now);
      }

      raf = window.requestAnimationFrame(loop);
    };

    // Boot
    actor.classList.add("is-active");
    setPos(-nisseW());
    dir = 1;
    startWalk();
    scheduleAction(performance.now() + 2500);
    raf = window.requestAnimationFrame(loop);

    // Demo overrides: ?nisse=eat|wave|polish|walk|porridge|look|cross
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
      window.setTimeout(() => {
        setPos(vw() * 0.42);
        if (map[forced] === "walk") {
          startWalk();
          scheduleAction(performance.now() + 20000);
        } else {
          startAction(map[forced]);
          // After forced action, resume patrol
          const resume = () => {
            if (mode !== map[forced]) return;
            if (performance.now() < actionUntil) {
              window.setTimeout(resume, 200);
              return;
            }
            startWalk();
            scheduleAction(performance.now() + 8000);
          };
          window.setTimeout(resume, 200);
        }
      }, 350);
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
