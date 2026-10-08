# Duomenų modelio juodraštis

Šis dokumentas aprašo pradinį loginį duomenų modelį pagal [requirements.md](requirements.md) reikalavimus.

## 1. Modelio tikslas
- Turėti aiškias esybes SF išrašymui iš darbų aktų ir be jų.
- Užtikrinti verslo taisykles: jokio dalinio akto įtraukimo, akto neliečiamumas po įtraukimo į SF, SF numeracijos kontrolė.
- Palaikyti projekto lygmens valiutą, SF kalbą ir SF šabloną.

## 1.1 Komponentai
Modelyje leidžiama naudoti pakartotinai naudojamus komponentus (embedded value objects), kad būtų išvengta tų pačių laukų dubliavimo.

### CompanyInfo
Paskirtis: įmonės rekvizitų blokas, naudojamas tiek `Company`, tiek `Client`, tiek `Issuer`, tiek `Invoice`.

Laukai:
- Name (string, max 200, not null)
- CompanyCode (string, max 50, null)
- VatCode (string, max 50, null)
- AddressLine1 (string, max 200, null)
- AddressLine2 (string, max 200, null)
- City (string, max 100, null)
- PostalCode (string, max 20, null)
- CountryCode (string, max 3, null)
- ContactName (string, max 200, null)
- ContactEmail (string, max 254, null)
- ContactPhone (string, max 50, null)
- BankName (string, max 200, null)
- BankAccount (string, max 34, null)
- BankSwift (string, max 11, null)
- VatPayerCode (string, max 50, null)

## 1.2 Paveldėjimas
- `Company` yra bazinė esybė.
- `Client` paveldi `Company`.
- `Issuer` paveldi `Company`.
- `CompanyInfo` yra bendras rekvizitų komponentas šioms esybėms ir jų snapshotams.

## 1.3 Max ilgių santrauka
| Max ilgis | Laukai |
| --- | --- |
| 254 | ContactEmail, Email |
| 200 | CompanyInfo.Name, AddressLine1, AddressLine2, ContactName, BankName, DisplayName, Project.Name, InvoiceTemplate.Name, Unit.Name |
| 100 | EntraObjectId, City, ExternalId, InvoiceNumberTemplate, Description, PrefixContext |
| 50 | CompanyCode, VatCode, ContactPhone, VatPayerCode, Unit.Code, InvoiceTemplate.Code, Project.Code, WorkAct.Number, Invoice.Number |
| 34 | BankAccount |
| 20 | PostalCode |
| 11 | BankSwift |
| 3 | CountryCode, LanguageCode, CurrencyCode |
| 500 | WorkActLine.Description, InvoiceLine.Description |
| 1000 | TotalAmountInText, WorkActLine.Notes, Invoice.Notes |
| 20000 | InvoiceTemplate.Content |

### Unit
Paskirtis: vienetų klasifikatorius, naudojamas SF eilutėse.

Laukai:
- Id (uuid, PK)
- Code (string, max 50, unique, not null)
- Name (string, max 200, not null)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

## 2. Esybių sąrašas
1. User
2. Company
3. Client
4. Issuer
5. InvoiceTemplate
6. Project
7. WorkAct
8. WorkActLine
9. Unit
10. Invoice
11. InvoiceLine
12. SystemSettings
13. InvoiceSequenceState

## 3. Esybės ir laukai

### 3.1 User
Paskirtis: autentifikuotas naudotojas per Azure AD (šiuo metu vienas naudotojas).

Laukai:
- Id (uuid, PK)
- EntraObjectId (string, max 100, unique, not null)
- DisplayName (string, max 200, not null)
- Email (string, max 254, null)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- LastLoginAtUtc (datetime, null)

Pastabos:
- Nors naudotojas vienas, esybė paliekama auditui ir ateities plėtrai.

### 3.2 Company
Paskirtis: bazinė įmonės esybė.

Laukai:
- Id (uuid, PK)
- Info (CompanyInfo, not null)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

### 3.3 Client
Paskirtis: klientų registras (Company specializacija).

Laukai:
- Paveldi `Company`.
- Papildomų laukų šiame juodraštyje nėra.

### 3.4 Issuer
Paskirtis: išrašanti įmonė (Company specializacija).

Laukai:
- Paveldi `Company`.
- Papildomų laukų šiame juodraštyje nėra.

### 3.5 InvoiceTemplate
Paskirtis: SF šablonų registras (atskira esybė).

Laukai:
- Id (uuid, PK)
- Code (string, max 50, unique, not null)
- Name (string, max 200, not null)
- LanguageCode (string(3), not null, ISO 639-2, pvz. LTU, ENG)
- Content (text, not null)
- IsDefault (bool, not null, default false)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Pastabos:
- Jei reikia versijavimo, galima pridėti Version ir VersionStatus.

### 3.6 Project
Paskirtis: projektų registras su finansiniais ir SF nustatymais.

