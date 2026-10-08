# Reikalavimų dokumentas: SF ir darbų apskaitos sistema

## 1. Tikslas
Sukurti sistemą, kuri padėtų išrašyti sąskaitas faktūras (SF) klientams, remiantis realiai atliktais darbais ir jų trukme.

## 2. Sistemos paskirtis ir apimtis
Sistema turi:
- valdyti klientus ir projektus;
- fiksuoti atliktus darbus (darbų aktus);
- importuoti darbo laiką iš `app.clockify.me`;
- generuoti SF pagal pasirinktus aktus;
- eksportuoti aktus į Excel;
- eksportuoti SF į PDF.

## 3. Vartotojai ir prieiga
Sistemą naudoja vienas autentifikuotas naudotojas.

Prieigos taisyklės:
- prisijungęs naudotojas gali atlikti visus sistemos veiksmus;
- papildomi vaidmenys ir teisių skirstymas pagal vaidmenis nėra reikalingi.

## 4. Aukšto lygio funkciniai reikalavimai

### FR-1. Autentifikacija per Azure AD
1. Sistema privalo autentifikuoti naudotojus per Azure Active Directory (Microsoft Entra ID).
2. Neautentifikuoti naudotojai negali pasiekti sistemos funkcijų.
3. Prisijungus turi būti išlaikoma naudotojo sesija ir saugiai tvarkomi prisijungimo duomenys.

### FR-2. Klientų registras
1. Sistema privalo leisti kurti, peržiūrėti, redaguoti ir deaktyvuoti klientų įrašus.
2. Kliento įraše turi būti bent:
   - kliento pavadinimas;
   - įmonės kodas (jei taikoma);
   - PVM kodas (jei taikoma);
   - adresas;
   - kontaktiniai duomenys.
3. Turi būti galimybė ieškoti ir filtruoti klientus.

### FR-3. Projektų registras
1. Sistema privalo leisti kurti, peržiūrėti, redaguoti ir deaktyvuoti projektus.
2. Kiekvienas projektas turi būti susietas su konkrečiu klientu.
3. Projekte turi būti saugomas valandinis įkainis.
4. Projekte turi būti nurodoma projekto valiuta (numatytoji reikšmė - `EUR`).
5. Projekte turi būti nurodomas SF kalbos nustatymas (LT arba EN).
6. Projekte turi būti nurodomas naudojamas SF šablonas.
7. Turi būti galimybė matyti kliento projektų sąrašą.

### FR-4. Darbų aktas
1. Sistema privalo leisti sudaryti darbų aktą pagal atliktus darbus.
2. Akte turi būti fiksuojama:
   - darbų aprašymas;
   - atlikimo data;
   - trukmė (valandos/minutės);
   - valandinis įkainis;
   - suma valandomis;
   - suma eurais.
3. Sistema privalo automatiškai apskaičiuoti bendrą akto sumą valandomis ir eurais.
4. Turi būti galimybė koreguoti aktą, kol jis nėra įtrauktas į SF.
5. Jeigu aktas jau įtrauktas į bent vieną SF, jokie veiksmai su aktu nebegalimi (redagavimas, skaidymas, trynimas).
6. Turi būti galimybė skaidyti aktą į kelis aktus, bet tik jei pradinis aktas dar neįtrauktas į SF.

### FR-5. Laiko importas iš Clockify
1. Sistema privalo turėti integraciją su `app.clockify.me` darbo valandų importui.
2. Importo metu turi būti galima pasirinkti laikotarpį.
3. Importuoti įrašai turi būti susiejami su klientais/projektais (rankiniu arba taisyklių pagrindu, jei automatinis susiejimas neįmanomas).
4. Turi būti matomas importo rezultatas (kiek įrašų importuota, kiek atmesta, klaidos priežastys).

### FR-6. SF išrašymas pagal aktus
1. Sistema privalo leisti sukurti SF pasirenkant vieną ar kelis darbų aktus.
2. Sistema turi apsaugoti nuo dalinio akto panaudojimo SF: aktas į SF įtraukiamas tik pilna apimtimi (arba visas aktas, arba neįtraukiamas).
3. Kiekvienas į SF įtrauktas aktas turi būti vaizduojamas kaip atskira SF eilutė su valandomis, valandiniu įkainiu ir suma.
4. Sistema turi apsaugoti nuo to paties akto pakartotinio įtraukimo į skirtingas SF, jei aktas jau panaudotas.
5. Sistema privalo leisti sukurti SF ir be darbų aktų (pvz., sistemų priežiūros darbams).
6. SF turi būti galimybė pridėti rankinę eilutę (aprašymas, kiekis/vienetai, įkainis, suma).
7. SF turi būti automatiškai apskaičiuojamos sumos:
   - suma be PVM;
   - PVM suma (kai taikoma);
   - galutinė suma.
8. Prie SF eilutės turi būti galimybė nurodyti, kad PVM netaikomas.
9. SF juodraštis turi būti peržiūrimas prieš patvirtinimą.
10. Turi būti galimybė ištrinti SF tik tuo atveju, jei tai paskutinė išrašyta SF (negalima trinti SF iš numeracijos vidurio).

