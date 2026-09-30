#!/usr/bin/env python3
"""Lokale webpagina om Canvas-projectgroepen aan te maken vanuit een CSV.

Alleen standaardbibliotheek. Start met:  python3 server.py
"""

import json
import os
import re
import ssl
import sys
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
PORT = int(os.environ.get("PORT", "8765"))

CONFIG = {
    "baseUrl": os.environ.get("CANVAS_URL", "").rstrip("/"),
    "token": os.environ.get("CANVAS_TOKEN", ""),
    "courseId": os.environ.get("CANVAS_COURSE", ""),
}


# ---------------------------------------------------------------- CSV lezen

def parse_csv(text):
    """Leest CSV (ook met ; of tabs) en geeft lijst van dicten terug."""
    text = text.lstrip("\ufeff")
    lines = [ln for ln in re.split(r"\r\n|\r|\n", text) if ln.strip()]
    if not lines:
        return []

    delim = ","
    for d in (",", ";", "\t"):
        if d in lines[0]:
            delim = d
            break

    def split(line):
        out, cur, quoted = [], "", False
        for ch in line:
            if ch == '"':
                quoted = not quoted
            elif ch == delim and not quoted:
                out.append(cur.strip())
                cur = ""
            else:
                cur += ch
        out.append(cur.strip())
        return out

    header = [h.strip().lower().lstrip("\ufeff") for h in split(lines[0])]
    rows = []
    for ln in lines[1:]:
        cells = split(ln)
        cells += [""] * (len(header) - len(cells))
        rows.append({header[i]: cells[i] for i in range(len(header))})
    return rows


def build_plan(rows):
    """Zet CSV-rijen om in een plan: teams + leden + waarschuwingen."""
    if not rows:
        return {
            "groupSetName": "Projectgroepen",
            "teams": [],
            "warnings": [],
            "missingColumns": ["Het bestand bevat geen kolommen of geen rijen."],
            "headers": [],
        }

    def col(*names):
        for n in names:
            for k in rows[0]:
                if k == n:
                    return k
        return None

    c_team = col("team_name", "group", "team", "groep", "group_name")
    c_name = col("student_name", "name", "naam", "student")
    c_mail = col("email", "mail", "e-mail")
    c_uid = col("canvas_user_id", "user_id", "canvas_id")
    c_sis = col("sis_user_id", "sis_id")
    c_set = col("group_category", "group_set", "category")
    c_stat = col("match_status", "status")

    # ontbrekende kolommen meteen benoemen, anders faalt het stil
    missing = []
    for label, found, hint in (
        ("de groepnaam", c_team, "team_name, group_name, groep of group"),
        ("de studentennaam", c_name, "student_name, naam of name"),
    ):
        if not found:
            missing.append(
                "Er ontbreekt een kolom voor %s. Geaccepteerde namen: %s."
                % (label, hint)
            )
    if missing:
        return {
            "groupSetName": "Projectgroepen",
            "teams": [],
            "warnings": [],
            "missingColumns": missing,
            "headers": list(rows[0].keys()),
        }

    warnings = []
    if not c_uid and not c_sis:
        warnings.append(
            "Er is geen kolom canvas_user_id of sis_user_id. Studenten worden dan "
            "alleen op naam gevonden, wat bij twee studenten met dezelfde naam "
            "kan misgaan. Kijk in de preview of iedereen is gevonden."
        )

    teams, seen_mail = [], {}
    for i, r in enumerate(rows, start=2):
        team = (r.get(c_team) or "").strip() or "(geen groep)"
        member = {
            "row": i,
            "name": (r.get(c_name) or "").strip(),
            "email": (r.get(c_mail) or "").strip().lower(),
            "canvasUserId": (r.get(c_uid) or "").strip(),
            "sisUserId": (r.get(c_sis) or "").strip(),
            "status": (r.get(c_stat) or "").strip(),
        }

        if member["email"] and member["email"] in seen_mail:
            warnings.append(
                "Rij %d: %s staat al in team %s."
                % (i, member["email"], seen_mail[member["email"]])
            )
        elif member["email"]:
            seen_mail[member["email"]] = team

        if not member["canvasUserId"] and not member["sisUserId"]:
            warnings.append(
                "Rij %d: %s heeft geen canvas- of sis-id; er wordt op naam gezocht."
                % (i, member["name"] or member["email"])
            )

        for t in teams:
            if t["name"] == team:
                t["members"].append(member)
                break
        else:
            teams.append({"name": team, "members": [member]})

    teams.sort(key=lambda t: t["name"])
    return {
        "groupSetName": (rows[0].get(c_set) or "Projectgroepen").strip() or "Projectgroepen",
        "teams": teams,
        "warnings": warnings,
        "missingColumns": [],
        "headers": list(rows[0].keys()),
    }