Laukai:
- Id (uuid, PK)
- ClientId (uuid, FK -> Client.Id, not null)
- Name (string, max 200, not null)
- Code (string, max 50, null)
- HourlyRate (decimal(18, 4), not null)
- CurrencyCode (string(3), not null, default EUR, ISO 4217)
- InvoiceTemplateId (uuid, FK -> InvoiceTemplate.Id, not null)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Verslo taisyklės:
- HourlyRate gali būti keičiamas projekto eigoje.
- Pakeistas HourlyRate taikomas tik naujai kuriamoms eilutėms; istoriniai įkainiai saugomi WorkActLine.HourlyRate.
- Projekto SF kalba nustatoma pagal susieto InvoiceTemplate.LanguageCode.

### 3.7 WorkAct
Paskirtis: darbų aktas.

Laukai:
- Id (uuid, PK)
- Number (string, max 50, unique, not null)
- ProjectId (uuid, FK -> Project.Id, not null)
- ClientId (uuid, FK -> Client.Id, not null)
- CurrencyCode (string(3), not null)
- TotalHours (decimal(18, 2), not null)
- TotalAmount (decimal(18, 2), not null)
- Status (enum: DRAFT, FINAL, INVOICED, not null, default DRAFT)
- InvoicedAtUtc (datetime, null)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Pastabos:
- Suskaidant aktą, sukuriami du nauji aktai.

### 3.8 WorkActLine
Paskirtis: darbų akto eilutės.

Laukai:
- Id (uuid, PK)
- WorkActId (uuid, FK -> WorkAct.Id, not null)
- ExternalId (string, max 100, null)
- Source (enum: CLOCKIFY, MANUAL, not null)
- Description (string, max 500, not null)
- WorkDate (date, not null)
- DurationInHours (decimal(18, 2), not null)
- Notes (string, max 1000, null)
- HourlyRate (decimal(18, 4), not null)
- Amount (decimal(18, 2), not null)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Skaičiavimo taisyklė:
- Amount = round(DurationInHours * HourlyRate, 2)

### 3.9 Invoice
Paskirtis: sąskaita faktūra.

Laukai:
- Id (uuid, PK)
- Number (string, max 50, unique, not null)
- SequenceNumber (int, unique, not null)
- IssuerId (uuid, FK -> Issuer.Id, not null)
- IssuerInfo (CompanyInfo, not null)
- ClientId (uuid, FK -> Client.Id, not null)
- ClientInfo (CompanyInfo, not null)
- ProjectId (uuid, FK -> Project.Id, null)
- CurrencyCode (string(3), not null)
- LanguageCode (string(3), not null, ISO 639-2)
- InvoiceTemplateId (uuid, FK -> InvoiceTemplate.Id, not null)
- IssueDate (date, not null)
- DueDate (date, null)
- SubtotalAmount (decimal(18, 2), not null)
- VatAmount (decimal(18, 2), not null)
- TotalAmount (decimal(18, 2), not null)
- TotalAmountInText (string, max 1000, not null)
- Status (enum: DRAFT, ISSUED, CANCELLED, not null, default DRAFT)
- Notes (string, max 1000, null)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Verslo taisyklės:
- Trinti leidžiama tik paskutinę SF pagal SequenceNumber.
- Išrašyta SF saugo ir išrašytojo, ir kliento rekvizitų kopijas bei nekinta net jei master duomenys vėliau redaguojami.
- Išrašyta SF saugo kliento rekvizitų kopiją ir nekinta net jei Client įrašas vėliau redaguojamas.
- Kliento kopija SF turi atspindėti išrašymo momentu galiojusius duomenis.
- Invoice.IssuerInfo turi būti užpildomas iš Issuer.Info išrašymo momentu.
- Invoice.ClientInfo turi būti užpildomas iš Client.Info išrašymo momentu.

### 3.10 InvoiceLine
Paskirtis: SF eilutės (iš aktų arba rankinės).

Laukai:
- Id (uuid, PK)
- InvoiceId (uuid, FK -> Invoice.Id, not null)
- LineNo (int, not null)
- SourceType (enum: WORK_ACT, MANUAL, not null)
- WorkActId (uuid, FK -> WorkAct.Id, null)
- Description (string, max 500, not null)
- Quantity (decimal(18, 2), not null)
- UnitId (uuid, FK -> Unit.Id, null)
- UnitPrice (decimal(18, 4), not null)
- Amount (decimal(18, 2), not null)
- VatApplicable (bool, not null, default true)
- VatRatePercent (decimal(5, 2), not null)
- VatAmount (decimal(18, 2), not null)
- TotalAmount (decimal(18, 2), not null)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Verslo taisyklės:
- Jei SourceType=WORK_ACT, WorkActId privalomas ir Quantity turi atitikti visą akto valandų kiekį.
- Jei SourceType=MANUAL, WorkActId turi būti null.
- Jei UnitId nurodytas, jis turi rodyti į aktyvų Unit klasifikatoriaus įrašą.
- Vienas WorkAct gali būti panaudotas tik vienoje InvoiceLine eilutėje.

