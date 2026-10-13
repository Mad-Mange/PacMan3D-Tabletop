# Nästa steg

Senast uppdaterad 2026-10-14.

## Läget

- Spelet är spelbart från början till slut:
  - titelskärm med en kamera som cirklar runt brädet
  - demoläge där AI:n spelar
  - intro, spel, död, nivåbyte och game over
- Reglerna följer arkaden:
  - de fyra spökpersonligheterna
  - scatter/chase-vågorna
  - frightened-läget med blinkningar
  - hur spökena släpps ut ur huset
  - Elroy, frukter, extraliv och high score
- Pac-Man kan svänga lite före eller efter rutans mitt ("cornering") och glider sedan tillbaka in på linjen, som i arkaden.
- 18 EditMode-tester är gröna.
- Utseendet bygger på `inspiration/InspirationPicture.png`:
  - glittriga teal-väggar med blå kanter och LED-strips
  - gula LED-pellets i svart golv
  - voxelspöken från Blender
  - ett träbord under alltihop
- Det finns menyer:
  - startmeny: START GAME / OPTIONS / QUIT
  - pausmeny: RESUME / OPTIONS / QUIT TO TITLE
  - OPTIONS: volym för master, effekter, musik och siren, plus kamera och helskärm
  - menyerna sparas och fungerar med tangentbord, gamepad och mus
  - titelskärmen har en egen (egenkomponerad) melodi
- Bordet lever:
  - rekvisita från Blender (`Blender/build_props.py`)
  - fönsterljus med moln som drar förbi, damm i ljuset och ånga från kaffet
  - LED-glöd under brädet som följer spelet
  - reservliven som figurer som tittar på, jublar och hoppar in på brädet
  - vunna frukter på rad, och en arkadspak som speglar styrningen
- Windows-bygget fungerar: `Builds/Windows/PacMan3D.exe`.

## Att göra, viktigast först

1. **Spela själv och notera vad som skaver.** Jag har bara sett stillbilder och korta automatiska körningar, så ingen har bedömt känslan i rörelse.
   - Testa båda kameralägena (C).
   - Översikten visar hela brädet. Följkameran är snyggast men visar mindre.
2. **Ljudet.** Allt syntetiseras i `Sfx.cs` och låter som enkel chiptune.
   - Om du vill ha mer "riktiga" ljud kan du byta till ljudfiler.
   - Introjingeln är egen, inte arkadens.
3. **Finputs på utseendet:**
   - Väggarnas toppar är ganska mörka. Fotot har mer synliga lager och färgskiftningar. Färgerna sitter i `TextureGen.WallTop` och `ProjectSetup.BuildMaterials`.
   - Det genomskinliga Inky-spöket kan sortera konstigt mot sig självt, eftersom voxlarna är transparenta. Ett alternativ är en dithrad eller "frostad" opak variant.
   - Pac-Man skulle kunna få en tryckt lagerstruktur som spökena, så att han matchar fotot bättre.
4. **Fler banor.** `Maze.Layout` är arkadens labyrint. Väggarna byggs automatiskt från layouten, så en ny labyrint behöver bara:
   - nya strängar
   - nya startpositioner i `GameSim`, om huset flyttas
   - uppdaterade siffror i testerna (240 prickar)
5. **Mer liv på bordet (idéer).**
   - En katt-tass eller hand som ibland flyttar en tärning.
   - Mobilen som surrar när du slår high score.
   - Kaffet som sjunker undan för varje nivå.
6. **AI-spelaren.** `Autopilot` är en enkel BFS som undviker spöken. Den räcker till demot men dör ganska ofta.

## Hur man kör

Se README.md (spel och kontroller) och CLAUDE.md (alla batch-kommandon och arkitekturen).