# ---------------------------------------------------------------- Canvas API

def norm(value):
    """Kleine letters, zonder diacrieten en zonder leestekens.

    Zodat 'Chloe Martin' gelijk is aan 'Chloë Martin' en
    "Jean-Noe Dubois" aan "Jean Noé Dubois". Studentnummers achter de naam
    worden weggesneden.
    """
    s = unicodedata.normalize("NFKD", str(value or "")).encode("ascii", "ignore").decode()
    s = re.sub(r"[\s._,]+", " ", s.lower())
    s = re.sub(r"[^a-z0-9 -]", "", s)
    s = re.sub(r"\s*-\s*\d+\s*$", "", s)
    return re.sub(r"\s+", " ", s).strip()


def encode_multipart(form, boundary="----CanvasGroepenTool1234567890"):
    """Barkeert een formulier als multipart/form-data.

    Canvas documenteert zijn voorbeelden met -F, dus we sturen het ook zo.
    Vooral belangrijk voor parameters als 'members[]'.
    """
    lines = []
    for key, value in form.items():
        for v in value if isinstance(value, (list, tuple)) else [value]:
            lines.append(
                '--%s\r\nContent-Disposition: form-data; name="%s"\r\n\r\n%s\r\n'
                % (boundary, key, v)
            )
    lines.append("--%s--\r\n" % boundary)
    return (
        "".join(lines).encode("utf-8"),
        "multipart/form-data; boundary=%s" % boundary,
    )


def describe_error(raw, code):
    """Haalt de leesbare foutmelding uit een Canvas-antwoord.

    Canvas stuurt 'errors' soms als lijst van objecten en soms als object,
    en valt terug op HTML of platte tekst.
    """
    try:
        parsed = json.loads(raw)
    except ValueError:
        text = re.sub(r"<[^>]+>", " ", raw)
        text = re.sub(r"\s+", " ", text).strip()
        return text[:300] if text else "leeg antwoord"

    errors = parsed.get("errors") if isinstance(parsed, dict) else None
    if isinstance(errors, dict):
        return str(errors.get("message") or errors)[:300]
    if isinstance(errors, list):
        msgs = [
            e.get("message") if isinstance(e, dict) else str(e) for e in errors
        ]
        return "; ".join(str(m) for m in msgs if m)[:300] or str(parsed)[:300]
    if isinstance(parsed, dict) and parsed.get("message"):
        return str(parsed["message"])[:300]
    return json.dumps(parsed)[:300]


class Roster:
    """Zoekt studenten op canvas_id, sis_id of genormaliseerde naam."""

    def __init__(self, students):
        self.by_id, self.by_sis, self.by_name = {}, {}, {}
        for s in students:
            self.by_id[s["id"]] = s
            if s.get("sis_user_id"):
                self.by_sis[str(s["sis_user_id"]).lower()] = s
            for key in (s.get("short_name"), s.get("name")):
                n = norm(key)
                if n:
                    self.by_name.setdefault(n, []).append(s)

    def find(self, member):
        problems = []

        if member["canvasUserId"].isdigit():
            uid = int(member["canvasUserId"])
            if uid in self.by_id:
                return uid, "canvas_user_id uit CSV"
            problems.append("canvas_user_id %d staat niet in deze cursus" % uid)
        elif member["canvasUserId"]:
            problems.append("canvas_user_id '%s' is geen getal" % member["canvasUserId"])

        sis = member["sisUserId"].lower()
        if sis and sis in self.by_sis:
            return self.by_sis[sis]["id"], "sis_user_id %s" % member["sisUserId"]
        if sis:
            problems.append("sis_user_id %s staat niet in deze cursus" % member["sisUserId"])

        n = norm(member["name"])
        hits = self.by_name.get(n, [])
        if len(hits) == 1:
            return hits[0]["id"], "naam '%s' gevonden in de cursus" % member["name"]
        if len(hits) > 1:
            problems.append("naam '%s' is niet uniek (%d treffers)" % (member["name"], len(hits)))
        elif n:
            problems.append("naam '%s' niet gevonden" % member["name"])

        return None, "; ".join(problems) or "geen id of naam om op te zoeken"


