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
- [x] Fase 3 – udseende (tema, lup, nyt ikon)
- [x] Fase 4 – funktioner (tegneværktøjer, slør, historik, OCR, forsinkelse, fastgør, filnavne)
- [ ] Fase 5 – udgivelse (installer, opdatering, signering, Chrome Web Store)

## Log
- Udvidelsen: omdøbt, 4 sprog, IndexedDB-lager, færre tilladelser, 7 Playwright-tests grønne.
- Windows: kode delt i filer, SDK-projekt net48, oversættelser (125 nøgler), selvtest, løbende fejllog, overførsel fra "Skærmbillede".
- CI grøn på begge jobs (570f05d): build, 11 xUnit-tests, selvtest på Windows 11; 7 Chromium-tests.
- Win11: PrtScn fanges med lavniveau-tastaturhook (egen tråd), så Skærmklip ikke stjæler tasten. Om ImageShot-vindue tilføjet (91e365d).
- Fase 2 (b73616c): markering skalerer tekst pr. skærm-DPI, hukommelse frigives efter optagelse (selvtest: 26→26 MB efter 8 optagelser), DirectX-sessioner testet pr. skærm. DXGI var allerede isoleret (HandleProcessCorruptedStateExceptions + GDI-reserve).
- Fase 3 (6eb3e15, 48a1e00): nyt ikon (brand/make_icon.py), Theme.cs (auto/lyst/mørkt, mørke listefelter, menu), indstillinger i 2 kolonner med sprog/udseende/filnavneskabelon, lup + piletaster i markering. Udvidelsen havde allerede brandfarver + mørk tilstand.
- CI lægger skærmbilleder på grenene ci-shots-windows / ci-shots-extension (hent: git fetch origin ci-shots-windows; git archive … | tar -x). Bruges til visuel kontrol.
- Fase 4 Windows (7fa5598): editor (Shapes.cs/Doc, ImageView, Icons, PreviewForm), OCR (Ocr.cs, WINRT + Microsoft.Windows.SDK.Contracts), PinWindow, History (30 seneste, menu Seneste), forsinkelse 3/5/10 s, filnavneskabelon. Alle selvtests grønne inkl. OCR på CI.
- Fase 4 udvidelse (3d48d5f): draw.js med 8 værktøjer, test draw.js grøn.
- Næste: Fase 5 – Inno Setup-installer i CI, udgivelses-workflow (tag v*), opdateringstjek, signering forberedt, Chrome Web Store-pakke + tekster + privatlivspolitik.
