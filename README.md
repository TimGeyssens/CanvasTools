# CanvasTools

Handige gereedschappen voor Canvas.

Deze repository verzamelt kleine, zelfstandige tools die het dagelijks werk met
Canvas makkelijker maken: dingen die je anders met de hand moet uitzoeken of
om de zoveel studenten opnieuw moet doen.

> **Status:** dit is een verzamelplek. De eerste tools staan erin, er zullen er
> meer volgen. Heb je zelf iets dat hier thuishoort? Doe maar een PR.

## Wat zit erin

| Tool | Map | Wat het doet |
| --- | --- | --- |
| [Groepentool](canvas-groepen/) | `canvas-groepen/` | Zet studenten uit een CSV in Canvas-groepen, met preview en droge proef |
| [Afwezigheden en vrijstellingen](canvas-afwezigheden-vrijstellingen/) | `canvas-afwezigheden-vrijstellingen/` | Zet op basis van een gewettigd-afwezighedenexport vrijstellingen, en 0 op afwezigheden zonder wettiging |

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

## Afwezigheden en vrijstellingen in het kort

Je hebt een export van gewettigde afwezigheden, en een lijst van opgaven met een
inleverdatum. De tool zet voor elke student met een geldige afwezigheid een
vrijstelling op de opgaven van die dag.

- Je geeft zelf per opgave de **inleverdatum** aan; dat hoeft niet de
  Canvas-deadline te zijn.
- Alleen afwezigheden met status **gewettigd** worden verwerkt.
- Optioneel zet hij ook een **0** voor wie afwezig was zonder wettiging, op basis
  van een aanwezigheidslijst.
- **Simulatie staat standaard aan**, dus eerst zien wat er zou gebeuren.
- Ook vind je hier je eigen afspraaksloten en een export van eindcijfers naar
  BamaFlex.

Dit is de enste tool die een build-stap heeft: het is een Blazor-app in .NET 10
en start met `dotnet run`. Daarom staat onder [Conventies](#conventies) hoe een
tool met een framework hiermee omgaat.

Zie
[`canvas-afwezigheden-vrijstellingen/README.md`](canvas-afwezigheden-vrijstellingen/)
voor de vereisten, de verwachte Excel-indeling en hoe je een API-token
aanmaakt.

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
  - er is één commando om te starten (`dotnet run` voor
    `canvas-afwezigheden-vrijstellingen/`);
  - de build-uitvoer (`bin/`, `obj/`) staat in de `.gitignore`;
  - geen NuGet- of `requirements`-lockfile op hoofdniveau: alles blijft bij de
    tool.
- **Gereedschapspecifieke afhankelijkheden** blijven in de map van die tool
  staan, niet gedeeld op hoofdniveau.
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
browser: daarvoor hoef je niets te installeren. Voor
`canvas-afwezigheden-vrijstellingen/` heb je de .NET 10 SDK nodig, en bij de
eerste start internet om de NuGet-pakketten op te halen.