class Canvas:
    def __init__(self, base_url, token):
        self.base = base_url.rstrip("/") + "/api/v1"
        self.token = token
        self.ctx = ssl.create_default_context()

    def _request(self, method, path, params=None, form=None, data=None):
        return self._send(method, path, params, form, data)[0]

    def _send(self, method, path, params=None, form=None, data=None):
        """Doet een API-call. Geeft (json, link_header) terug."""
        url = path if path.startswith("http") else self.base + path
        if params:
            clean = {k: v for k, v in params.items() if v is not None}
            if clean:
                url += "?" + urllib.parse.urlencode(clean, doseq=True)
        body = None
        headers = {"Authorization": "Bearer " + self.token}

        if form is not None:
            body, ctype = encode_multipart(form)
            headers["Content-Type"] = ctype
        elif data is not None:
            body = json.dumps(data).encode()
            headers["Content-Type"] = "application/json"

        req = urllib.request.Request(url, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=60, context=self.ctx) as r:
                raw = r.read().decode("utf-8", "replace")
                return (json.loads(raw) if raw.strip() else None), r.headers.get("Link", "")
        except urllib.error.HTTPError as e:
            raw = e.read().decode("utf-8", "replace")
            if e.code == 401:
                raise RuntimeError(
                    "Token geweigerd door Canvas (401). Het token is verlopen, "
                    "ingetrokken of je hebt geen rechten voor deze cursus. "
                    "Maak een nieuw token in Canvas: Account > Instellingen > "
                    "Geintegreerde toegang > Token nieuw."
                )
            raise RuntimeError(
                "Canvas %s %s -> HTTP %d: %s"
                % (method, path, e.code, describe_error(raw, e.code))
            )
        except urllib.error.URLError as e:
            raise RuntimeError("Geen verbinding met %s (%s)" % (url, e.reason))

    def get(self, path, **params):
        return self._request("GET", path, params=params)

    def get_all(self, path, **params):
        """Haalt alle pagina's op via de Link-header van Canvas."""
        out, url, first = [], path, True
        for _ in range(100):  # veiligheidsrem
            batch, link = self._send("GET", url, params if first else None)
            if not isinstance(batch, list):
                return out + [batch] if batch else out
            out.extend(batch)
            nxt = ""
            for part in link.split(","):
                if 'rel="next"' in part:
                    nxt = part.split("<")[1].split(">")[0]
            if not nxt:
                return out
            url, first = nxt, False
        return out

    def post(self, path, **form):
        return self._request("POST", path, form=form)

    def put(self, path, **form):
        return self._request("PUT", path, form=form)

    def delete(self, path, **form):
        return self._request("DELETE", path, form=form)

    # -- helpers ---------------------------------------------------------

    def roster(self, course_id):
        """Haalt de cursuspas op en maakt er een zoektabel van."""
        students = self.get_all("/courses/%s/students" % course_id)
        return Roster(students)

    def resolve_course(self, course_ref):
        """Accepteert een numerieke ID, sis_course_id:CODE of course_code."""
        ref = str(course_ref).strip()
        try:
            return self.get("/courses/%s" % ref)["id"]
        except RuntimeError:
            pass
        for key in ("sis_course_id", "sis_source_id"):
            try:
                return self.get("/courses/%s:%s" % (key, ref))["id"]
            except RuntimeError:
                continue
        for c in self.get_all("/courses"):
            if (c.get("course_code") or "").lower() == ref.lower():
                return c["id"]
        raise RuntimeError(
            "Cursus '%s' niet gevonden. Gebruik het numerieke course ID "
            "(te vinden in de URL van de cursus)." % course_ref
        )

    def existing_groups(self, course_id, set_name):
        """Vindt of maakt de group set; geeft {naam: group} terug."""
        cats = self.get_all("/courses/%s/group_categories" % course_id)
        cat = next((c for c in cats if (c.get("name") or "").strip() == set_name), None)
        if not cat:
            return None, {}

        groups = self.get_all(
            "/courses/%s/groups" % course_id,
            group_category_id=cat["id"],
            include=["users"],
        )
        return cat, {g["name"]: g for g in groups}

    def ensure_group_set(self, course_id, set_name, create=True):
        cats = self.get_all("/courses/%s/group_categories" % course_id)
        cat = next((c for c in cats if (c.get("name") or "").strip() == set_name), None)
        if cat:
            return cat, "bestaande group set (id %d)" % cat["id"], True
        if not create:
            return None, "zou aangemaakt worden", False
        cat = self.post(
            "/courses/%s/group_categories" % course_id,
            name=set_name,
            self_signup="false",
            auto_leader="false",
        )
        return cat, "aangemaakt (id %d)" % cat["id"], True


