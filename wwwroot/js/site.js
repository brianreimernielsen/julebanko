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

  // Christmas nisse — visit: enter left → job → walk off RIGHT off-screen → return later.
  const stage = document.querySelector("[data-nisse]");
  if (stage && !reducedMotion) {
    const actor = stage.querySelector("[data-nisse-actor]");
    const sprite = actor.querySelector(".nisse-sprite");
    const arkAnims = {
      walk: { frames: [[0, 0], [1, 0], [2, 0], [3, 0]], ms: 280 },
      eat: { frames: [[4, 0], [5, 0], [6, 0], [7, 0]], ms: 320 },
      wave: { frames: [[3, 1], [4, 1], [5, 1], [6, 1], [7, 1]], ms: 220 },
      polish: { frames: [[5, 2], [6, 2], [7, 2], [6, 2]], ms: 200 },
    };
    // Tree/water strip indices (nisse-tree-water.png, 12 cols)
    const treeSeq = {
      // water ground → sprout → tree → linger
      frames: [
        [0, 420], // water-0
        [1, 420],
        [2, 420],
        [3, 500], // sprout with nisse
        [4, 400], // sprout only
        [9, 700], // full tree
        [6, 500], // idle linger
        [8, 500], // cheer
        [11, 900], // present with tree
      ],
    };
    const jobs = ["eat", "wave", "polish", "tree"];
    let frameTimer = 0;
    let raf = 0;
    let running = true;
    let x = 0;
    let mode = "enter"; // enter | job | exit | wait
    let job = "wave";
    let loopName = "";
    let frameIdx = 0;
    let actionAnchorX = 0;
    let phaseUntil = 0;
    const speed = 14; // cozy
    let approachBoost = 0;
    let demoForce = null;

    const vw = () => window.innerWidth || 1200;
    const nisseW = () => actor.offsetWidth || 55;
    const offLeft = () => -nisseW() - 20;
    const offRight = () => vw() + 20;

    const setSheet = (sheet) => {
      if (sheet === "tree") actor.setAttribute("data-sheet", "tree");
      else actor.removeAttribute("data-sheet");
    };

    const setFrame = (col, row) => {
      sprite.style.setProperty("--nisse-col", String(col));
      sprite.style.setProperty("--nisse-row", String(row || 0));
    };

    const setFlip = (goingLeft) => {
      actor.style.setProperty("--nisse-flip", goingLeft ? "-1" : "1");
    };

    const setPos = (nx) => {
      x = nx;
      actor.style.setProperty("--nisse-x", `${Math.round(x)}px`);
    };

    const stopFrames = () => window.clearTimeout(frameTimer);

    const playArkLoop = (name) => {
      setSheet("ark");
      loopName = name;
      const anim = arkAnims[name];
      frameIdx = 0;
      const tick = () => {
        if (!running || loopName !== name) return;
        const [c, r] = anim.frames[frameIdx % anim.frames.length];
        setFrame(c, r);
        frameIdx += 1;
        frameTimer = window.setTimeout(tick, anim.ms);
      };
      stopFrames();
      tick();
    };

    const playTreeSequence = () =>
      new Promise((resolve) => {
        setSheet("tree");
        setFlip(false);
        let i = 0;
        const step = () => {
          if (!running || mode !== "job" || job !== "tree") {
            resolve();
            return;
          }
          if (i >= treeSeq.frames.length) {
            resolve();
            return;
          }
          const [col, hold] = treeSeq.frames[i];
          setFrame(col, 0);
          i += 1;
          frameTimer = window.setTimeout(step, hold);
        };
        stopFrames();
        step();
      });

    const startEnter = (targetX, thenJob) => {
      mode = "enter";
      job = thenJob;
      actor.setAttribute("data-scene", "walk");
      setSheet("ark");
      setPos(offLeft());
      setFlip(false);
      actor.classList.add("is-active");
      playArkLoop("walk");
      actionAnchorX = targetX;
    };

    const startJob = async () => {
      mode = "job";
      actor.setAttribute("data-scene", job);
      setPos(actionAnchorX);
      setFlip(false);
      stopFrames();

      if (job === "tree") {
        await playTreeSequence();
        if (mode === "job") startExit();
        return;
      }

      playArkLoop(job);
      const cycles = job === "eat" ? 6 : job === "wave" ? 7 : 10;
      const anim = arkAnims[job];
      phaseUntil = performance.now() + anim.ms * anim.frames.length * cycles;
    };

    const startExit = () => {
      mode = "exit";
      actor.setAttribute("data-scene", "walk");
      setSheet("ark");
      setFlip(false); // always exit to the right
      // Demo only: a bit quicker exit so recordings can show off-screen leave
      if (demoForce) approachBoost = 48;
      playArkLoop("walk");
    };

    const startWait = () => {
      mode = "wait";
      stopFrames();
      actor.classList.remove("is-active");
      setPos(offLeft());
      const pause = 5000 + Math.random() * 8000;
      phaseUntil = performance.now() + pause;
    };

    const pickJob = () => jobs[Math.floor(Math.random() * jobs.length)];

    let last = performance.now();
    const loop = (now) => {
      if (!running) return;
      const dt = Math.min(48, now - last);
      last = now;
      const pxPerSec = approachBoost > 0 ? approachBoost : speed;

      if (mode === "enter") {
        const nx = x + pxPerSec * (dt / 1000);
        if (nx >= actionAnchorX) {
          setPos(actionAnchorX);
          approachBoost = 0;
          startJob();
        } else {
          setPos(nx);
        }
      } else if (mode === "job") {
        setPos(actionAnchorX);
        if (job !== "tree" && now >= phaseUntil) {
          startExit();
        }
      } else if (mode === "exit") {
        const nx = x + pxPerSec * (dt / 1000);
        if (nx >= offRight()) {
          setPos(offRight());
          startWait();
        } else {
          setPos(nx);
        }
      } else if (mode === "wait") {
        if (now >= phaseUntil) {
          startEnter(vw() * (0.32 + Math.random() * 0.2), pickJob());
        }
      }

      raf = window.requestAnimationFrame(loop);
    };

    // Boot visit
    actor.classList.add("is-active");
    startEnter(vw() * 0.4, pickJob());
    raf = window.requestAnimationFrame(loop);

    // Demo: ?nisse=eat|wave|polish|tree|walk
    const params = new URLSearchParams(window.location.search);
    const forced = params.get("nisse");
    const map = {
      porridge: "eat",
      eat: "eat",
      look: "wave",
      wave: "wave",
      polish: "polish",
      tree: "tree",
      water: "tree",
      cross: "walk",
      walk: "walk",
    };
    if (forced && map[forced]) {
      const want = map[forced];
      demoForce = want;
      window.setTimeout(() => {
        approachBoost = 70;
        if (want === "walk") {
          // Full traverse: enter mid-screen then exit right off-screen
          startEnter(vw() * 0.45, "wave");
          const watch = setInterval(() => {
            if (mode === "job") {
              clearInterval(watch);
              startExit();
            }
          }, 50);
          return;
        }
        startEnter(vw() * 0.4, want);
      }, 150);
    }

    document.addEventListener("visibilitychange", () => {
      if (document.hidden) {
        running = false;
        window.cancelAnimationFrame(raf);
        stopFrames();
      } else {
        running = true;
        last = performance.now();
        if (mode === "enter" || mode === "exit") playArkLoop("walk");
        else if (mode === "job" && job !== "tree") playArkLoop(job);
        raf = window.requestAnimationFrame(loop);
      }
    });
  } else if (stage && reducedMotion) {
    stage.remove();
  }
})();
