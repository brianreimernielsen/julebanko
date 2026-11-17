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
      '<div class="nisse-inner"><div class="nisse-sprite"><div class="nisse-frame"></div><img class="nisse-tree-frame" alt="" hidden><img class="nisse-grown-tree" alt="" hidden></div></div>';
    const frame = actor.querySelector(".nisse-frame");
    const water = actor.querySelector(".nisse-tree-frame");
    const tree = actor.querySelector(".nisse-grown-tree");
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
    tree.src = assets.get("tree-full.png").src;
    const durations = { walk: 160, eat: 320, wave: 125, polish: 145 };
    const counts = { walk: 8, eat: 4, wave: 8, polish: 8 };
    const jobs = ["eat", "wave", "polish", "tree"];
    let forced = new URLSearchParams(location.search).get("nisse");
    forced = ({ water: "tree", porridge: "eat", cross: "walk" })[forced] || forced;
    if (![ "walk", ...jobs ].includes(forced)) forced = null;
    let mode = "enter";
    let job = forced || jobs[Math.floor(Math.random() * jobs.length)];
    let x = 0;
    let elapsed = 0;
    let prev = 0;
    let raf = 0;
    let targetRatio = 0.4;
    const width = () => stage.clientWidth;
    const size = () => actor.getBoundingClientRect().width;
    // x tracks the left of a FIXED scene canvas. Foot anchor remains 1/3 across it.
    const target = () =>
      Math.max(8, Math.min(width() - size() - 8, width() * targetRatio - size() / 3));
    const showAction = (name, t) => {
      water.hidden = true;
      frame.hidden = false;
      const speed = forced ? 72 : 36;
      // One cycle covers about 80 logical canvas pixels; speed and scale set the cadence.
      const frameMs =
        name === "walk" ? (((80 * size()) / 384 / speed) * 1000) / 8 : durations[name];
      const index = Math.floor(t / frameMs) % counts[name];
      frame.style.backgroundImage = `url("${assets.get(
        name === "eat" ? "nisse-actions.png" : `nisse-${name}.png`
      ).src}")`;
      frame.style.backgroundSize = name === "eat" ? "400% 400%" : "400% 200%";
      const row = name === "eat" ? 1 : Math.floor(index / 4);
      frame.style.backgroundPosition = `${(index % 4) * 100 / 3}% ${
        name === "eat" ? 100 / 3 : row * 100
      }%`;
      frame.style.setProperty("--row", row);
      frame.style.setProperty("--col", index % 4);
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
      water.style.width = `${(332 * scale) / 384 * 100}%`;
      water.style.left = `${((128 - cx * scale) / 384) * 100}%`;
      water.style.top = `${((288 - ground * scale) / 320) * 100}%`;
    };
    const begin = (next) => {
      mode = next;
      elapsed = 0;
    };
    const tick = (now) => {
      const dt = prev ? Math.min(now - prev, 80) : 0;
      prev = now;
      elapsed += dt;
      const speed = forced ? 72 : 36;
      if (mode === "enter") {
        x = Math.min(target(), x + (speed * dt) / 1000);
        showAction("walk", elapsed);
        if (x >= target()) begin(forced === "walk" ? "exit" : "job");
      } else if (mode === "job") {
        x = target();
        if (job === "tree") {
          if (elapsed < 3800) showWater(elapsed < 2200 ? Math.floor(elapsed / 340) % 3 : 3);
          else showAction("wave", elapsed - 3800);
          tree.hidden = false;
          tree.classList.toggle("is-grown", elapsed >= 2600);
          if (elapsed >= 6500) begin("exit");
        } else {
          showAction(job, elapsed);
          if (elapsed >= durations[job] * counts[job] * (job === "eat" ? 5 : 4)) begin("exit");
        }
      } else if (mode === "exit") {
        // Keep the tree at the planting location while the gnome walks away.
        x += (speed * dt) / 1000;
        if (!tree.hidden) tree.style.translate = `${target() - x}px 0`;
        showAction("walk", elapsed);
        if (x > width() + size()) {
          actor.classList.remove("is-active");
          begin("wait");
        }
      } else if (elapsed >= (forced ? 1500 : 7000)) {
        tree.hidden = true;
        tree.classList.remove("is-grown");
        tree.style.translate = "";
        job = forced || jobs[Math.floor(Math.random() * jobs.length)];
        targetRatio = 0.25 + Math.random() * 0.4;
        x = -size() - 16;
        actor.classList.add("is-active");
        begin("enter");
      }
      actor.style.setProperty("--nisse-x", `${x}px`);
      raf = requestAnimationFrame(tick);
    };
    x = -size() - 16;
    actor.classList.add("is-active");
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
