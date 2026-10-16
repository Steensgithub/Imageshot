# ImageShot – browserudvidelse

Skærmbilleder til Chrome, Edge, Ecosia og andre Chromium-browsere. Tag et billede af det **synlige område**,
et **markeret område**, **hele siden** eller **alle faner** – og kopiér det til udklipsholderen eller gem det som fil.
Udvidelsen er en del af **ImageShot** (Windows-program + browserudvidelse).

Sprog: English, Dansk, Deutsch, Español – browserens sprog bruges automatisk.
Andre sprog: [English](README.md) · [Deutsch](README.de.md) · [Español](README.es.md)

## Installation

1. Pak ZIP-filen ud, så du får mappen `extension`.
2. Skriv `chrome://extensions` i adresselinjen (eller: Menu → Udvidelser → Administrer udvidelser).
3. Slå **Udviklertilstand** til øverst til højre.
4. Klik **Indlæs upakket** og vælg mappen `extension`.
5. Klik på puslespilsikonet i værktøjslinjen og fastgør **ImageShot**.
   Lad mappen blive liggende – udvidelsen kører fra den.

## Brug

* **Højreklik** på en side → **ImageShot** → *Synligt område* · *Markér område* · *Hele siden* · *Alle faner*
* Eller klik på **ImageShot-ikonet** i værktøjslinjen og vælg optagelse dér.
* **Genveje:** `Alt+Shift+S` (synligt område), `Alt+Shift+A` (markér område).
  Kan ændres under `chrome://extensions/shortcuts`.
* **Markér område:** træk med musen. `Esc` eller højreklik annullerer.
  Skub musen mod øverste eller nederste kant, mens du trækker, så scroller siden (eller webappens boks)
  af sig selv, og markeringen bliver længere end skærmen. Musehjulet virker også. Billedet syes sammen til ét.

### Indstillinger (klik på ikonet)

* **Opløsning:** skyderen styrer billedstørrelse og kvalitet. 70 = 87 % af skærmens opløsning, kvalitet 85.
* **Filformat:** WebP (små filer), PNG (tabsfri) eller JPEG.
* **Efter optagelse:**
  * Vis udklipsholder – vælg kopiér eller gem
  * Kopiér direkte til udklipsholderen
  * Gem direkte som fil
* **Luk udklipsholderen automatisk**, når billedet er kopieret eller gemt. Den findes både i indstillingerne
  og nederst i udklipsholderen.
* **"Alle faner" omfatter:** det aktuelle vindue eller alle vinduer.
* **Mappe i Overførsler:** f.eks. `ImageShot` (undermapper med `/` er tilladt).
  Filerne hedder `ImageShot ÅÅÅÅ-MM-DD TT.mm.ss`.

### Tilskær

Klik **Tilskær** i udklipsholderen, træk en ramme om det, du vil beholde, og justér kanter og hjørner.
Træk inde i rammen for at flytte den. `Enter` (eller dobbeltklik) anvender, `Esc` annullerer.
**Fortryd tilskæring** eller `Ctrl+Z` går tilbage. Der tilskæres altid fra originalen – intet ekstra kvalitetstab.
Kopiér og Gem bruger det tilskårne billede.

## Godt at vide

* Når du kopierer, lægges billedet altid på udklipsholderen som PNG, fordi udklipsholderen kun modtager PNG.
  Gemte filer har det valgte format.
* "Kopiér direkte" med *Alle faner* viser udklipsholderen i stedet, fordi der kun kan ligge ét billede på
  udklipsholderen ad gangen.
* *Hele siden* på webapps (fx Gmail, Saxo, Notion), hvor indholdet scroller inde i en boks i stedet for i
  vinduet: ImageShot finder selv boksen og scroller den igennem. Billedet får toplinjen øverst, hele boksens
  indhold og bundlinjen nederst. Feltet ved siden af boksen (fx en menu) kommer kun med én gang.
* Første gang du bruger *Alle faner*, beder Chrome om adgang til alle websteder – det skal ImageShot have for at
  kunne fotografere andre faner end den aktuelle. Se [PERMISSIONS.md](PERMISSIONS.md) (engelsk).

## Begrænsninger

* Browserens egne sider (`chrome://`, Chrome Web Store, andre udvidelser) kan ikke fotograferes.
  Ikonet viser så et rødt "!" – hold musen over det for at se årsagen.
* *Hele siden*: faste topmenuer skjules efter første billede, så de ikke gentages. Meget lange sider afkortes
  ved 30.000 px. WebP kan højst være 16.383 px, så længere billeder gemmes som PNG.
* Lister, der henter mere, mens man scroller (uendelige feeds), tages kun så langt, som de var indlæst,
  da optagelsen startede.
* *Alle faner* skifter kort mellem fanerne, mens den fotograferer, og vender derefter tilbage til den fane,
  du stod på. Minimerede vinduer kan ikke fotograferes.
* Billederne gemmes kun, så længe udklipsholderen er åben (og aldrig mere end én time).

## Privatliv og tilladelser

ImageShot virker offline og sender intet nogen steder hen. Billederne ligger i udvidelsens lokale lager, indtil
udklipsholderen lukkes. Alle tilladelser er forklaret i [PERMISSIONS.md](PERMISSIONS.md).

## Udvikling

Automatiske test (Playwright) ligger i [`tests/`](tests/README.md). Udelad mappen `tests/`, når udvidelsen
pakkes til Chrome Web Store.