# ---------------------------------------------------------------- Werkzaamheid

def resolve_members(roster, plan):
    """Zet per lid de user_id vast. Geeft een lijst met niet-gevonden leden."""
    errors = []
    for team in plan["teams"]:
        for m in team["members"]:
            uid, how = roster.find(m)
            m["resolvedUserId"] = uid
            m["resolveNote"] = how
            if uid is None:
                errors.append(
                    "%s (%s) in %s: %s" % (m["name"], m["email"], team["name"], how)
                )
    return errors


def check_token(canvas):
    """Snel checken of het token nog werkt, met een duidelijke melding."""
    try:
        return canvas.get("/users/self")["name"]
    except RuntimeError as e:
        raise RuntimeError(str(e))


def check_write(canvas, course_id):
    """Doet een volledige proef met een tijdelijke groep en ruimt hem op.

    Test het hele pad: group set maken, groep maken, lid toevoegen, lid
    verwijderen. Niets van je echte groepen wordt aangeraakt.
    """
    cat = grp = probe = None
    try:
        cat = canvas.post(
            "/courses/%s/group_categories" % course_id, name="__groepentool_test__"
        )
        grp = canvas.post("/group_categories/%s/groups" % cat["id"], name="Testgroep")
        canvas.get("/groups/%s" % grp["id"])

        students = canvas.get_all("/courses/%s/students" % course_id)
        if students:
            probe = students[0]["id"]
            canvas.post("/groups/%s/memberships" % grp["id"], user_id=probe)
            m = canvas.get("/groups/%s/users/%s" % (grp["id"], probe))
            if m.get("workflow_state") != "accepted":
                canvas.put(
                    "/groups/%s/users/%s" % (grp["id"], probe),
                    workflow_state="accepted",
                )
            canvas.delete("/groups/%s/users/%s" % (grp["id"], probe))
    except RuntimeError as e:
        _cleanup_group(canvas, cat, grp)
        raise RuntimeError("Schrijven naar Canvas lukt niet: %s" % e)
    _cleanup_group(canvas, cat, grp)
    return True


def _cleanup_group(canvas, cat, grp=None):
    if grp:
        try:
            canvas.delete("/groups/%s" % grp["id"])
        except RuntimeError:
            pass
    if cat:
        try:
            canvas.delete("/group_categories/%s" % cat["id"])
        except RuntimeError:
            pass


def do_preview(canvas, course_id, plan):
    """Kopiert de voorbereiding die do_push ook doet, maar wijzigt niets."""
    set_name = plan["groupSetName"]
    cat, existing = canvas.existing_groups(course_id, set_name)
    unmatched = resolve_members(canvas.roster(course_id), plan)

    for team in plan["teams"]:
        team["ok"] = [m for m in team["members"] if m.get("resolvedUserId")]
        team["skipped"] = [m for m in team["members"] if not m.get("resolvedUserId")]

    return {
        "plan": plan,
        "unmatched": unmatched,
        "canvas": {
            "connected": True,
            "groupSetExists": bool(cat),
            "groupSetId": cat["id"] if cat else None,
            "newGroups": sorted(t["name"] for t in plan["teams"] if t["name"] not in existing),
            "existingGroups": sorted(
                t["name"] for t in plan["teams"] if t["name"] in existing
            ),
        },
    }


def add_members(canvas, group_id, user_ids, wanted, team):
    """Zet studenten in een groep en controleert of het echt gelukt is.

    Canvas accepteert members[] alleen voor differentiatietags, dus voor
    gewone cursusgroepen gaat het per student via user_id. Daarna lezen we
    de groep uit; blijft iemand 'invited', dan accepteren we die lidmaatschap.
    """
    added, refused = [], []
    for uid in user_ids:
        try:
            canvas.post("/groups/%s/memberships" % group_id, user_id=uid)
            added.append(uid)
        except RuntimeError as e:
            refused.append((wanted.get(uid, uid), str(e)))

    pending = accept_pending(canvas, group_id, user_ids)
    for uid in pending:
        name = wanted.get(uid, uid)
        if uid not in [r[0] for r in refused]:
            refused.append(
                (name, "Canvas wil dit lidmaatschap accepteren niet; "
                       "de student is uitgenodigd maar niet lid")
            )

    if not added and refused:
        raise RuntimeError(
            "Geen enkele student kon aan %s worden toegevoegd. Eerste fout: %s"
            % (team["name"], refused[0][1])
        )
    return added, refused