### FR-9. Sistemos nustatymai
1. Sistema privalo turėti SF numerio šablono nustatymą.
2. Sistema privalo turėti SF numerio skaitliuko (counter) nustatymą ir valdymą.
3. Sistema privalo turėti bendrą PVM procento nustatymą.

### FR-10. SF šablonų registras
1. Sistema privalo turėti atskirą SF šablonų registrą (atskira esybė).
2. Sistema privalo leisti kurti, peržiūrėti, redaguoti ir deaktyvuoti SF šablonus.
3. Kiekvienam SF šablonui turi būti nurodyta kalba (LT arba EN).
4. Projektas turi būti susiejamas su pasirinktu SF šablonu iš SF šablonų registro.

### FR-7. Darbų aktų eksportas į Excel
1. Sistema privalo leisti eksportuoti pasirinktus darbų aktus į Excel formatą (`.xlsx`).
2. Eksporte turi būti visa pagrindinė aktų informacija ir sumos.
3. Eksportas turi būti tinkamas dalijimuisi su klientu ar vidinei apskaitai.

### FR-8. SF eksportas į PDF
1. Sistema privalo leisti eksportuoti SF į PDF formatą.
2. PDF dokumentas turi būti tinkamas oficialiam pateikimui klientui.
3. PDF turi būti aiškiai atvaizduoti SF rekvizitai, eilutės, sumos ir mokėjimo informacija.

## 5. Pagrindiniai duomenų objektai
- **Naudotojas** (autentifikuotas per Azure AD)
- **Klientas**
- **Projektas** (susietas su klientu, turi valandinį įkainį)
- **Projektas** (susietas su klientu, turi valandinį įkainį, valiutą, SF kalbos nustatymą ir SF šabloną)
- **Darbo įrašas** (iš Clockify ar rankinis)
- **Darbų aktas**
- **Sąskaita faktūra**
- **Sistemos nustatymai** (SF numerio šablonas, SF numerio skaitliukas, PVM procentas)
- **SF šablonas** (atskira esybė, naudojama SF generavimui pagal projekto nustatymus)

## 6. Nefunkciniai reikalavimai
1. **Saugumas**:
   - autentifikacija tik per Azure AD;
   - veiksmų audito žurnalas (kas ir kada sukūrė/pakeitė aktą ar SF).
2. **Duomenų kokybė**:
   - validuojami privalomi laukai;
   - sumos skaičiuojamos automatiškai ir nuosekliai;
   - SF valiuta ir kalbos šablonas parenkami pagal projekto nustatymus.
3. **Našumas**:
   - pagrindinės sąrašo užklausos (klientai, projektai, aktai, SF) turi veikti sklandžiai prie tipinių apkrovų.
4. **Patogumas naudoti**:
   - aiškūs formų validacijos pranešimai;
   - paprastas SF formavimo srautas iš pasirinktų aktų.

## 7. Integracijos
1. **Microsoft Entra ID (Azure AD)** autentifikacijai.
2. **Clockify (`app.clockify.me`)** darbo laiko importui.

## 8. Pagrindiniai naudotojo scenarijai
1. Naudotojas prisijungia per Azure AD.
2. Sukuria arba atnaujina klientą.
3. Sukuria projektą ir nustato valandinį įkainį, valiutą (numatytai `EUR`), SF kalbą bei SF šabloną.
4. Importuoja darbo valandas iš Clockify.
5. Suformuoja darbų aktą pagal importuotus įrašus ir, jei reikia, suskaido aktą (kol jis dar neįtrauktas į SF).
6. Išrašo SF pasirinkdamas vieną ar kelis aktus, arba išrašo SF be aktų pridėdamas rankines eilutes.
7. Prireikus SF eilutėje pažymi, kad PVM netaikomas.
8. Eksportuoja aktą į Excel ir SF į PDF.

## 9. Priėmimo kriterijai (aukšto lygio)
1. Prisijungimas per Azure AD veikia ir neautentifikuoti naudotojai neįleidžiami.
2. Galima sukurti klientą ir projektą su valandiniu įkainiu.
3. Galima importuoti darbo laiką iš Clockify pasirinktame laikotarpyje.
4. Galima sukurti darbų aktą su automatiniais valandų ir sumų skaičiavimais, o aktą redaguoti tik kol jis neįtrauktas į SF.
5. Galima suskaidyti aktą į kelis tik iki jo įtraukimo į SF.
6. Galima sukurti SF iš kelių aktų, kur kiekvienas aktas įtraukiamas kaip pilna atskira eilutė.
7. Negalima išrašyti SF tik daliai akto.
8. Galima sukurti SF be darbų aktų ir pridėti rankines eilutes.
9. SF numeris formuojamas pagal nustatytą šabloną ir skaitliuką.
10. Galima ištrinti tik paskutinę SF.
11. Projekte galima nurodyti valiutą, numatytoji valiuta - `EUR`.
12. Projekte galima nurodyti SF kalbą (LT/EN) ir susietą SF šabloną.
13. Sistema turi atskirą SF šablonų registrą.
14. Galima eksportuoti aktus į `.xlsx` ir SF į `.pdf`.

## 10. Atviri klausimai (patikslinimui)
Šiuo metu atvirų klausimų nėra.