### 3.11 Unit
Paskirtis: vienetų klasifikatorius SF eilutėms.

Laukai:
- Id (uuid, PK)
- Code (string, max 50, unique, not null)
- Name (string, max 200, not null)
- IsActive (bool, not null, default true)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

### 3.12 SystemSettings
Paskirtis: sistemos lygmens nustatymai.

Laukai:
- Id (int, PK, visada 1)
- DefaultVatRatePercent (decimal(5, 2), not null)
- InvoiceNumberTemplate (string, max 100, not null)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Pastabos:
- InvoiceNumberTemplate pavyzdys: SF-{yyyy}-{####}.

### 3.13 InvoiceSequenceState
Paskirtis: SF numeracijos skaitliuko būsena.

Laukai:
- Id (int, PK, visada 1)
- CurrentValue (int, not null)
- PrefixContext (string, max 50, null)
- ResetRule (enum: NONE, YEARLY, MONTHLY, not null, default YEARLY)
- CreatedAtUtc (datetime, not null)
- CreatedByUserId (uuid, FK -> User.Id, not null)
- UpdatedAtUtc (datetime, not null)
- UpdatedByUserId (uuid, FK -> User.Id, not null)

Pastabos:
- Numerio generavimas turi būti transakcinis, kad nebūtų dublikatų.

## 4. Ryšiai
- Company yra bazinė esybė; Client ir Issuer yra jos specializacijos.
1. Client 1:N Project
2. Client 1:N WorkAct
3. Project 1:N WorkAct
4. WorkAct 1:N WorkActLine
5. Issuer 1:N Invoice
6. Unit 1:N InvoiceLine
7. Invoice 1:N InvoiceLine
8. WorkAct 0..1 : 1 InvoiceLine (pilno akto taisyklė)
9. InvoiceTemplate 1:N Project
10. InvoiceTemplate 1:N Invoice

## 5. Pagrindinės verslo taisyklės (DB + aplikacijos lygmuo)
1. Aktas gali būti įtrauktas į SF tik pilnai.
2. Jei aktas panaudotas SF, aktas ir jo eilutės tampa nekeičiami.
3. Akto skaidymas leidžiamas tik kai aktas dar nenaudotas SF.
4. SF galima sukurti be aktų, tik su MANUAL eilutėmis.
5. Kiekvienai SF eilutei galima nustatyti PVM netaikymą.
6. SF trynimas leidžiamas tik jei SF.SequenceNumber yra maksimalus.
7. Project.CurrencyCode pagal nutylėjimą EUR.
8. Invoice kalba nustatoma pagal InvoiceTemplate.LanguageCode.
9. Clockify/importo identifikatorius saugomas WorkActLine.ExternalId.
10. Invoice turi saugoti išrašytojo rekvizitų kopiją ir nuorodą į Issuer.
11. InvoiceLine.UnitId, jei nurodytas, turi rodyti į aktyvų Unit įrašą.
12. Invoice turi saugoti ir kliento rekvizitų kopiją bei nuorodą į Client.

## 6. Rekomenduojami indeksai
- Client(Name)
- Project(ClientId, IsActive)
- Company(IsActive)
- Issuer(IsActive)
- WorkAct(ProjectId, Status)
- WorkActLine(WorkActId, WorkDate)
- Unit(Code) unique
- Invoice(IssuerId, IssueDate)
- Invoice(ClientId, IssueDate)
- Invoice(SequenceNumber) unique
- InvoiceLine(InvoiceId, LineNo) unique
- InvoiceLine(WorkActId) unique where WorkActId is not null

## 7. Minimalūs DB apribojimai
- CHECK: CurrencyCode ilgis = 3.
- CHECK: LanguageCode ilgis = 3.
- CHECK: HourlyRate >= 0, DurationInHours >= 0, Amount >= 0.
- CHECK: VatRatePercent tarp 0 ir 100.
- CHECK: CompanyInfo.CountryCode ilgis = 2 arba 3 pagal pasirinktą koduotę.
- CHECK: InvoiceLine.SourceType=WORK_ACT -> WorkActId not null.
- CHECK: InvoiceLine.SourceType=MANUAL -> WorkActId is null.
- CHECK: InvoiceLine.UnitId is null or points to active Unit.
- FK apribojimai su aiškiomis ON DELETE taisyklėmis:
  - Master duomenims dažniausiai RESTRICT.
  - Eilučių duomenims (Invoice -> InvoiceLine, WorkAct -> WorkActLine) CASCADE tik jei atitinka verslo taisykles.

## 8. Atviri modelio klausimai
1. Ar WorkAct.Number reikia atskiro skaitliuko (kaip SF), ar pakanka datos+projekto formato?
2. Ar Status DRAFT/FINAL reikalingi WorkAct, jei patvirtinimo žingsnis pašalintas (galima palikti tik OPEN/INVOICED)?
3. Ar reikia saugoti išsiųsto PDF failo versiją (blob/file storage nuoroda), ar generuoti dinamiškai kiekvieną kartą?
