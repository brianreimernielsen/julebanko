# Granrammer til Julebanko

Åbn `demo.html` i en browser. Den viser Today’s Numbers med begge All Numbers-lister samlet, Have You Won? og Confirmed Winners på samme række. På smalle skærme står de under hinanden. Klik på **Tilføj tal** og **Tilføj vindere** for at ændre panelernes højde.

Kopiér `julerammer.css` og hele mappen `grafik/` til samme mappe i din app. Tilføj klasserne på hvert panel, fx `class="juleramme juleramme--glimt"`. Behold også panelets egne klasser. Hvis du vil bruge pakkens layout, lægges panelerne i en beholder med klassen `julebanko-paneler`.

- `juleramme--lys` viser tynd gran med gyldne lys.
- `juleramme--baer` viser gran med bær, kogler og sne.
- `juleramme--glimt` bruger præcis samme grafik som bærvarianten med et separat lag af julelys. Pærerne står fast, mens gløden flakker blidt som levende lys. Pauseknappen i demoen fryser den aktuelle lysstyrke. Systemets indstilling for reduceret bevægelse holder lyset konstant.
- `juleramme--baer-lys` bruger den tredje PNG med indtegnede, stillestående lys.

Kanterne gentages med CSS `border-image`, mens hjørnerne beholder deres størrelse. Hvert panel kan vokse ud fra sit indhold. Tilpas evt. `--gran-bredde` og sørg for tilstrækkelig indvendig afstand til panelets indhold. Lys og rammer ligger i dekorative pseudoelementer, så knapper og formularer stadig kan klikkes på.
