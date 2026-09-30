# Canvas projectgroepen

Lokale webpagina om groepen aan te maken in Canvas vanuit een CSV.

## Starten

```bash
cd canvas-groepen

# eenmalig, token in de environment (niet in een bestand):
export CANVAS_URL="https://arteveldehogeschool.instructure.com"
export CANVAS_TOKEN="je-token"
export CANVAS_COURSE="39211"        # mag ook een course_code zijn

python3 server.py
```

Open http://localhost:8765

Je kan de velden ook gewoon in de pagina invullen. Het token wordt alleen in
`sessionStorage` van dat tabblad bewaard en gaat nergens anders heen. De server
luistert uitsluitend op `127.0.0.1`.

## Gebruik

1. Vul Canvas URL, course ID/code en API-token in, klik **Verbinden**.
2. Sleep je CSV op de pagina. De verwachte indeling staat onder
   *Welke kolommen moet mijn CSV hebben?* — die uitleg staat ook hieronder.

## Indeling van de CSV

Eén **rij per student**. De groep is de waarde in de kolom `team_name`: alle
rijen met dezelfde groepnaam worden samen één groep in Canvas. Er is dus geen
kolom nodig die het aantal leden per groep aangeeft.

```
team_number,team_name,group_category,student_name,email,canvas_user_id,sis_user_id,section,match_status
1,Team 1,BYOB projectgroepen,X,martvanm1@...,101077,S154638,DVG2-B,OK
1,Team 1,BYOB projectgroepen,X,quinverg@...,101060,S154551,DVG2-B,OK
2,Team 2,BYOB projectgroepen,X,arnoschi@...,96355,S150664,DVG2-A,OK
```

| Kolom | Verplicht | Alternatieve kolomnaam | Gebruik |
| --- | --- | --- | --- |
| `team_name` | ja | `group_name`, `groep`, `group`, `team` | Naam van de groep in Canvas |
| `student_name` | ja | `naam`, `name`, `student` | Naam zoals in Canvas, valback bij zoeken |
| `canvas_user_id` | aanbevolen | `user_id`, `canvas_id` | Numeriek Canvas-id, meest betrouwbaar |
| `sis_user_id` | aanbevolen | `sis_id` | SIS-nummer zoals `S153136` |
| `email` | nee | `mail`, `e-mail` | Alleen om dubbele studenten te herkennen |
| `group_category` | nee | `group_set`, `category` | Naam van de group set; op de pagina aan te passen |
| `team_number` | nee | — | Genegeerd, mag gewoon blijven staan |
| `section` | nee | — | Genegeerd, mag gewoon blijven staan |
| `match_status` | nee | `status` | Genegeerd, mag gewoon blijven staan |

Alleen `team_name` en `student_name` zijn echt nodig, plus minstens één van
`canvas_user_id`, `sis_user_id` of een bruikbare `student_name`. Ontbreekt er
iets, dan zegt de pagina welke kolom het mist en toont het de kolommen die het
wél herkend heeft.

### Zo wordt een student in Canvas teruggevonden

In deze volgorde, tegen de roosterlijst van de cursus:

1. `canvas_user_id` — als dat nummer in de cursus voorkomt
2. `sis_user_id` — idem, op het SIS-nummer
3. `student_name` — genormaliseerd: hoofdlettergevoelig, accenten en
   leestekens weggehaald, en een eventueel studentnummer achter de naam
   afgesneden. Zo matcht `Chloe Martin` op `Chloë Martin` en
   `Jean-Noe Dubois` op `Jean Noé Dubois`.

Vindt het er geen, dan wordt de student overgeslagen en aan het eind van het
rapport getoond. De groep zelf wordt wel aangemaakt.

### Formaat

- Koptekst op rij 1, daarna één rij per student.
- Scheidingsteken mag `,`, `;` of een tab zijn; het wordt automatisch herkend.
- Een BOM (Excel-export) wordt genegeerd.
- Lege regels worden overgeslagen.
- Kolomnamen zijn hoofdlettergevoelig; onbekende kolommen worden genegeerd.
- Dubbele e-mails in het bestand worden als waarschuwing gemeld.
3. Controleer de verdeling. Studenten zonder `canvas_user_id` worden op
   e-mail gezocht binnen de cursus.
4. **Eerst droog proberen** laat zien wat er zou gebeuren zonder iets te
   wijzigen. Daarna **Naar Canvas sturen**.

## Gedrag

- De group set uit je CSV-kolom `group_category` wordt aangemaakt als die nog
  niet bestaat.