def accept_pending(canvas, group_id, user_ids):
    """Accepteert lidmaatschappen die Canvas als uitnodiging heeft gemaakt.

    Geeft terug welke studenten na het accepteren nog steeds geen echt
    lidmaatschap hebben, zodat je dat in het rapport ziet.
    """
    still_pending = []
    for uid in user_ids:
        try:
            m = canvas.get("/groups/%s/users/%s" % (group_id, uid))
        except RuntimeError:
            continue
        if m.get("workflow_state") == "accepted":
            continue
        try:
            canvas.put(
                "/groups/%s/users/%s" % (group_id, uid), workflow_state="accepted"
            )
        except RuntimeError:
            pass
        try:
            after = canvas.get("/groups/%s/users/%s" % (group_id, uid))
            if after.get("workflow_state") != "accepted":
                still_pending.append(uid)
        except RuntimeError:
            still_pending.append(uid)
    return still_pending


def remove_members(canvas, group_id, user_ids):
    """Haalt studenten één voor één uit een groep."""
    for uid in user_ids:
        try:
            canvas.delete("/groups/%s/users/%s" % (group_id, uid))
        except RuntimeError as e:
            raise RuntimeError("Lid %s kon niet verwijderd worden: %s" % (uid, e))


def do_push(cfg, csv_text, options):
    plan = build_plan(parse_csv(csv_text))
    if not str(cfg["courseId"]).strip():
        raise RuntimeError("Geen course ID of code ingevuld.")
    set_name = (options.get("groupSetName") or plan["groupSetName"]).strip()

    dry = bool(options.get("dryRun"))
    canvas = Canvas(cfg["baseUrl"], cfg["token"])
    course_id = canvas.resolve_course(cfg["courseId"])
    log = ["Cursus gevonden: id %d" % course_id]

    if not dry:
        # voorkomt dat er halverwege iets kapotgaat: eerst even testen
        check_write(canvas, course_id)
        log.append("Schrijftoegang gecontroleerd: ok")

    cat, note, found = canvas.ensure_group_set(course_id, set_name, create=not dry)
    log.append("Group set '%s': %s" % (set_name, note))
    category_id = cat["id"] if cat else None

    existing = {}
    if category_id:
        existing = {
            g["name"]: g
            for g in canvas.get_all(
                "/courses/%s/groups" % course_id, group_category_id=category_id
            )
        }

    resolve_members(canvas.roster(course_id), plan)
    fail_lines = []

    for team in plan["teams"]:
        wanted = {}
        for m in team["members"]:
            if m.get("resolvedUserId"):
                wanted[m["resolvedUserId"]] = m["name"]
            else:
                fail_lines.append(
                    "OVERGESLAGEN - %s (%s) in %s: %s"
                    % (m["name"], m["email"], team["name"], m["resolveNote"])
                )

        group = existing.get(team["name"])
        if group:
            action = "bestaande groep (id %d)" % group["id"]
        else:
            if dry:
                group = {"id": None, "name": team["name"], "users": []}
                action = "zou aangemaakt worden"
            else:
                group = canvas.post(
                    "/group_categories/%s/groups" % category_id,
                    name=team["name"],
                )
                action = "aangemaakt (id %d)" % group["id"]
        team["canvasGroupId"] = group["id"]

        current = {u["id"] for u in (group.get("users") or [])}
        to_add = [u for u in wanted if u not in current]
        to_remove = sorted(current - set(wanted)) if options.get("removeExtra") else []
        if dry and cat is None:
            current = set()

        if not to_add and not to_remove:
            log.append("  %-10s %s - geen wijzigingen (%d leden)" % (team["name"], action, len(wanted)))
            continue

        if not dry:
            if to_add:
                add, refused = add_members(canvas, group["id"], to_add, wanted, team)
                for name, why in refused:
                    fail_lines.append(
                        "GEWEIGERD - %s in %s: %s" % (name, team["name"], why)
                    )
            if to_remove:
                remove_members(canvas, group["id"], to_remove)

        log.append(
            "  %-10s %s - %d lid%s toegevoegd%s"
            % (
                team["name"], action, len(to_add), "en" if len(to_add) != 1 else "",
                (", %d verwijderd" % len(to_remove)) if to_remove else "",
            )
        )
        time.sleep(0.1)

    return {
        "log": log,
        "failed": fail_lines,
        "dryRun": dry,
        "summary": "Group set '%s' | %d groepen | %d studenten toegevoegd"
        % (set_name, len(plan["teams"]), sum(len(t["members"]) for t in plan["teams"])),
    }


