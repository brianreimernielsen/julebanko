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
})();

(() => {
  const script = document.currentScript;
  const base = new URL("../img/nisse/", script.src);
  const boot = async () => {
    const stage = document.querySelector("[data-nisse]");
    if (!stage || stage.dataset.nisseReady) return;
    stage.dataset.nisseReady = "true";
    stage.setAttribute("aria-hidden", "true");
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    if (motion.matches) return;
    const actor = stage.querySelector("[data-nisse-actor]");
    if (!actor) return;
    actor.innerHTML =
      '<div class="nisse-inner"><div class="nisse-sprite"><div class="nisse-frame"></div><img class="nisse-tree-frame" alt="" hidden></div></div>';
    const frame = actor.querySelector(".nisse-frame");
    const water = actor.querySelector(".nisse-tree-frame");
    const files = [
      "nisse-actions.png",
      "nisse-walk.png",
      "nisse-wave.png",
      "nisse-polish.png",
      "tree-water-0.png",
      "tree-water-1.png",
      "tree-water-2.png",
      "tree-sprout-nisse.png",
      "tree-full.png",
      "snowman.png",
      "lantern.png",
      "nisse-ski.png",
      "scrooge.png",
      "heart.png",
      "gift.png",
    ];
    const assets = new Map();
    try {
      await Promise.all(
        files.map(async (file) => {
          const img = new Image();
          img.src = new URL(file, base).href;
          await img.decode();
          assets.set(file, img);
        })
      );
    } catch (error) {
      console.error("Nisse: et billede kunne ikke indlæses", error);
      return;
    }
    frame.style.backgroundImage = `url("${assets.get(files[0]).src}")`;
    const durations = { walk: 160, eat: 320, wave: 125, polish: 145, ski: 155 };
    const counts = { walk: 8, eat: 4, wave: 8, polish: 8, ski: 4 };
    const decorationJobs = ["tree", "snowman", "lantern", "heart", "gift"];
    const walkingJobs = ["eat", "wave", "polish", ...decorationJobs];
    const maxDecorations = 7;
    const decorationSpacing = 116;
    const decorations = [];
    const pick = (items) => items[Math.floor(Math.random() * items.length)];
    let forced = new URLSearchParams(location.search).get("nisse");
    forced = ({ water: "tree", porridge: "eat", cross: "walk" })[forced] || forced;
    if (!["walk", "ski", "visit", "scrooge", ...walkingJobs].includes(forced)) forced = null;

    let mode = "wait";
    let elapsed = 0;
    let prev = 0;
    let raf = 0;
    let x = 0;
    let direction = 1;
    let nextDirection = 1;
    let afterTurn = "move";
    let visit = [];
    let job = null;
    let stopRatio = 0.5;
    let plantRatio = null;
    let planted = false;
    let scrooge = null;
    let stolen = null;
    let dragStart = 0;
    const width = () => stage.clientWidth;
    const size = () => actor.getBoundingClientRect().width;
    const foot = () => size() / 3;
    const outside = (side) => (side < 0 ? -size() - 24 : width() + 24);
    const limit = () => Math.max(1, Math.min(maxDecorations, Math.floor(width() / decorationSpacing)));
    const jobTarget = () => stopRatio * width() - foot();
    const speed = forced ? 72 : 36;

    const showAction = (name, t) => {
      water.hidden = true;
      frame.hidden = false;
      // One cycle covers about 80 logical canvas pixels; speed and scale set the cadence.
      const frameMs =
        name === "walk" ? (((80 * size()) / 384 / speed) * 1000) / 8 : durations[name];
      const index = Math.floor(t / frameMs) % counts[name];
      frame.style.backgroundImage = `url("${assets.get(
        name === "eat" ? "nisse-actions.png" : `nisse-${name}.png`
      ).src}")`;
      const columns = name === "ski" ? 2 : 4;
      frame.style.backgroundSize = name === "eat" ? "400% 400%" : `${columns * 100}% 200%`;
      const row = name === "eat" ? 1 : Math.floor(index / columns);
      frame.style.backgroundPosition = `${((index % columns) * 100) / (columns - 1)}% ${
        name === "eat" ? 100 / 3 : row * 100
      }%`;
      frame.style.setProperty("--row", row);
      frame.style.setProperty("--col", index % columns);
      frame.style.setProperty("--nisse-facing", name === "walk" || name === "ski" ? direction : 1);
    };
    // Existing water images are intact; align the gnome's feet and body, not each PNG's bounding box.
    const waterGeometry = [
      [110, 320, 0.77],
      [117, 320, 0.77],
      [120, 313, 0.8],
      [112, 320, 0.77],
    ];
    const showWater = (index) => {
      frame.hidden = true;
      water.hidden = false;
      const key = index === 3 ? "tree-sprout-nisse.png" : `tree-water-${index}.png`;
      const [cx, ground, scale] = waterGeometry[index];
      water.src = assets.get(key).src;
      water.style.width = `${((332 * scale) / 384) * 100}%`;
      water.style.left = `${((128 - cx * scale) / 384) * 100}%`;
      water.style.top = `${((288 - ground * scale) / 320) * 100}%`;
    };
    const showFront = () => {
      water.hidden = true;
      frame.hidden = false;
      frame.style.backgroundImage = `url("${assets.get("nisse-actions.png").src}")`;
      frame.style.backgroundSize = "400% 400%";
      frame.style.backgroundPosition = "0% 66.666667%";
      frame.style.setProperty("--nisse-facing", 1);
    };

    const startMode = (next) => {
      mode = next;
      elapsed = 0;
    };
    const faceAndGo = (newDirection, next) => {
      if (newDirection === direction) {
        startMode(next);
        return;
      }
      nextDirection = newDirection;
      afterTurn = next;
      startMode("turn");
    };
    const clearForPlanting = (ratio) =>
      decorations.every(
        (item) =>
          Math.abs(item.ratio - ratio) * width() >= Math.min(decorationSpacing * 0.85, width() * 0.36)
      );
    const choosePlantSpot = () => {
      if (decorations.length >= limit()) return null;
      const count = limit();
      const candidates = Array.from({ length: count }, (_, i) => (i + 0.5) / count).filter(
        clearForPlanting
      );
      if (!decorations.length) return pick(candidates);
      let best = null;
      let bestGap = -1;
      for (const candidate of candidates) {
        const gap = Math.min(...decorations.map((item) => Math.abs(item.ratio - candidate)));
        if (gap > bestGap) {
          bestGap = gap;
          best = candidate;
        }
      }
      return best;
    };
    const fitDecorations = () => {
      const ordered = decorations.map((item) => item.ratio).sort((a, b) => a - b);
      const gaps = ordered.slice(1).map((ratio, i) => ratio - ordered[i]);
      const gap = Math.min(1, ...gaps);
      stage.style.setProperty("--nisse-decoration-width", `min(var(--nisse-width), ${gap * 240}vw)`);
    };
    const placeDecoration = (name, ratio) => {
      const element = document.createElement("div");
      element.className = `nisse-decoration nisse-decoration--${name}`;
      element.style.setProperty("--plant-x", `${ratio * 100}%`);
      const image = document.createElement("img");
      image.alt = "";
      image.src = assets.get(name === "tree" ? "tree-full.png" : `${name}.png`).src;
      element.append(image);
      stage.insertBefore(element, actor);
      decorations.push({ element, ratio });
      fitDecorations();
    };
    const nextStop = () => {
      job = visit.shift();
      if (!job) {
        const exitDirection = forced === "walk" ? -1 : Math.random() < 0.62 ? direction : -direction;
        faceAndGo(exitDirection, "exit");
        return;
      }
      planted = false;
      plantRatio = decorationJobs.includes(job) ? choosePlantSpot() : null;
      if (decorationJobs.includes(job) && plantRatio === null) job = pick(["wave", "eat", "polish"]);
      const footRatio = (x + foot()) / width();
      const stops = [0.18, 0.32, 0.48, 0.64, 0.8].filter((ratio) => Math.abs(ratio - footRatio) > 0.17);
      stopRatio =
        plantRatio === null ? pick(stops) : plantRatio - (0.565 - 1 / 3) * size() / width();
      if (job === "walk") stopRatio = 0.43;
      stopRatio = Math.max(0.035, Math.min(0.94, stopRatio));
      faceAndGo(Math.sign(jobTarget() - x) || direction, "move");
    };
    const startVisit = () => {
      direction = forced === "walk" ? 1 : forced === "visit" ? -1 : pick([-1, 1]);
      x = outside(-direction);
      if (forced === "ski" || (!forced && Math.random() < 0.15)) {
        startMode("ski");
        return;
      }
      if (forced === "visit") {
        visit = ["tree", "wave", "heart", "polish"];
      } else if (forced) {
        visit = [forced];
      } else {
        const choices = [...walkingJobs];
        const stops = 1 + Math.floor(Math.random() * 4);
        visit = [];
        for (let i = 0; i < stops; i++) {
          visit.push(choices.splice(Math.floor(Math.random() * choices.length), 1)[0]);
        }
      }
      nextStop();
    };
    const doJob = () => {
      if (job === "tree") {
        if (elapsed < 3100) showWater(elapsed < 2200 ? Math.floor(elapsed / 340) % 3 : 3);
        else {
          if (!planted) {
            placeDecoration("tree", plantRatio);
            planted = true;
          }
          if (elapsed < 4300) showFront();
          else showAction("wave", elapsed - 4300);
        }
        if (elapsed >= 6500) nextStop();
      } else if (decorationJobs.includes(job)) {
        showFront();
        if (elapsed < 850) {
          const lean = Math.sin((Math.PI * elapsed) / 850) * 12;
          frame.style.setProperty("--nisse-lean", `${lean}deg`);
        } else {
          if (!planted) {
            placeDecoration(job, plantRatio);
            planted = true;
          }
          if (elapsed >= 2100) showAction("wave", elapsed - 2100);
        }
        if (elapsed >= 3800) nextStop();
      } else if (elapsed >= durations[job] * counts[job] * (job === "eat" ? 3 : 2)) {
        nextStop();
      } else {
        showAction(job, elapsed);
      }
    };
    const startTheft = () => {
      stolen = decorations[0];
      direction = stolen.ratio < 0.5 ? 1 : -1;
      x = outside(-direction);
      scrooge = document.createElement("div");
      scrooge.className = "nisse-scrooge";
      scrooge.style.backgroundImage = `url("${assets.get("scrooge.png").src}")`;
      stage.append(scrooge);
      startMode("sneak");
    };
    const playTheft = (dt) => {
      if (mode === "sneak") {
        const goal = stolen.ratio * width() - foot() - direction * size() * 0.2;
        const distance = goal - x;
        x += Math.sign(distance) * Math.min(Math.abs(distance), (85 * dt) / 1000);
        if (Math.abs(distance) <= (85 * dt) / 1000) startMode("grab");
      } else if (mode === "grab" && elapsed >= 450) {
        direction = -direction;
        dragStart = x;
        stolen.element.classList.add("is-dragged");
        startMode("drag");
      } else if (mode === "drag") {
        x += (direction * 190 * dt) / 1000;
        stolen.element.style.setProperty("--drag-x", `${x - dragStart}px`);
        if (direction * (x - outside(direction)) >= 0) {
          stolen.element.remove();
          decorations.shift();
          fitDecorations();
          scrooge.remove();
          scrooge = null;
          startMode("pause");
          return;
        }
      }
      const dragging = mode === "drag";
      const col = mode === "grab" ? 1 : Math.floor(elapsed / (dragging ? 115 : 220)) % 2;
      scrooge.style.backgroundPosition = `${col * 100}% ${dragging ? 100 : 0}%`;
      scrooge.style.setProperty("--scrooge-x", `${x}px`);
      scrooge.style.setProperty("--scrooge-facing", direction);
    };
    const tick = (now) => {
      const dt = prev ? Math.min(now - prev, 80) : 0;
      prev = now;
      elapsed += dt;
      frame.style.setProperty("--nisse-lean", "0deg");
      actor.classList.toggle("is-skiing", mode === "ski");
      if (scrooge) {
        playTheft(dt);
      } else if (mode === "ski" || mode === "exit") {
        const skiing = mode === "ski";
        x += (direction * (skiing ? 150 : speed) * dt) / 1000;
        showAction(skiing ? "ski" : "walk", elapsed);
        if (direction * (x - outside(direction)) >= 0) startMode("wait");
      } else if (mode === "move") {
        const distance = jobTarget() - x;
        x += Math.sign(distance) * Math.min(Math.abs(distance), (speed * dt) / 1000);
        showAction("walk", elapsed);
        if (Math.abs(distance) <= (speed * dt) / 1000) {
          if (job === "walk") nextStop();
          else startMode("job");
        }
      } else if (mode === "job") {
        x = jobTarget();
        doJob();
      } else if (mode === "turn") {
        if (elapsed < 100) showAction("walk", 0);
        else if (elapsed < 245) showFront();
        else {
          direction = nextDirection;
          showAction("walk", 0);
        }
        if (elapsed >= 360) startMode(afterTurn);
      } else if (mode === "pause") {
        if (elapsed >= 2200) {
          if (forced === "scrooge") seedTheftDemo();
          else startVisit();
        }
      } else if (mode === "wait" && elapsed >= (forced ? 1100 : 4200)) {
        const enoughToRaid = decorations.length >= 3 && Math.random() < 0.48;
        const plotsFull = decorations.length > 0 && choosePlantSpot() === null;
        if (forced === "scrooge" || plotsFull || (!forced && enoughToRaid)) startTheft();
        else startVisit();
      }
      const elfOnScreen = ["move", "job", "turn", "exit", "ski"].includes(mode);
      actor.classList.toggle("is-active", elfOnScreen);
      actor.style.setProperty("--nisse-ski-direction", direction);
      actor.style.setProperty("--nisse-x", `${x}px`);
      raf = requestAnimationFrame(tick);
    };
    const seedTheftDemo = () => {
      if (!decorations.length) placeDecoration("tree", 0.27);
      if (decorations.length < 2 && limit() > 1) {
        const opposite = decorations[0].ratio < 0.5 ? 0.73 : 0.27;
        placeDecoration(pick(decorationJobs), opposite);
      }
      startMode("wait");
    };
    if (forced === "scrooge") seedTheftDemo();
    else startVisit();
    actor.style.setProperty("--nisse-x", `${x}px`);
    raf = requestAnimationFrame(tick);
    document.addEventListener("visibilitychange", () => {
      cancelAnimationFrame(raf);
      prev = 0;
      if (!document.hidden && !motion.matches) raf = requestAnimationFrame(tick);
    });
    motion.addEventListener("change", () => {
      cancelAnimationFrame(raf);
      prev = 0;
      stage.hidden = motion.matches;
      if (!motion.matches && !document.hidden) raf = requestAnimationFrame(tick);
    });
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot, { once: true });
  else boot();
})();