- Bestaande groepen met dezelfde naam worden hergebruikt, niet overschreven.
- Leden worden één voor één toegevoegd via `user_id`, want Canvas accepteert
  `members[]` alleen bij differentiatietags. Daarna leest het script de groep
  na; blijft een lidmaatschap op 'uitnodiging' staan, dan wordt het geaccepteerd.
  Wat uiteindelijk niet lukt, staat onderaan in het rapport.
- Al toegevoegde studenten worden overgeslagen. Het script is dus veilig om
  te herhalen.
- Vinkje "niet in de CSV" aan: haalt studenten die niet in je bestand staan
  ook uit hun groep. Staat uitgelaten blijven ze zitten.
- Studenten die niet in Canvas gevonden worden, worden overgeslagen en
  onderaan getoond. De groep wordt alsnog aangemaakt.

## API-calls die het script gebruikt

| Actie | Call |
| --- | --- |
| Group set opzoeken | `GET /api/v1/courses/:id/group_categories` |
| Group set maken | `POST /api/v1/courses/:id/group_categories` |
| Groepen ophalen | `GET /api/v1/courses/:id/groups?group_category_id=` |
| Groep maken | `POST /api/v1/group_categories/:id/groups` |
| Student zoeken | `GET /api/v1/courses/:id/students` (op id, sis_id of naam) |
| Lid toevoegen | `POST /api/v1/groups/:id/memberships` met `user_id` |
| Lidmaatschap accepteren | `PUT /api/v1/groups/:id/users/:user_id` met `workflow_state=accepted` |
| Lid verwijderen | `DELETE /api/v1/groups/:id/users/:user_id` |
| Groep verwijderen (proef) | `DELETE /api/v1/groups/:id` |

Let op: `POST /api/v1/courses/:id/groups` bestaat niet. En `members[]` op
`/memberships` werkt alleen voor differentiatietags, niet voor gewone
cursusgroepen.

## API-token aanmaken

Het token is een sleutel die je zelf in Canvas aanmaakt. Het is los van je
wachtwoord en je kunt het op elk moment intrekken.

1. Open in Canvas je **Account**-pagina: klik linksonder op je naam, of op
   **Account** in de voettekst.
2. Kies **Instellingen** en daarna **Geïntegreerde toegang**.
3. Klik op **+ Nieuw token** / *Nieuwe toegangstoken genereren*.
4. Geef het een herkenbare naam, bijvoorbeeld `groepentool laptop`, zodat je
   het later kunt terugvinden en intrekken.
5. Klik **Token genereren** en **kopieer het meteen** — Canvas toont het
   token maar één keer.
6. Plak het in het veld **API-token** op de pagina en klik op **Verbinden**.

De pagina zelf heeft deze uitleg ook, inclusief een knop om de reikwijdtes te
kopiëren. Standaard mag een nieuw token alles binnen je eigen account. Je
kunt het bij het genereren beperken: zet dan het vinkje *Beperkte reikwijdte*
en plak onderstaande regels één per regel in het tekstveld.

```
url:GET|/api/v1/users/self
url:GET|/api/v1/courses/:course_id/students
url:GET|/api/v1/courses/:course_id/group_categories
url:GET|/api/v1/courses/:course_id/groups
url:POST|/api/v1/courses/:course_id/group_categories
url:POST|/api/v1/group_categories/:group_category_id/groups
url:GET|/api/v1/group_categories/:group_category_id
url:GET|/api/v1/groups/:group_id
url:GET|/api/v1/groups/:group_id/users/:user_id
url:POST|/api/v1/groups/:group_id/memberships
url:PUT|/api/v1/groups/:group_id/users/:user_id
url:DELETE|/api/v1/groups/:group_id/users/:user_id
url:DELETE|/api/v1/group_categories/:group_category_id
```

### Token intrekken

Ga terug naar **Instellingen → Geïntegreerde toegang** en klik op het
prullenbakje naast het token. Doe dat zodra je klaar bent met de groepentool,
zeker als iemand anders op dezelfde laptop heeft meegekeken.

### Waar het token opgeslagen wordt

Het token wordt **alleen in het geheugen van het tabblad** bewaard
(`sessionStorage`) en nooit naar de schijf geschreven. Sluit je het tabblad, dan
is het weg. Wil je het niet telkens opnieuw plakken, zet het dan in
`canvas-groepen.env` achter `CANVAS_TOKEN=` — dat bestand staat in
`.gitignore` en wordt door `start.sh` ingelezen. Let op: wie toegang heeft tot
dat bestand, heeft toegang tot je Canvas-account.