def do_verify(cfg):
    canvas = Canvas(cfg["baseUrl"], cfg["token"])
    course_id = canvas.resolve_course(cfg["courseId"])
    me = canvas.get("/users/self", include=["account_id"])
    course = canvas.get("/courses/%s" % course_id)
    cats = canvas.get_all("/courses/%s/group_categories" % course_id)
    detail = []
    for c in cats:
        groups = canvas.get_all(
            "/courses/%s/groups" % course_id, group_category_id=c["id"]
        )
        detail.append(
            {
                "name": c["name"],
                "id": c["id"],
                "groups": len(groups),
                "members": sum(g.get("members_count", 0) for g in groups),
            }
        )
    return {
        "user": me.get("name"),
        "course": course.get("name"),
        "course_code": course.get("course_code"),
        "categories": detail,
    }


# ---------------------------------------------------------------- HTTP

class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        pass

    def _send(self, code, payload, ctype="application/json"):
        if ctype == "application/json":
            body = json.dumps(payload, ensure_ascii=False).encode()
        else:
            body = payload.encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path in ("/", "/index.html"):
            with open(os.path.join(HERE, "index.html"), "rb") as f:
                return self._send(200, f.read().decode("utf-8"), "text/html; charset=utf-8")
        if self.path == "/api/config":
            return self._send(200, CONFIG)
        self._send(404, {"error": "not found"})

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        try:
            data = json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            return self._send(400, {"error": "Ongeldige JSON."})

        cfg = dict(CONFIG)
        for k in ("baseUrl", "token", "courseId"):
            if data.get(k):
                cfg[k] = str(data[k]).strip()

        try:
            if self.path == "/api/verify":
                return self._send(200, do_verify(cfg))

            if self.path == "/api/checkwrite":
                canvas = Canvas(cfg["baseUrl"], cfg["token"])
                cid = canvas.resolve_course(cfg["courseId"])
                check_write(canvas, cid)
                return self._send(200, {"ok": True,
                                        "message": "Schrijftoegang is in orde."})

            if self.path == "/api/preview":
                plan = build_plan(parse_csv(data.get("csv", "")))
                if plan.get("missingColumns"):
                    return self._send(200, {
                        "plan": plan,
                        "unmatched": [],
                        "canvas": {"connected": False, "error": ""},
                    })
                if not plan["teams"]:
                    return self._send(200, {"error": "Geen bruikbare rijen gevonden."})
                if not (cfg["baseUrl"] and cfg["token"] and cfg["courseId"]):
                    return self._send(200, {
                        "plan": plan,
                        "unmatched": [],
                        "canvas": {"connected": False,
                                   "error": "Vul URL, cursus en token in om te controleren."},
                    })
                try:
                    canvas = Canvas(cfg["baseUrl"], cfg["token"])
                    return self._send(200, do_preview(
                        canvas, canvas.resolve_course(cfg["courseId"]), plan
                    ))
                except RuntimeError as e:
                    return self._send(200, {
                        "plan": plan, "unmatched": [],
                        "canvas": {"connected": False, "error": str(e)},
                    })

            if self.path == "/api/push":
                return self._send(200, do_push(cfg, data.get("csv", ""), data.get("options", {})))

        except RuntimeError as e:
            return self._send(200, {"error": str(e)})
        except Exception as e:  # noqa: BLE001
            return self._send(500, {"error": "%s: %s" % (type(e).__name__, e)})

        self._send(404, {"error": "not found"})


if __name__ == "__main__":
    print("Canvas groepentool -> http://localhost:%d" % PORT)
    print("Stop met Ctrl+C\n")
    try:
        ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
    except KeyboardInterrupt:
        sys.exit(0)
