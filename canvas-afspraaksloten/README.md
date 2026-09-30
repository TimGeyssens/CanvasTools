# Canvas Afspraaksloten

Blazor-webapp die laat zien welke afspraaksloten in Canvas **van jou** zijn, wie
ze heeft geboekt en wat die studenten als eindcijfer hebben.

Handig om na een afspraakmoment snel te zien wie er wel en niet is geweest,
zonder eerst zelf door de lijsten in Canvas te bladeren.

Dit is een .NET-project en dus een uitzondering op de conventies van deze repo:
het heeft een build-stap nodig. Zie de [conventies](../README.md#conventies).

## Vereisten

- **.NET 10 SDK** of later. Controleer met `dotnet --version`.
- Internet bij de eerste start om het NuGet-pakket op te halen.
- Een Canvas-account met een API-token.

Dit is de lichtste van de drie .NET-tools: hij gebruikt alleen
`Newtonsoft.Json`, geen Excel, dus geen EPPlus.

## Starten

```bash
cd canvas-afspraaksloten
dotnet run
```

Open http://localhost:5000

Er is geen configuratiebestand nodig. `appsettings.json` is optioneel: wil je
standaard een instantie-URL of voorgeselecteerde cursus, kopieer dan
`appsettings.example.json` naar `appsettings.json`. Dat bestand staat in
`.gitignore`.

## Verbinden

De pagina begint met **Stap 0: Verbinden met Canvas**:

1. Vul de URL van je instantie in, bijvoorbeeld
   `https://arteveldehogeschool.instructure.com`. `/api/v1` mag erbij, dat wordt
   afgeknipt.
2. Plak je API-token in het tokenveld.
3. Klik op **Verbinden**. De app doet meteen een `GET /users/self` om te
   controleren of de URL en het token kloppen.

Het token wordt **nooit naar schijf geschreven**. Het leeft alleen in het geheugen
van het serverproces, in het geheugen van je browsertabblad. Sluit je het tabblad,
dan is het kwijt.

De app luistert standaard op `localhost` en is niet bereikbaar vanaf andere
machines.

## Gebruik

1. Kies een cursus.
2. De app haalt de afspraaksloten op die je zelf kunt beheren, en toont ze
   gegroepeerd per dag.
3. Vink **Include past appointment groups** aan als je ook de al geweest slots
   wilt zien.

Per slot zie je de tijd, de duur, de groep, **wie geboekt heeft** en het
**eindcijfer** van die student. Een vrij slot staat op `Available` en is groen
gemarkeerd.

## Gedrag

- Alleen afspraaksloten waarvoor je `manageable` rechten hebt worden getoond, dus
  in de praktijk de groepen die je zelf hebt aangemaakt. Slots van collega's
  verschijnen niet.
- Een geboekte reservering is in Canvas een *child event* van het slot. De tool
  leest het `context_code` daarvan (`user_<id>`) en koppelt dat aan de
  roosterijst om de naam en het cijfer te tonen.
- Wie geen eindcijfer heeft, toont een streepje.
- Canvas rapporteert `current_score`/`final_score` meestal als percentage
  (0-100). Cijfers boven de 20 worden omgerekend naar een schaal van 20, omdat
  veel van onze cursussen op /20 rapporteren. De tool doet dat op heuristiek, dus
  controleer een vreemde waarde.

## API-calls die de app gebruikt

| Actie | Call |
| --- | --- |
| Verbinden controleren | `GET /api/v1/users/self` |
| Eigen cursussen | `GET /api/v1/courses?enrollment_state=active` |
| Afspraaksloten | `GET /api/v1/appointment_groups?scope=manageable&context_codes[]=course_` |
| Details per groep | `GET /api/v1/appointment_groups/:id?include[]=appointments&include[]=child_events` |
| Namen en cijfers | `GET /api/v1/courses/:id/enrollments?type[]=StudentEnrollment&include[]=user` |

Voor de lijst-endpoints volgt de app de `Link`-header met `rel="next"` in plaats
van `?page=1,2,3`, omdat Canvas daar bookmark-paginering voor gebruikt.

## API-token aanmaken

Het token is een sleutel die je zelf in Canvas aanmaakt. Het is los van je
wachtwoord en je kunt het op elk moment intrekken.

1. Open in Canvas je **Account**-pagina: klik linksonder op je naam, of op
   **Account** in de voettekst.
2. Kies **Instellingen** en daarna **Geïntegreerde toegang**.
3. Klik op **+ Nieuw token**.
4. Geef het een herkenbare naam, bijvoorbeeld `afspraaksloten laptop`.
5. Klik **Token genereren** en **kopieer het meteen** — Canvas toont het token
   maar één keer.
6. Plak het in het tokenveld van de app en klik op **Verbinden**.

### Benodigde reikwijdtes

Standaard mag een nieuw token alles binnen je eigen account. Je kunt het
beperken: zet het vinkje *Beperkte reikwijdte* en plak onderstaande regels één per
regel in het tekstveld.

```
url:GET|/api/v1/users/self
url:GET|/api/v1/courses
url:GET|/api/v1/appointment_groups
url:GET|/api/v1/courses/:course_id/enrollments
```

### Token intrekken

Ga terug naar **Instellingen → Geïntegreerde toegang** en klik op het
prullenbakje naast het token.

## Waar het token opgeslagen wordt

Nergens. Er is geen `.env` en geen `appsettings.json` met een token nodig. Het
token leeft in het geheugen van het serverproces zolang je tabblad open is, en
verdwijnt zodra de app gestopt is.
