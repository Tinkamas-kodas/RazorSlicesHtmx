# Time-tracking feature planas

## 1. Tikslas
Sukurti naują Time-tracking modulį, kuris atkartoja Clockify tipo laiko pildymą savaitės vaizde:
- savaitės dienos rodomos kaip stulpeliai;
- eilutės kas 30 minučių;
- vizualiai išskiriamas darbo laikas 08:00-17:00;
- galima spausti ant celės ir pildyti laiką dialoge;
- jei įrašas jau yra, jį galima atidaryti ir redaguoti.

## 2. Pirmos iteracijos funkcijų sąrašas

### F-01. Savaitės grid vaizdas
- Rodomas 7 dienų vaizdas (pirmadienis-sekmadienis) su pasirenkama savaite.
- Laiko ašis 00:00-24:00 kas 30 min.
- Didesni laiko intervalų skyrikliai ties pilnomis valandomis.

### F-02. Darbo laiko zonos vizualizacija
- Intervalas 08:00-17:00 pažymėtas aktyvesniu fonu.
- Ne darbo laikas rodomas prislopintai (pilkesnis fonas).
- Darbo laiko juostos veikia tik kaip UI indikacija, ne verslo apribojimas.

### F-03. Celės pasirinkimas pele
- Paspaudus ant tuščios celės atidaromas dialogas su prefill:
  - data;
  - start laikas;
  - numatyta trukmė 30 min.
- Pasirinkta celė paryškinama iki dialogo uždarymo.

### F-04. Įrašo kūrimo dialogas (Clockify tipo)
- Laukai:
  - klientas;
  - projektas;
  - veiklos aprašymas;
  - start laikas;
  - end laikas arba trukmė;
  - žyma (nebūtina).
- Validacija:
  - end > start;
  - leidžiama kirsti vidurnaktį;
  - privalomi klientas/projektas/aprašymas.

### F-05. Įrašo rodymas gride
- Sukurtas įrašas atvaizduojamas atitinkamoje dienoje/laiko intervale.
- Įrašo blokas rodo:
  - projekto pavadinimą;
  - trukmę;
  - trumpą aprašą.
- Blokai spalvinami pagal projektą (MVP: pastovi spalva per projektą).

### F-06. Įrašo redagavimas
- Paspaudus ant esamo bloko atidaromas tas pats dialogas edit režimu.
- Galima keisti visus laukus.
- Išsaugojus grid persipiešia be pilno puslapio perkrovimo.

### F-07. Įrašo trynimas
- Edit dialoge mygtukas Trinti.
- Prieš trynimą pateikiamas patvirtinimo žingsnis.

### F-08. Savaitės navigacija
- Mygtukai: Ankstesnė savaitė, Ši savaitė, Kita savaitė.
- Header rodo intervalo datas (pvz. 2026-08-03 - 2026-08-09).

### F-09. Dienos ir savaitės sumos
- Kiekvienai dienai rodoma bendra trukmė.
- Viršuje ar apačioje rodoma savaitės bendra trukmė.

### F-10. Konfliktų tikrinimas
- Persidengiantys įrašai leidžiami tam pačiam naudotojui.
- Persidengimo atveju papildomi pranešimai nerodomi.

## 3. Duomenų modelio pradžia

### WorkActLine (esamas pagrindinis įrašas)
- Vienas pildymo įrašas atitinka vieną `WorkActLine`.
- `WorkActLine` turi `TimeTrackings` sąrašą (dažniausiai 1 segmentas).

### TimeTracking (segmentas)
- Id (Guid)
- WorkActLineId (Guid)
- StartedAtLocal (datetime)
- EndedAtLocal (datetime)

### Vidurnakčio taisyklė
- Jei naudotojo įrašas kerta vidurnaktį, sistema sukuria:
  - 1 `WorkActLine`;
  - 2 `TimeTracking` segmentus:
    - nuo pradžios iki 23:59:59;
    - nuo 00:00:00 iki pabaigos.

## 4. API/endpoint planas (MVP)
- GET /time-tracking/list?weekStart=yyyy-MM-dd
- GET /time-tracking/create?date=...&time=...
- POST /time-tracking/create
- GET /time-tracking/edit/{id}
- POST /time-tracking/edit/{id}
- POST /time-tracking/delete/{id}

Pastaba: fragmentams naudoti Result.Fragment(...), feature-level ekranams Result.Page(...).

## 5. UI slice planas
- _ListPage.cshtml (savaitės karkasas)
- _Grid.cshtml (dienų stulpeliai ir 30 min eilutės)
- _EntryBlock.cshtml (vienas įrašo blokas)
- _CreateEditDialog.cshtml (modal form)
- _DeleteDialog.cshtml (patvirtinimas)

## 6. Verslo taisyklės (MVP)
- Vienas įrašas gali kirsti vidurnaktį.
- Jei įrašas kerta vidurnaktį, jis saugomas kaip 1 `WorkActLine` su 2 `TimeTracking` segmentais.
- Trukmė skaičiuojama minutėmis, UI rodo h:mm.
- Persidengimai leidžiami.
- Neaktyvūs klientai/projektai naujam įrašui nesiūlomi.

## 7. Įgyvendinimo etapai
1. Modelis + migracija + repository užklausos.
2. List/Grid su savaitės navigacija ir read-only įrašų atvaizdavimu.
3. Create dialogas iš celės paspaudimo.
4. Edit/Delete.
5. Konfliktų validacija ir sumos.
6. UX polishing (drag/select ateityje, ne MVP).

## 8. Ne MVP (vėlesniam etapui)
- Drag-to-select intervalo kūrimas pele.
- Drag-and-drop perstumti įrašą laike.
- Bulk copy į kitą dieną/savaitę.
- Integracija/sinchronizacija su Clockify API dvikrypčiai.
