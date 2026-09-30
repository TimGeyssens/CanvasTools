# CanvasTools

Handige gereedschappen voor Canvas.

Deze repository verzamelt kleine, zelfstandige tools die het dagelijks werk met
Canvas makkelijker maken: dingen die je anders met de hand moet uitzoeken of
om de zoveel studenten opnieuw moet doen.

> **Status:** dit is een verzamelplek. De eerste tools staan erin, er zullen er
> meer volgen. Heb je zelf iets dat hier thuishoort? Doe maar een PR.

## Wat zit erin

| Tool | Map | Vereiste | Wat het doet |
| --- | --- | --- | --- |
| [Groepentool](canvas-groepen/) | `canvas-groepen/` | Python 3 | Zet studenten uit een CSV in Canvas-groepen, met preview en droge proef |
| [Afwezigheden en vrijstellingen](canvas-afwezigheden-vrijstellingen/) | `canvas-afwezigheden-vrijstellingen/` | .NET 10 | Zet op basis van een gewettigd-afwezighedenexport vrijstellingen, en 0 op afwezigheden zonder wettiging |
| [Afspraaksloten](canvas-afspraaksloten/) | `canvas-afspraaksloten/` | .NET 10 | Toont je eigen afspraaksloten per dag, met wie geboekt heeft en hun cijfer |
| [Export naar BamaFlex](canvas-bamaflex-export/) | `canvas-bamaflex-export/` | .NET 10 | Exporteert de eindcijfers van een cursus naar een Excel-bestand voor BamaFlex |

## Groepentool in het kort

Je hebt een lijst met wie in welk groepje moet zitten, en een Canvas-cursus.
De Groepentool leest die lijst, laat je eerst zien wie er herkend wordt, en
plaatst de studenten daarna in echte Canvas-groepen.

- Eén student per rij in de CSV, de groep is de waarde in `team_name`.
- Studenten worden herkend op `canvas_user_id`, `sis_user_id` of naam.
- **Preview en droge proef eerst**, pas daarna echt naar Canvas.
- Wie al in de juiste groep zit wordt overgeslagen, dus herhalen is veilig.
- Het API-token blijft in je browser; het wordt nergens opgeslagen.

Zie [`canvas-groepen/README.md`](canvas-groepen/) voor de installatie, de
verwachte CSV-indeling en hoe je een API-token aanmaakt.

## De drie .NET-tools in het kort

