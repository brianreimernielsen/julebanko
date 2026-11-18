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
  const base = new URL('../img/nisse/', script.src);
  const boot = async () => {
    const stage = document.querySelector('[data-nisse]');
    if (!stage || stage.dataset.nisseReady) return;
    stage.dataset.nisseReady = 'true';
    stage.setAttribute('aria-hidden','true');
    const motion = matchMedia('(prefers-reduced-motion: reduce)');
    if (motion.matches) return;
    const actor = stage.querySelector('[data-nisse-actor]');
    if (!actor) return;
    actor.innerHTML = '<div class="nisse-inner"><div class="nisse-sprite"><div class="nisse-frame"></div></div></div>';
    const frame = actor.querySelector('.nisse-frame');
    const giftSprites = ['gift.png','gift-red.png','gift-blue.png','gift-gold.png'];
    const heartSprites = ['heart-ornament.png','heart-red-gold.png','heart-green-white.png','heart-green-gold.png'];
    const files = ['nisse-actions.png','nisse-walk.png','nisse-wave.png','nisse-polish.png','nisse-watering-v2.png','nisse-backwork.png','tree-frost-v1.png','snow-body.png','snow-hat-nordic.png','snow-scarf-nordic.png','snow-scarf-tail.png','snow-carrot.png','lantern.png','nisse-ski.png','scrooge-walk-v2.png','scrooge-kneel.png','heart-branch.png','nisse-heart-kneel.png','nisse-gift-magic.png','goat.png','nisse-goat-ride.png','nisse-meeting-v1.png','nisse-partner-walk-v2.png','nisse-couple-walk-v2.png',...heartSprites,...giftSprites];
    const assets = new Map();
    try {
      await Promise.all(files.map(async file => {
        const img = new Image(); img.src = new URL(file,base).href;
        await img.decode(); assets.set(file,img);
      }));
    } catch(error) { console.error('Nisse: et billede kunne ikke indlæses',error); return; }
    frame.style.backgroundImage = `url("${assets.get(files[0]).src}")`;
    const durations = {walk:160,eat:320,wave:125,polish:145,ski:155};
    const counts = {walk:8,eat:4,wave:8,polish:8,ski:4};
    const decorationJobs = ['tree','snowman','lantern','heart','gift'];
    const walkingJobs = ['wave','polish',...decorationJobs];
    const maxDecorations = 7;
    const decorationSpacing = 116;
    const decorations = [];
    const decorationCount = new Map();
    const pick = (items)=>items[Math.floor(Math.random()*items.length)];
    const mealInterval=5*60_000;
    const mealDuration=15_000;
    // Schedule up to a minute early so an ongoing scene can finish before eight minutes.
    const skiDelay=()=>(4+Math.random()*3)*60_000;
    let activeTime=0, nextMealAt=mealInterval, nextSkiAt=skiDelay();
    const skiDue=()=>!forced&&activeTime>=nextSkiAt;
    let forced = new URLSearchParams(location.search).get('nisse');
    forced = ({water:'tree',porridge:'eat',cross:'walk'})[forced] || forced;
    if(!['walk','eat','ski','visit','scrooge','scrooge-flugt','scrooge-kaede','scrooge-alle','scrooge-snowman','landscape','gifts','snowmen','hearts','goat','meeting',...walkingJobs].includes(forced)) forced=null;

    const scroogeDemo=['scrooge','scrooge-flugt','scrooge-kaede','scrooge-alle','scrooge-snowman'].includes(forced);
    let mode='wait', elapsed=0, prev=0, raf=0, x=0;
    let direction=1, nextDirection=1, afterTurn='move';
    let visit=[], job=null, stopRatio=.5, plantRatio=null, planted=false;
    let work=null, treeFacing=1, polishTarget=null, polishFacing=1;
    let goatRide=null, meeting=null;
    let nextMeetingAt=120_000;
    const treeVisitInterval=4;
    let visitsSinceTree=treeVisitInterval-1;
    let scrooge=null, stolen=null, dragStart=0, towChain=null, testPropIndex=0;
    let retreat=null, retryPending=false, interruptAttempt=false;
    let nextInterruptionAt=45_000;
    const width=()=>stage.clientWidth;
    const size=()=>actor.getBoundingClientRect().width;
    const foot=()=>size()/3;
    const outside=(side)=>side<0?-size()-24:width()+24;
    const limit=()=>Math.max(1,Math.min(maxDecorations,Math.floor(width()/decorationSpacing)));
    const jobTarget=()=>stopRatio*width()-foot();
    const speed=forced?108:54;

    const showAction=(name,t)=>{
      frame.hidden=false;
      // One cycle covers about 80 logical canvas pixels; speed and scale set the cadence.
      const frameMs=name==='walk'?(80*size()/384/speed*1000/8):durations[name];
      const index=Math.floor(t/frameMs)%counts[name];
      frame.style.backgroundImage=`url("${assets.get(name==='eat'?'nisse-actions.png':`nisse-${name}.png`).src}")`;
      const columns=name==='ski'?2:4;
      frame.style.backgroundSize=name==='eat'?'400% 400%':`${columns*100}% 200%`;
      const row=name==='eat'?1:Math.floor(index/columns);
      frame.style.backgroundPosition=`${(index%columns)*100/(columns-1)}% ${name==='eat'?100/3:row*100}%`;
      frame.style.setProperty('--row',row);
      frame.style.setProperty('--col',index%columns);
      frame.style.setProperty('--nisse-facing',name==='polish'?polishFacing:(name==='walk'||name==='ski'?direction:1));
    };
    const showWorkshopPose=(sprite,index,columns,facing)=>{
      frame.hidden=false;
      frame.style.backgroundImage=`url("${assets.get(sprite).src}")`;
      frame.style.backgroundSize=`${columns*100}% 100%`;
      frame.style.backgroundPosition=columns===1?'0% 0%':`${index*100/(columns-1)}% 0%`;
      frame.style.setProperty('--nisse-facing',facing);
    };
    const showFront=()=>{
      frame.hidden=false;
      frame.style.backgroundImage=`url("${assets.get('nisse-actions.png').src}")`;
      frame.style.backgroundSize='400% 400%';
      frame.style.backgroundPosition='0% 66.666667%';
      frame.style.setProperty('--nisse-facing',1);
    };

    const startMode=(next)=>{
      mode=next;
      elapsed=0;
    };
    const faceAndGo=(newDirection,next)=>{
      if(newDirection===direction) {
        startMode(next);
        return;
      }
      nextDirection=newDirection;
      afterTurn=next;
      startMode('turn');
    };
    const clearForPlanting=(ratio)=>decorations.every(item=>
      Math.abs(item.ratio-ratio)*width()>=Math.min(decorationSpacing*.85,width()*.36));
    const choosePlantSpot=(preferredRatio=null)=>{
      if(decorations.length>=limit()) return null;
      const count=limit();
      const candidates=Array.from({length:count},(_,i)=>(i+.5)/count).filter(clearForPlanting);
      if(!decorations.length){
        if(preferredRatio===null) return pick(candidates);
        return candidates.reduce((closest,ratio)=>
          Math.abs(ratio-preferredRatio)<Math.abs(closest-preferredRatio)?ratio:closest);
      }
      let best=null;
      let bestGap=-1;
      for(const candidate of candidates){
        const gap=Math.min(...decorations.map(item=>Math.abs(item.ratio-candidate)));
        if(gap>bestGap){
          bestGap=gap;
          best=candidate;
        }
      }
      return best;
    };
    const fitDecorations=()=>{
      const ordered=decorations.map(item=>item.ratio).sort((a,b)=>a-b);
      const gaps=ordered.slice(1).map((ratio,i)=>ratio-ordered[i]);
      const gap=Math.min(1,...gaps);
      stage.style.setProperty('--nisse-decoration-width',`min(var(--nisse-width), ${gap*240}vw)`);
    };
    // Normalized edge heights of snow-banner.png at equal horizontal intervals.
    const snowEdgeHeights = [
      0.932, 0.951, 1.002, 1.034, 1.038, 1.016, 0.971, 0.934, 0.932,
      0.953, 0.975, 0.973, 0.967, 1.002, 1.036, 1.053, 1.051, 1.026,
      0.985, 0.953, 0.936, 0.955, 0.979, 0.985, 0.969, 0.936, 0.92,
      0.932, 0.975, 1.008, 1.02, 1.004, 0.963
    ];
    const snowEdgeHeight=(ratio)=>{
      const position=ratio*(snowEdgeHeights.length-1);
      const left=Math.floor(position);
      const right=Math.min(left+1,snowEdgeHeights.length-1);
      return snowEdgeHeights[left]+(snowEdgeHeights[right]-snowEdgeHeights[left])*(position-left);
    };
    const placeDecoration=(name,ratio,depth)=>{
      const element=document.createElement('div');
      const count=decorationCount.get(name)||0;
      decorationCount.set(name,count+1);
      const smallProp=name==='gift'||name==='lantern';
      const layer=smallProp?'snow-front':depth||(count%2===0?'snow-front':'snow-back');
      element.className=`nisse-decoration nisse-decoration--${name} ${layer}`;
      if(smallProp&&count%2===1){
        element.dataset.size='small';
        element.style.setProperty('--nisse-edge-ratio',snowEdgeHeight(ratio).toFixed(3));
      }
      element.style.setProperty('--plant-x',`${ratio*100}%`);
      const image=document.createElement('img');
      image.alt='';
      let spriteName=`${name}.png`;
      if(name==='tree') spriteName='tree-frost-v1.png';
      if(name==='gift') spriteName=giftSprites[count%giftSprites.length];
      if(name==='heart') spriteName='heart-branch.png';
      if(name==='snowman') spriteName='snow-body.png';
      image.src=assets.get(spriteName).src;
      element.append(image);
      if(name==='lantern'){
        const flame=document.createElement('span');
        flame.className='nisse-lantern-flame';
        element.append(flame);
      }
      if(name==='heart'){
        const ornament=document.createElement('img');
        ornament.alt='';
        ornament.className='nisse-heart-ornament';
        ornament.src=assets.get(heartSprites[count%heartSprites.length]).src;
        element.append(ornament);
      }
      if(name==='snowman'){
        element.dataset.variant=count%3;
        element.dataset.facing=count%2===0?'right':'left';
        if(job!=='snowman') element.classList.add('is-dressed');
        for(const piece of ['scarf','carrot','hat']){
          const accessory=document.createElement('img');
          accessory.alt='';
          accessory.className=`nisse-snow-${piece}`;
          const accessoryFile=piece==='carrot'?'snow-carrot.png':`snow-${piece}-nordic.png`;
          accessory.src=assets.get(accessoryFile).src;
          element.append(accessory);
        }
      }
      stage.insertBefore(element,actor);
      decorations.push({element,ratio,name});
      fitDecorations();
      return element;
    };
    const nextStop=()=>{
      const leavingForSki=skiDue();
      job=leavingForSki?null:visit.shift();
      if(!job){
        const exitDirection=leavingForSki?(x+foot()<width()/2?-1:1):
          forced==='walk'||forced==='scrooge-flugt'?-direction:(Math.random()<.62?direction:-direction);
        faceAndGo(exitDirection,'exit');
        return;
      }
      planted=false;
      work=null;
      const firstTree=job==='tree'&&!decorationCount.has('tree');
      const preferredRatio=firstTree?(direction>0?.23:.77):null;
      plantRatio=decorationJobs.includes(job)?choosePlantSpot(preferredRatio):null;
      if(decorationJobs.includes(job)&&plantRatio===null) job='polish';
      polishTarget=job==='polish'?pick(decorations.filter(item=>item.name==='tree'||item.name==='lantern')):null;
      if(job==='polish'&&!polishTarget) job='wave';
      const footRatio=(x+foot())/width();
      const stops=[.18,.32,.48,.64,.80].filter(ratio=>Math.abs(ratio-footRatio)>.17);
      stopRatio=plantRatio===null?pick(stops):plantRatio-(.565-1/3)*size()/width();
      if(job==='tree'){
        treeFacing=plantRatio<.28?-1:1;
        stopRatio=plantRatio-treeFacing*(.565-1/3)*size()/width();
      }
      if((job==='snowman'||job==='lantern'||job==='heart')&&plantRatio!==null){
        const approach=plantRatio<.28?1:-1;
        stopRatio=plantRatio+approach*size()*.20/width();
      }
      if(job==='gift'&&plantRatio!==null){
        const approach=plantRatio<.28?1:-1;
        stopRatio=plantRatio+approach*size()*.40/width();
      }
      if(polishTarget){
        polishFacing=polishTarget.ratio>.5?1:-1;
        stopRatio=polishTarget.ratio-polishFacing*size()*.22/width();
      }
      if(job==='walk') stopRatio=.43;
      if(forced==='scrooge-flugt') stopRatio=direction>0?.17:.83;
      stopRatio=Math.max(.035,Math.min(.94,stopRatio));
      faceAndGo(Math.sign(jobTarget()-x)||direction,'move');
    };
    const startGoatRide=(ratio)=>{
      goatRide={ratio,facing:direction,side:ratio>.80?-1:ratio<.20?1:direction};
      startMode('ride');
    };
    const rideGoat=(dt)=>{
      const target=goatRide.ratio*width()-size()/2;
      const distance=target-x;
      const rideSpeed=forced?145:99;
      x+=Math.sign(distance)*Math.min(Math.abs(distance),rideSpeed*dt/1000);
      const index=Math.floor(elapsed/135)%4;
      showWorkshopPose('nisse-goat-ride.png',index,4,goatRide.facing);
      frame.style.setProperty('--nisse-hop',`${Math.sin(elapsed/86)*1.25}px`);
      if(Math.abs(distance)<=rideSpeed*dt/1000){
        const goat=placeDecoration('goat',goatRide.ratio,'snow-back');
        goat.style.setProperty('--goat-facing',goatRide.facing);
        startMode('dismount');
      }
    };
    const dismountGoat=()=>{
      const {ratio,side}=goatRide;
      const progress=Math.min(1,elapsed/740);
      const hop=Math.sin(Math.PI*progress);
      x=ratio*width()-foot()+side*size()*(.03+.34*progress);
      if(elapsed<740){
        showFront();
        frame.style.setProperty('--nisse-hop',`${-size()*(.12*(1-progress)+.09*hop)}px`);
      }else{
        showAction('wave',elapsed-740);
      }
      if(elapsed>=1740){
        goatRide=null;
        nextStop();
      }
    };
    const startMeeting=()=>{
      const figure=document.createElement('div');
      figure.className='nisse-meeting';
      figure.style.backgroundImage=`url("${assets.get('nisse-partner-walk-v2.png').src}")`;
      figure.style.backgroundSize='400% 200%';
      figure.style.setProperty('--meeting-facing',direction);
      const heart=document.createElement('span');
      heart.className='nisse-meeting-heart';
      heart.textContent='♥';
      figure.append(heart);
      meeting={figure,heart,side:direction,partnerX:width()-(x+foot())-size()/2,pairX:0};
      figure.style.setProperty('--meeting-x',`${meeting.partnerX}px`);
      stage.insertBefore(figure,actor);
      nextMeetingAt=activeTime+180_000;
      startMode('meet-approach');
    };
    const moveToward=(current,target,distance)=>current+Math.sign(target-current)*Math.min(Math.abs(target-current),distance);
    const placeMeetingFigure=(position)=>{
      meeting.figure.style.setProperty('--meeting-x',`${position}px`);
    };
    const showMeetingGait=(position,walkSpeed,stride)=>{
      // Eight pictures per full gait cycle. Scale the cadence with the walking speed.
      const cycleMs=stride*size()/384/walkSpeed*1000;
      const index=Math.floor(elapsed/cycleMs*8)%8;
      meeting.figure.style.backgroundPosition=`${index%4*100/3}% ${Math.floor(index/4)*100}%`;
      placeMeetingFigure(position);
    };
    const playMeeting=(dt)=>{
      const {figure,heart,side}=meeting;
      const center=width()/2;
      if(mode==='meet-approach'){
        const step=speed*dt/1000;
        const maleTarget=center-side*size()*.13-foot();
        const femaleTarget=center+side*size()*.13-size()/2;
        x=moveToward(x,maleTarget,step);
        meeting.partnerX=moveToward(meeting.partnerX,femaleTarget,step);
        showAction('walk',elapsed);
        showMeetingGait(meeting.partnerX,speed,114);
        if(Math.abs(x-maleTarget)<.01&&Math.abs(meeting.partnerX-femaleTarget)<.01){
          meeting.pairX=center-size()/2;
          figure.style.backgroundImage=`url("${assets.get('nisse-meeting-v1.png').src}")`;
          figure.style.backgroundSize='200% 200%';
          figure.classList.add('is-kissing');
          figure.style.backgroundPosition='100% 0%';
          placeMeetingFigure(meeting.pairX);
          startMode('meet-kiss');
        }
      }else if(mode==='meet-kiss'){
        meeting.pairX=center-size()/2;
        placeMeetingFigure(meeting.pairX);
        if(elapsed>=1600){
          figure.classList.remove('is-kissing');
          heart.remove();
          figure.style.backgroundImage=`url("${assets.get('nisse-couple-walk-v2.png').src}")`;
          figure.style.backgroundSize='400% 200%';
          figure.style.backgroundPosition='0% 0%';
          startMode('meet-exit');
        }
      }else{
        const pairSpeed=speed*.86;
        meeting.pairX+=side*pairSpeed*dt/1000;
        showMeetingGait(meeting.pairX,pairSpeed,104);
        if(side*(meeting.pairX-outside(side))>=0){
          figure.remove();
          meeting=null;
          startMode('wait');
        }
      }
    };
    const startVisit=(entryDirection=null)=>{
      direction=entryDirection??(forced==='walk'?1:forced==='visit'?-1:pick([-1,1]));
      x=outside(-direction);
      if(!forced) visitsSinceTree++;
      const treeDue=!forced&&visitsSinceTree>=treeVisitInterval;
      if(forced==='ski'||skiDue()){
        nextSkiAt=activeTime+skiDelay();
        startMode('ski');
        return;
      }
      const meetingBudget=(width()/2+size())/speed*(1+1/.86)*1000+5800;
      const canMeet=!forced&&entryDirection===null&&!treeDue&&!retryPending&&!retreat
        &&activeTime>=nextMeetingAt&&Math.min(nextSkiAt,nextMealAt)-activeTime>meetingBudget;
      if(forced==='meeting'||(canMeet&&Math.random()<.20)){
        startMeeting();
        return;
      }
      if(forced==='goat'){
        visit=[];
        const available=choosePlantSpot(direction>0?.33:.67);
        if(available!==null){
          startGoatRide(available);
          return;
        }
        visit=['wave'];
      }else if(forced==='scrooge-flugt'){
        visit=['wave'];
      }else if(forced==='visit'){
        visit=['tree','wave','heart','polish'];
      }else if(forced){
        visit=[forced];
      }else{
        const choices=treeDue?walkingJobs.filter(name=>name!=='tree'):[...walkingJobs];
        const stops=1+Math.floor(Math.random()*4);
        visit=treeDue?['tree']:[];
        if(activeTime>=nextMealAt) visit.push('eat');
        while(visit.length<stops){
          const available=choosePlantSpot()===null?[]:choices.filter(name=>decorationJobs.includes(name));
          const next=pick(available.length&&Math.random()<.78?available:choices);
          visit.push(next);
          choices.splice(choices.indexOf(next),1);
        }
        const canRide=!treeDue&&activeTime<nextMealAt&&!retryPending&&!retreat;
        if(canRide&&Math.random()<.19){
          const available=choosePlantSpot(direction>0?.33:.67);
          if(available!==null){
            visit.pop();
            startGoatRide(available);
            return;
          }
        }
      }
      nextStop();
    };
    const startSnowman=()=>{
      const element=placeDecoration('snowman',plantRatio);
      element.classList.add('is-building');
      const pieces=document.createElement('div');
      pieces.className='nisse-snowman-build';
      for(const name of ['base','middle','head']){
        const part=document.createElement('span');
        part.className=`snow-${name} nisse-snowman-piece`;
        pieces.append(part);
      }
      element.append(pieces);
      return {element,pieces,approach:plantRatio<.28?1:-1};
    };
    const buildSnowman=()=>{
      if(!work) work=startSnowman();
      const {element,pieces,approach}=work;
      const center=plantRatio*width()-foot();
      if(elapsed<6700){
        const beat=elapsed<3600?elapsed%1200:0;
        const pose=elapsed<3600?(beat<640?0:1):elapsed<4500?1:2;
        x=center+approach*size()*(elapsed<3600?.15:.23);
        showWorkshopPose('nisse-backwork.png',pose,3,-approach);
        if(elapsed>=3550&&element.classList.contains('is-building')){
          pieces.remove();
          element.classList.remove('is-building');
        }
        if(elapsed>=3950) element.classList.add('has-scarf');
        if(elapsed>=4750) element.classList.add('has-carrot');
        if(elapsed>=5530) element.classList.add('has-hat');
      }else{
        x=center+approach*size()*.26;
        showAction('wave',elapsed-6700);
        if(elapsed>=8100) nextStop();
      }
    };
    const plantTree=()=>{
      const wateringEnd=2200;
      const growthStart=wateringEnd+2500;
      const growthEnd=growthStart+1650;
      direction=treeFacing;
      const watering=elapsed<wateringEnd;
      showWorkshopPose('nisse-watering-v2.png',watering?Math.floor(elapsed/340)%3:3,4,treeFacing);
      if(!watering&&!work){
        const element=placeDecoration('tree',plantRatio);
        element.classList.add('is-sprout');
        work={element};
      }
      if(elapsed>=growthStart&&!planted){
        work.element.classList.remove('is-sprout');
        planted=true;
        visitsSinceTree=0;
      }
      if(elapsed>=growthEnd+1600) nextStop();
    };
    const setLantern=()=>{
      const approach=plantRatio<.28?1:-1;
      const matchStart=1320;
      const contactAt=2160;
      const candleOn=contactAt+3500;
      const matchEnd=candleOn+570;
      const waveAt=matchEnd+290;
      x=plantRatio*width()-foot()+approach*size()*.23;
      if(!work){
        const element=placeDecoration('lantern',plantRatio,'snow-front');
        element.classList.add('is-unlit');
        element.classList.add('is-setting');
        element.style.setProperty('--lantern-approach',approach);
        const match=document.createElement('span');
        match.className='nisse-lantern-match';
        element.append(match);
        work={element,match};
      }
      if(elapsed<waveAt){
        showWorkshopPose('nisse-backwork.png',elapsed<250||elapsed>=matchEnd?1:0,3,-approach);
        if(elapsed>=matchStart&&elapsed<matchEnd){
          const nearGlass=elapsed<candleOn
            ? Math.max(0,1-(elapsed-matchStart)/(contactAt-matchStart))
            : Math.min(1,(elapsed-candleOn)/(matchEnd-candleOn));
          work.match.classList.add('is-visible');
          work.match.style.setProperty('--match-reach',nearGlass);
        }else{
          work.match.classList.remove('is-visible');
        }
        if(elapsed>=candleOn) work.element.classList.remove('is-unlit');
      }else{
        showAction('wave',elapsed-waveAt);
      }
      if(elapsed>=waveAt+1680){
        work.match.remove();
        nextStop();
      }
    };
    // Position of the pinching mitten in the kneeling 384 × 320 sprite.
    const heartGrip={x:191,y:190};
    const heartHook={x:.626,fromBottom:.411};
    const hangHeart=()=>{
      const approach=plantRatio<.28?1:-1;
      const facing=-approach;
      const hookAt=1510;
      const releaseAt=2100;
      const standAt=2700;
      if(!work){
        const element=placeDecoration('heart',plantRatio,'snow-front');
        element.classList.add('is-hanging');
        work={element,released:false};
      }

      if(elapsed<releaseAt){
        const frameSize=size();
        const scale=frameSize/384;
        if(work.width!==width()||work.height!==stage.clientHeight){
          const branch=work.element.getBoundingClientRect();
          const actorBox=actor.getBoundingClientRect();
          const stageLeft=stage.getBoundingClientRect().left;
          work.hookX=branch.left-stageLeft+branch.width*heartHook.x;
          work.hookY=branch.bottom-branch.height*heartHook.fromBottom;
          work.footY=actorBox.bottom-actorBox.height*.10;
          work.width=width();
          work.height=stage.clientHeight;
        }
        const progress=Math.max(0,Math.min(1,(elapsed-520)/(hookAt-520)));
        const remaining=1-progress*progress*(3-2*progress);
        // The mitten and loop travel together on a short, straight path to the hook.
        const handX=foot()+facing*(heartGrip.x-128)*scale;
        const handY=work.footY+(heartGrip.y-288)*scale;
        const carryX=(jobTarget()+handX-work.hookX)*remaining;
        const carryY=frameSize*.036*remaining;
        x=work.hookX+carryX-handX;
        work.lastX=x;
        work.lastHop=work.hookY+carryY-handY;
        work.element.style.setProperty('--heart-grip-x',`${carryX}px`);
        work.element.style.setProperty('--heart-grip-y',`${carryY}px`);
        showWorkshopPose('nisse-heart-kneel.png',0,1,facing);
        frame.style.setProperty('--nisse-hop',`${work.lastHop}px`);
      }else{
        if(!work.released){
          work.element.classList.remove('is-hanging');
          work.element.classList.add('is-hung');
          work.released=true;
        }
        const withdraw=Math.min(1,(elapsed-releaseAt)/380);
        x=work.lastX+approach*size()*.06*withdraw;
        if(elapsed<standAt){
          showWorkshopPose('nisse-heart-kneel.png',0,1,facing);
          frame.style.setProperty('--nisse-hop',`${work.lastHop+3*withdraw}px`);
        }else{
          showAction('wave',elapsed-standAt);
        }
      }
      if(elapsed>=4200) nextStop();
    };
    const conjureGift=()=>{
      const approach=plantRatio<.28?1:-1;
      const castAt=1660;
      const giftAt=2450;
      const packAwayAt=3120;
      x=jobTarget();
      if(elapsed<4420){
        const pose=elapsed<890?0:elapsed<castAt?1:elapsed<packAwayAt?2:3;
        showWorkshopPose('nisse-gift-magic.png',pose,4,-approach);
      }else{
        showFront();
      }
      if(elapsed>=castAt&&!work){
        const element=placeDecoration('gift',plantRatio,'snow-front');
        element.classList.add('is-conjuring');
        element.style.setProperty('--gift-spell-side',approach);
        // The tip is at x=224 and y=245 within the cast pose's 384 × 320 cell.
        const tipX=x+foot()-approach*size()*(224-128)/384;
        element.style.setProperty('--gift-tip-x',`${tipX-plantRatio*width()}px`);
        const spell=document.createElement('span');
        spell.className='nisse-gift-magic';
        element.append(spell);
        work={element,spell};
      }
      if(elapsed>=giftAt&&!planted){
        work.element.classList.remove('is-conjuring');
        work.spell.classList.add('is-arrived');
        planted=true;
      }
      if(elapsed>=4920){
        work.spell.remove();
        nextStop();
      }
    };
    const doJob=()=>{
      if(job==='tree'){
        plantTree();
      }else if(job==='snowman'){
        buildSnowman();
      }else if(job==='lantern'){
        setLantern();
      }else if(job==='heart'){
        hangHeart();
      }else if(job==='gift'){
        conjureGift();
      }else if(elapsed>=(job==='eat'?mealDuration:durations[job]*counts[job]*2)){
        nextStop();
      }else{
        showAction(job,elapsed);
        if(polishTarget?.name==='lantern'){
          frame.style.setProperty('--nisse-lean',`${polishFacing*13}deg`);
        }
      }
    };
    const startTheft=(isRetry=false)=>{
      stolen=decorations[0];
      interruptAttempt=!isRetry&&(forced==='scrooge-flugt'||(
        !forced&&activeTime>=nextInterruptionAt&&nextSkiAt-activeTime>25_000&&Math.random()<.30
      ));
      direction=stolen.ratio<.5?1:-1;
      // Leave enough room to show the kneeling pose at trees close to a screen edge.
      if(stolen.name==='tree'&&Math.min(stolen.ratio,1-stolen.ratio)*width()<size()*.70) direction=-direction;
      x=outside(-direction);
      scrooge=document.createElement('div');
      scrooge.className='nisse-scrooge';
      scrooge.style.backgroundImage=`url("${assets.get('scrooge-walk-v2.png').src}")`;
      stage.append(scrooge);
      startMode('sneak');
    };
    const interruptTheft=()=>{
      // Independently track the departing Scrooge while the elf walks in.
      retreat={element:scrooge,x,facing:direction,elapsed:0};
      scrooge.classList.add('is-retreating');
      scrooge=null;
      retryPending=true;
      nextInterruptionAt=activeTime+100_000;
      startVisit(-retreat.facing);
    };
    const playRetreat=(dt)=>{
      if(!retreat) return;
      retreat.elapsed+=dt;
      const hurrying=retreat.elapsed>=800;
      const facing=hurrying?-retreat.facing:retreat.facing;
      if(hurrying) retreat.x+=facing*145*dt/1000;
      if(hurrying&&facing*(retreat.x-outside(facing))>=0){
        retreat.element.remove();
        retreat=null;
        return;
      }
      const col=hurrying?Math.floor((retreat.elapsed-800)/105)%4:1;
      retreat.element.style.backgroundPosition=`${col*100/3}% 0%`;
      retreat.element.style.setProperty('--scrooge-x',`${retreat.x}px`);
      retreat.element.style.setProperty('--scrooge-facing',facing);
    };
    const dragMitten=[[74,150],[71,146],[77,151],[72,145]];
    const towAnchor={
      gift:{x:.50,y:.34,width:.26,aspect:436/260},
      lantern:{x:.43,y:.024,width:.20,aspect:198/320},
      snowman:{x:.52,y:.36,width:.32,aspect:208/320},
      heart:{x:.30,y:.27,width:.23,aspect:220/260},
      goat:{x:.68,y:.48,width:.479167,aspect:184/196,fromStage:true},
    };
    const towTestProps=['gift','lantern','snowman','heart','goat','tree'];
    const startTowChain=()=>{
      const anchor=towAnchor[stolen.name];
      if(!anchor) return;

      const element=document.createElement('div');
      element.className='nisse-tow-chain';
      const links=Array.from({length:16},()=>{
        const link=document.createElement('span');
        link.className='nisse-tow-link';
        element.append(link);
        return link;
      });
      const hook=document.createElement('span');
      hook.className='nisse-tow-hook';
      element.append(hook);
      stage.append(element);
      towChain={element,links,hook,anchor};
      stolen.element.classList.add('has-tow-chain');
    };
    const updateTowChain=(frameIndex)=>{
      if(!towChain) return;
      const drag=x-dragStart;
      if(towChain.width!==width()||towChain.height!==stage.clientHeight){
        const bounds=stage.getBoundingClientRect();
        const item=stolen.element.getBoundingClientRect();
        const scroogeBounds=scrooge.getBoundingClientRect();
        const anchor=towChain.anchor;
        const sprite=stolen.element.firstElementChild;
        const imageWidth=(anchor.fromStage?scroogeBounds.width:item.width)*anchor.width;
        const imageAspect=sprite?.naturalWidth&&sprite.naturalHeight
          ? sprite.naturalWidth/sprite.naturalHeight : anchor.aspect;
        const imageHeight=imageWidth/imageAspect;
        const imageX=item.left-bounds.left-drag+item.width*.565;
        const imageY=item.bottom-bounds.top-item.height*.10-imageHeight/2;
        const mirrored=stolen.name==='goat'&&stolen.element.style.getPropertyValue('--goat-facing')==='-1';
        const u=mirrored?1-anchor.x:anchor.x;
        // Taller props still use their original rotation before translateX.
        const pivotOffset=stolen.name==='gift'||stolen.name==='lantern'?0:imageWidth/2;
        const a=(u-.5)*imageWidth-pivotOffset;
        const b=(anchor.y-.5)*imageHeight;
        const tilt=-8*Math.PI/180;
        towChain.hookX=imageX+pivotOffset+a*Math.cos(tilt)-b*Math.sin(tilt);
        towChain.hookY=imageY+a*Math.sin(tilt)+b*Math.cos(tilt);
        towChain.scroogeTop=scroogeBounds.top-bounds.top;
        towChain.scroogeWidth=scroogeBounds.width;
        towChain.width=width();
        towChain.height=stage.clientHeight;
      }
      const [mittenX,mittenY]=dragMitten[frameIndex];
      const scale=towChain.scroogeWidth/384;
      const startX=x+(128+direction*(mittenX-128))*scale;
      const startY=towChain.scroogeTop+mittenY*scale;
      const endX=towChain.hookX+drag;
      const endY=towChain.hookY;
      const dx=endX-startX,dy=endY-startY;
      const length=Math.hypot(dx,dy);
      const count=Math.max(3,Math.min(towChain.links.length,Math.round(length/5)));
      const spacing=length/count;
      const sag=Math.min(6,length*.11);
      for(let i=0;i<towChain.links.length;i++){
        const link=towChain.links[i];
        link.hidden=i>=count;
        if(link.hidden) continue;
        const t=(i+.5)/count;
        const cx=startX+dx*t;
        const cy=startY+dy*t+4*sag*t*(1-t);
        const angle=Math.atan2(dy+4*sag*(1-2*t),dx)*180/Math.PI;
        link.style.width=`${spacing+1.5}px`;
        link.style.transform=`translate(${cx}px,${cy}px) translate(-50%,-50%) rotate(${angle}deg)`;
      }
      const angle=Math.atan2(dy-4*sag,dx)*180/Math.PI;
      towChain.hook.style.transform=`translate(${endX}px,${endY}px) translate(-50%,-50%) rotate(${angle}deg)`;
    };
    const removeOldestDecoration=()=>{
      stolen.element.remove();
      decorations.shift();
      stolen=null;
      fitDecorations();
    };
    const finishTheft=()=>{
      if(towChain){
        towChain.element.remove();
        towChain=null;
      }
      scrooge.remove();
      scrooge=null;
      startMode('pause');
    };
    const scroogeSneakSpeed=85;
    const scroogeExitSpeed=190;
    const playTheft=(dt)=>{
      if(mode==='sneak'){
        const reach=stolen.name==='tree'?.44:.20;
        const goal=stolen.ratio*width()-foot()-direction*size()*reach;
        const distance=goal-x;
        x+=Math.sign(distance)*Math.min(Math.abs(distance),scroogeSneakSpeed*dt/1000);
        const visiblePoint=direction>0?-foot()+size()*.28:width()-foot()-size()*.28;
        const interruptAt=direction>0?Math.min(visiblePoint,goal-18):Math.max(visiblePoint,goal+18);
        if(interruptAttempt&&direction*(x-interruptAt)>=0){
          interruptTheft();
          return;
        }
        if(Math.abs(distance)<=scroogeSneakSpeed*dt/1000) startMode('grab');
      }else if(mode==='grab'&&elapsed>=450){
        if(stolen.name==='tree'){
          startMode('ignite');
        }else{
          direction=-direction;
          dragStart=x;
          stolen.element.classList.add('is-dragged');
          if(stolen.name==='snowman'){
            stolen.element.style.setProperty('--tow-direction',direction);
            const hat=Array.from(stolen.element.children).find(child=>child.className==='nisse-snow-hat');
            if(hat){
              const angle=direction>0?-26:29;
              const hatX=direction>0?-60:-52;
              const facing=stolen.element.dataset.facing==='left'?-1:1;
              hat.style.rotate='none';
              hat.style.transformOrigin='50% 88%';
              hat.style.transform=`translate(${hatX}%, 4px) rotate(${angle}deg) scaleX(${facing})`;
            }
            const scarfTail=document.createElement('span');
            scarfTail.className='nisse-scarf-tail';
            scarfTail.style.backgroundImage=`url("${assets.get('snow-scarf-tail.png').src}")`;
            scarfTail.style.setProperty('--scarf-tail-side',-direction);
            stolen.element.append(scarfTail);
          }
          startTowChain();
          startMode('drag');
        }
      }else if(mode==='ignite'&&elapsed>=2950){
        direction=-direction;
        const ash=document.createElement('span');
        ash.className='nisse-tree-ash';
        stolen.element.append(ash);
        stolen.element.classList.add('is-burning');
        startMode('fire-exit');
      }else if(mode==='drag'){
        x+=direction*scroogeExitSpeed*dt/1000;
        stolen.element.style.setProperty('--drag-x',`${x-dragStart}px`);
        if(direction*(x-outside(direction))>=0){
          removeOldestDecoration();
          finishTheft();
          return;
        }
      }else if(mode==='fire-exit'){
        x+=direction*scroogeExitSpeed*dt/1000;
        if(stolen&&elapsed>=3800) stolen.element.classList.add('is-ash');
        if(stolen&&elapsed>=5250) removeOldestDecoration();
        if(!stolen&&direction*(x-outside(direction))>=0){
          finishTheft();
          return;
        }
      }
      const dragging=mode==='drag';
      const kneeling=mode==='ignite';
      const hurrying=dragging||mode==='fire-exit';
      const col=mode==='grab'?1:Math.floor(elapsed/(hurrying?105:165))%4;
      const sprite=assets.get(kneeling?'scrooge-kneel.png':'scrooge-walk-v2.png');
      scrooge.style.backgroundImage=`url("${sprite.src}")`;
      scrooge.style.backgroundSize=kneeling?'200% 100%':'400% 200%';
      scrooge.style.backgroundPosition=kneeling
        ? `${elapsed>=575?100:0}% 0%`
        : `${col*100/3}% ${dragging?100:0}%`;
      scrooge.style.setProperty('--scrooge-x',`${x}px`);
      scrooge.style.setProperty('--scrooge-facing',direction);
      if(dragging) updateTowChain(col);
    };
    const tick=(now)=>{
      const delta=prev?now-prev:0;
      const dt=Math.min(delta,80);
      prev=now;
      activeTime+=delta;
      elapsed+=dt;
      playRetreat(dt);
      frame.style.setProperty('--nisse-lean','0deg');
      frame.style.setProperty('--nisse-hop','0px');
      actor.classList.toggle('is-skiing',mode==='ski');
      actor.classList.toggle('is-riding',mode==='ride');
      if(scrooge){
        playTheft(dt);
      }else if(meeting){
        playMeeting(dt);
      }else if(mode==='ride'){
        rideGoat(dt);
      }else if(mode==='dismount'){
        dismountGoat();
      }else if(mode==='ski'||mode==='exit'){
        const skiing=mode==='ski';
        x+=direction*(skiing?150:speed)*dt/1000;
        showAction(skiing?'ski':'walk',elapsed);
        if(direction*(x-outside(direction))>=0) startMode('wait');
      }else if(mode==='move'){
        const distance=jobTarget()-x;
        x+=Math.sign(distance)*Math.min(Math.abs(distance),speed*dt/1000);
        showAction('walk',elapsed);
        if(Math.abs(distance)<=speed*dt/1000){
          if(job==='walk') nextStop();
          else{
            if(job==='eat') nextMealAt=activeTime+mealInterval;
            startMode('job');
          }
        }
      }else if(mode==='job'){
        x=jobTarget();
        doJob();
      }else if(mode==='turn'){
        if(elapsed<100) showAction('walk',0);
        else if(elapsed<245) showFront();
        else{
          direction=nextDirection;
          showAction('walk',0);
        }
        if(elapsed>=360) startMode(afterTurn);
      }else if(mode==='pause'){
        if(elapsed>=2200){
          if(scroogeDemo) seedTheftDemo();
          else startVisit();
        }
      }else if(mode==='wait'&&retryPending){
        if(elapsed>=900&&!retreat){
          retryPending=false;
          startTheft(true);
        }
      }else if(mode==='wait'&&elapsed>=(forced?1100:4200)){
        const enoughToRaid=decorations.length>=3&&Math.random()<.48;
        const plotsFull=decorations.length>0&&choosePlantSpot()===null;
        if(skiDue()) startVisit();
        else if(scroogeDemo||plotsFull||(!forced&&enoughToRaid)) startTheft();
        else startVisit();
      }
      const elfOnScreen=['move','job','turn','exit','ski','ride','dismount','meet-approach'].includes(mode);
      actor.classList.toggle('is-polishing',mode==='job'&&job==='polish');
      actor.classList.toggle('is-workshop',mode==='dismount'||(mode==='job'&&(job==='snowman'||job==='lantern'||job==='heart'||job==='gift')));
      actor.classList.toggle('is-meeting-hidden',mode==='meet-kiss'||mode==='meet-exit');
      actor.classList.toggle('is-active',elfOnScreen);
      actor.style.setProperty('--nisse-ski-direction',direction);
      actor.style.setProperty('--nisse-x',`${x}px`);
      raf=requestAnimationFrame(tick);
    };
    const seedTheftDemo=()=>{
      if(forced==='scrooge-snowman'){
        placeDecoration('snowman',testPropIndex++%2===0?.72:.28,'snow-front');
        startMode('wait');
        return;
      }
      if(forced==='scrooge-alle'){
        const name=towTestProps[testPropIndex%towTestProps.length];
        const lap=Math.floor(testPropIndex/towTestProps.length);
        const ratio=(testPropIndex+lap)%2===0?.28:.72;
        placeDecoration(name,ratio);
        testPropIndex++;
        startMode('wait');
        return;
      }
      if(!decorations.length) placeDecoration(forced==='scrooge-kaede'?'gift':'tree',.27);
      if(decorations.length<2&&limit()>1){
        const opposite=decorations[0].ratio<.5?.73:.27;
        const companion=forced==='scrooge-kaede'
          ? (decorations[0].name==='gift'?'lantern':'gift')
          : pick(decorationJobs.filter(name=>name!=='tree'));
        placeDecoration(companion,opposite);
      }
      startMode('wait');
    };
    if(forced==='snowmen'){
      const count=Math.min(limit(),3);
      for(let i=0;i<count;i++) placeDecoration('snowman',(i+.5)/count);
      return;
    }
    if(forced==='hearts'){
      const count=Math.min(limit(),heartSprites.length);
      for(let i=0;i<count;i++) placeDecoration('heart',(i+.5)/count,'snow-front');
      return;
    }
    if(forced==='gifts'){
      const count=Math.min(limit(),giftSprites.length);
      for(let i=0;i<count;i++) placeDecoration('gift',(i+.5)/count);
      return;
    }
    if(forced==='landscape'){
      const examples=[['tree','snow-back'],['gift','snow-front'],['lantern','snow-front'],
        ['snowman','snow-back'],['heart','snow-front'],['gift','snow-front'],['lantern','snow-front']];
      const count=Math.min(limit(),examples.length);
      examples.slice(0,count).forEach(([name,depth],i)=>placeDecoration(name,(i+.5)/count,depth));
      return;
    }
    if(forced==='polish') placeDecoration('lantern',.55,'snow-front');
    if(scroogeDemo) seedTheftDemo();
    else startVisit();
    actor.style.setProperty('--nisse-x',`${x}px`);
    raf=requestAnimationFrame(tick);
    document.addEventListener('visibilitychange',()=>{
      cancelAnimationFrame(raf);prev=0;
      if(!document.hidden&&!motion.matches)raf=requestAnimationFrame(tick);
    });
    motion.addEventListener('change',()=>{
      cancelAnimationFrame(raf);prev=0;
      stage.hidden=motion.matches;
      if(!motion.matches&&!document.hidden)raf=requestAnimationFrame(tick);
    });
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',boot,{once:true});else boot();
})();
