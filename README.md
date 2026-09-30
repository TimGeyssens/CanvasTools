# CanvasTools

Handige gereedschappen voor Canvas.

Deze repository verzamelt kleine, zelfstandige tools die het dagelijks werk met
Canvas makkelijker maken: dingen die je anders met de hand moet uitzoeken of
om de zoveel studenten opnieuw moet doen.

> **Status:** dit is een verzamelplek. De eerste tool staat erin, er zullen er
> meer volgen. Heb je zelf iets dat hier thuishoort? Doe maar een PR.

## Wat zit erin

| Tool | Map | Wat het doet |
| --- | --- | --- |
| [Groepentool](canvas-groepen/) | `canvas-groepen/` | Zet studenten uit een CSV in Canvas-groepen, met preview en droge proef |

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

## Conventies

Om te voorkomen dat het een onoverzichtelijke dump wordt:

- **Elke tool staat in een eigen map**, met een eigen `README.md` die uitlegt
  wat het doet, hoe je het start en waar je een API-token vindt.
- **Elke tool heeft één ingang**: meestal een `start.sh` en/of één pagina.
  Liever geen framework of build-stap; je moet iets kunnen starten met één
  commando.
- **Gereedschapspecifieke afhankelijkheden** blijven in de map van die tool
  staan, niet gedeeld in een `requirements.txt` op hoofdniveau.
- **Geen secrets in de repo.** Elk token hoort in een `.env`-bestand dat
  genegeerd wordt, of in het geheugen van je browser. Er staat een
  `.env.example` per tool om het formaat te tonen.

## Accounts

De code is geschreven voor een Canvas-instantie met een account dat
cursussen beheert, bijvoorbeeld `arteveldehogeschool.instructure.com`. De
instantie-URL en cursus staan per tool in een `.env`-bestand, niet in de code,
dus de tools werken ook op een andere instantie.

## Git

```bash
git clone https://github.com/TimGeyssens/CanvasTools.git
cd CanvasTools
```

Nog niets om te installeren: alles draait op Python 3 (alleen de
standaardbibliotheek) en een browser.