De volgende drie tools zijn kleine Blazor-apps in .NET 10. Ze hebben een
build-stap en starten allemaal met `dotnet run`. Zie
[Conventies](#conventies) waarom dit een uitzondering is.

Ze delen dezelfde aanpak: je plakt je Canvas-URL en een API-token in de pagina,
het token wordt alleen in het geheugen bewaard en nergens opgeschreven, en je
kiest daarna een cursus uit je eigen inschrijvingen.

### Afwezigheden en vrijstellingen

Je hebt een export van gewettigde afwezigheden, en opgaven met een
inleverdatum. De tool zet voor elke student met een geldige afwezigheid een
vrijstelling op de opgaven van die dag.

- Je geeft zelf per opgave de **inleverdatum** aan; dat hoeft niet de
  Canvas-deadline te zijn, en kan ook per klas verschillen.
- Alleen afwezigheden met status **gewettigd** worden verwerkt.
- Optioneel zet hij ook een **0** voor wie afwezig was zonder wettiging, op basis
  van een aanwezigheidslijst.
- **Simulatie staat standaard aan**, dus eerst zien wat er zou gebeuren.

Zie
[`canvas-afwezigheden-vrijstellingen/README.md`](canvas-afwezigheden-vrijstellingen/)
voor de verwachte Excel-indeling.

### Afspraaksloten

Na een afspraakmoment wil je snel zien wie er wel en niet is geweest. De tool
toont je eigen slots per dag, met wie geboekt heeft en wat die student als
eindcijfer heeft. Slots van collega's blijven weg.

Zie [`canvas-afspraaksloten/README.md`](canvas-afspraaksloten/) voor de
werkwijze.

### Export naar BamaFlex

Eenmaal per cursus wil je de eindcijfers niet overtypen in BamaFlex. De tool
haalt de eindcijfers op, laat ze zien, en levert een Excel-bestand op met de
kolommen `Studentnummer`, `Naam`, `E-mail` en `Score`.

Let op: de omrekening naar een schaal van 20 is een heuristiek. Controleer de
lijst voor je importeert.

Zie [`canvas-bamaflex-export/README.md`](canvas-bamaflex-export/) voor het
bestandsformaat.

## Conventies


Om te voorkomen dat het een onoverzichtelijke dump wordt:

- **Elke tool staat in een eigen map**, met een eigen `README.md` die uitlegt
  wat het doet, hoe je het start en waar je een API-token vindt.
- **Elke tool heeft één ingang**: meestal een `start.sh` en/of één pagina. Je moet
  iets kunnen starten met één commando.
- **Liever geen framework of build-stap.** Een tool zonder framework draait op
  Python 3 met alleen de standaardbibliotheek, en heeft geen
  `pip install` nodig. Dat blijft de voorkeur.
- **Uitzondering: een tool die echt een UI nodig heeft** mag een framework
  gebruiken. Dan geldt:
  - de frameworkkeuze en de vereiste versie staan in de README van die tool;
  - er is één commando om te starten (`dotnet run`);
  - de build-uitvoer (`bin/`, `obj/`) staat in de `.gitignore`;
  - geen NuGet- of `requirements`-lockfile op hoofdniveau: alles blijft bij de
    tool;
  - **één functie per map.** De drie .NET-tools waren eerst één app met drie
    pagina's, en zijn opgesplitst omdat je een tool per keer start, bij een
    andere code, en niet de hele app om één ding te gebruiken. Houd dat zo:
    verhuis een functie naar een eigen map in plaats van er een tab aan toe te
    voegen.
- **Gereedschapspecifieke afhankelijkheden** blijven in de map van die tool
  staan, niet gedeeld op hoofdniveau. Haalt een tool geen EPPlus meer mee, dan
  staat EPPlus niet in de `.csproj`.
- **Geen secrets in de repo.** Elk token hoort in een `.env`-bestand dat
  genegeerd wordt, of in het geheugen van je browser. Er staat een
  `.env.example` per tool om het formaat te tonen. Een tool zonder `.env` laat
  het token in de pagina invoeren en toont een `*.example.json` of
  `.example` voor de overige instellingen.
- **API-URLs staan niet in de code**, zodat de tools op een andere
  Canvas-instantie werken.

## Accounts

De code is geschreven voor een Canvas-instantie met een account dat
cursussen beheert, bijvoorbeeld `arteveldehogeschool.instructure.com`. De
instantie-URL en cursus staan per tool in een `.env`-bestand of een voorbeeld-
configuratie, niet in de code, dus de tools werken ook op een andere instantie.

## Git

```bash
git clone https://github.com/TimGeyssens/CanvasTools.git
cd CanvasTools
```

De meeste tools draaien op Python 3 (alleen de standaardbibliotheek) en een
browser: daarvoor hoef je niets te installeren. Voor de drie .NET-tools heb je
de .NET 10 SDK nodig, en bij de eerste start internet om de NuGet-pakketten op te
halen. Elke .NET-tool start vanuit zijn eigen map:

```bash
cd canvas-afwezigheden-vrijstellingen && dotnet run
cd canvas-afspraaksloten              && dotnet run
cd canvas-bamaflex-export            && dotnet run
```

Ze luisteren alle drie op `http://localhost:5000`, dus draai er niet twee
tegelijk zonder een andere poort mee te geven
(`dotnet run --urls http://localhost:5001`).

