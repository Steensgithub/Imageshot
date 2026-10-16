# ImageShot – fremdrift

Mål: gøre værktøjet professionelt, så andre kan bruge det. Rækkefølge og beslutninger.

## Beslutninger
- Navn: **ImageShot**. Arkiv: Steensgithub/imageshot (privat).
- Sprog: dansk, engelsk, tysk, spansk (følger Windows, kan vælges).
- Platform: .NET Framework 4.8 (indbygget i Win10/11, lille download) i stedet for .NET 8 (+60–70 MB).
- Optagelse: DirectX Desktop Duplication beholdes (bevist på HDR); Windows.Graphics.Capture fravalgt, fordi den tegner en gul ramme på Windows 10.
- Signering: forberedes i CI, slås til senere (Azure Trusted Signing).
- Byg: altid på windows-latest i GitHub Actions + selvtest i det rigtige program.

## Status
- [x] Fase 1 – fundament: struktur, CI, tests, selvtest, fejllog, sprog, Win11-PrtScn, Om-side
- [x] Fase 2 – stabil motor
- [ ] Fase 3 – udseende (tema, lup, nyt ikon)
- [ ] Fase 4 – funktioner (tegneværktøjer, slør, historik, OCR, forsinkelse, fastgør, filnavne)
- [ ] Fase 5 – udgivelse (installer, opdatering, signering, Chrome Web Store)

## Log
- Udvidelsen: omdøbt, 4 sprog, IndexedDB-lager, færre tilladelser, 7 Playwright-tests grønne.
- Windows: kode delt i filer, SDK-projekt net48, oversættelser (125 nøgler), selvtest, løbende fejllog, overførsel fra "Skærmbillede".
- CI grøn på begge jobs (570f05d): build, 11 xUnit-tests, selvtest på Windows 11; 7 Chromium-tests.
- Win11: PrtScn fanges med lavniveau-tastaturhook (egen tråd), så Skærmklip ikke stjæler tasten. Om ImageShot-vindue tilføjet (91e365d).
- Fase 2 (b73616c): markering skalerer tekst pr. skærm-DPI, hukommelse frigives efter optagelse (selvtest: 26→26 MB efter 8 optagelser), DirectX-sessioner testet pr. skærm. DXGI var allerede isoleret (HandleProcessCorruptedStateExceptions + GDI-reserve).
- Næste: Fase 3 – nyt ikon, lyst/mørkt tema, lup + piletaster i markering.
