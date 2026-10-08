# DAL pasirinkimo svarstymai

Šis dokumentas aprašo galimus duomenų prieigos sluoksnio variantus projektui, kai reikia dirbti su MySQL ir turėti gerą suderinamumą su AOT.

## 1. Trumpa išvada

Jei AOT yra svarbus tikslas, saugiausias pasirinkimas yra:

- `MySqlConnector` kaip MySQL duomenų prieigos pagrindas;
- `Dapper` skaitymui ir sąrašams;
- `EF Core` tik tada, jei reikia patogaus rašymo sluoksnio ir priimtini AOT apribojimai.

Jei reikia vieno aiškaus sprendimo, rekomenduočiau:

1. **Pagrindinis variantas:** `Dapper + MySqlConnector`
2. **ORM variantas:** `EF Core + MySQL provideris`
3. **Hibridinis variantas:** `Dapper` skaitymui, `EF Core` rašymui
4. **Alternatyva su PoC:** `TORM`

## 2. Variantai

### 2.1. Dapper + MySqlConnector

Tai yra lengviausias ir AOT draugiškiausias kelias. Čia pats valdai SQL, o Dapper tik sumapina rezultatą į C# objektus.

Privalumai:

- geras suderinamumas su AOT;
- aiškus ir greitas SQL;
- paprasta daryti filtrus, rūšiavimą ir puslapiavimą;
- mažai magijos.

Trūkumai:

- daugiau rankinio SQL;
- nėra automatinio change tracking;
- daugiau atsakomybės dėl transakcijų ir modelio sinchronizavimo.

#### Filtrų pavyzdys

```csharp
public sealed record InvoiceFilter(
    string? Search,
    Guid? ClientId,
    Guid? ProjectId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 20);

public sealed record InvoiceListRow(
    Guid Id,
    string Number,
    string ClientName,
    string ProjectName,
    DateOnly IssueDate,
    decimal TotalAmount);
```

```csharp
using Dapper;
using MySqlConnector;

public sealed class InvoiceReadRepository
{
    private readonly MySqlDataSource dataSource;

    public InvoiceReadRepository(MySqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<IReadOnlyList<InvoiceListRow>> SearchAsync(InvoiceFilter filter, CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            where.Add("(i.Number LIKE CONCAT('%', @Search, '%') OR c.Name LIKE CONCAT('%', @Search, '%') OR p.Name LIKE CONCAT('%', @Search, '%'))");
            parameters.Add("Search", filter.Search);
        }

        if (filter.ClientId is not null)
        {
            where.Add("i.ClientId = @ClientId");
            parameters.Add("ClientId", filter.ClientId);
        }

        if (filter.ProjectId is not null)
        {
            where.Add("i.ProjectId = @ProjectId");
            parameters.Add("ProjectId", filter.ProjectId);
        }

        if (filter.FromDate is not null)
        {
            where.Add("i.IssueDate >= @FromDate");
            parameters.Add("FromDate", filter.FromDate);
        }

        if (filter.ToDate is not null)
        {
            where.Add("i.IssueDate <= @ToDate");
            parameters.Add("ToDate", filter.ToDate);
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var offset = Math.Max(filter.Page - 1, 0) * filter.PageSize;

        parameters.Add("PageSize", filter.PageSize);
        parameters.Add("Offset", offset);

        var sql = $"""
            SELECT
                i.Id,
                i.Number,
                c.Name AS ClientName,
                COALESCE(p.Name, '') AS ProjectName,
                i.IssueDate,
                i.TotalAmount
            FROM Invoices i
            INNER JOIN Clients c ON c.Id = i.ClientId
            LEFT JOIN Projects p ON p.Id = i.ProjectId
            {whereSql}
            ORDER BY i.IssueDate DESC, i.Number DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<InvoiceListRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }
}
```

### 2.2. EF Core + MySQL provideris

Tai patogus variantas, kai norisi mažiau rankinio SQL ir patogesnio domeno darbo. Tačiau su AOT reikia būti atsargiems.

Privalumai:

- patogus rašymas CRUD scenarijams;
- change tracking;
- lengviau modeliuoti ryšius ir agregatus;
- geras pasirinkimas komandų pusėje.

Trūkumai:

- AOT atveju ribojimai yra realesni;
- reikia compiled model ir dažnai precompiled queries;
- reikia vengti lazy loading, proxy ir kitų dinamiškų funkcijų.

#### EF Core pavyzdys

```csharp
public async Task<Invoice?> GetAsync(Guid id, CancellationToken cancellationToken)
{
    return await dbContext.Invoices
        .Include(x => x.Lines)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
```

```csharp
public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken)
{
    dbContext.Invoices.Update(invoice);
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### 2.3. Hibridinis variantas

Tai praktiškiausias pasirinkimas tavo sistemai, jei nori išnaudoti abiejų pasaulių stiprybes.

Skaitymui naudok `Dapper`, o rašymui `EF Core`.

Tinka, kai:

- sąrašai ir filtrai turi būti greiti ir aiškūs;
- formų išsaugojimas gali naudoti ORM patogumą;
- reikia mažiau rizikos AOT pusėje nei pilnai priklausant nuo EF Core visur.

#### Hibridinio varianto pavyzdys

```csharp
public sealed class InvoiceService
{
    private readonly InvoiceReadRepository readRepository;
    private readonly AccountingDbContext dbContext;

    public InvoiceService(InvoiceReadRepository readRepository, AccountingDbContext dbContext)
    {
        this.readRepository = readRepository;
        this.dbContext = dbContext;
    }

    public Task<IReadOnlyList<InvoiceListRow>> SearchAsync(InvoiceFilter filter, CancellationToken cancellationToken)
        => readRepository.SearchAsync(filter, cancellationToken);

    public async Task<Guid> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = request.Number,
            IssueDate = request.IssueDate
        };

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}
```

### 2.4. TORM (ThingsGateway)

`TORM` yra lengvas, našumui orientuotas ORM, kuris deklaruoja AOT palaikymą ir pilną MySQL palaikymą.

Privalumai:

- deklaruojamas AOT palaikymas;
- deklaruojamas pilnas MySQL palaikymas;
- turi LINQ stiliaus užklausas, Code First ir batch operacijas;
- orientuotas į našumą ir turi benchmark pavyzdžius.

Rizikos:

- palyginti mažas projekto brandos lygis (nedidelė bendruomenė, mažiau battle-tested scenarijų);
- mažiau nepriklausomo turinio ir mažiau plačiai naudojamų pavyzdžių nei su `Dapper` ar `EF Core`;
- mažesnis ekosistemos stabilumo tikrumas ilgalaikiam verslo projektui.

Kada verta rinktis:

- jei prioritetas yra našumas ir nori ORM API, o ne rankinio SQL;
- jei sutinki pradėti nuo trumpo PoC ir patikrinti realius use-case savo duomenų modeliui.

Siūlomas PoC prieš sprendimą:

1. Įgyvendinti vieną realų sąskaitų filtravimo endpointą.
2. Įgyvendinti vieną Create/Update scenarijų su transakcija.
3. Pamatuoti našumą ir deploy dydį su AOT build.
4. Patikrinti migracijų ir schemos valdymo patikimumą tavo aplinkoje.

#### TORM pavyzdžiai

Žemiau pateikti pavyzdžiai orientuoti į tavo scenarijų: sąskaitų filtravimas ir kūrimas.

```csharp
using TORM;

// Bazinė ORM kliento konfigūracija (MySQL)
using var ormClient = new OrmClient(new OrmConnectionConfig
{
    DatabaseType = OrmDbType.MySql,
    ConnectionString = "Server=localhost;Database=apskaita;Uid=root;Pwd=password;"
});
```

```csharp
using TORM;

[OrmTable(TableName = "invoices")]
public sealed class InvoiceEntity
{
    [OrmColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; }

    [OrmColumn(Length = 50)]
    public string Number { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal TotalAmount { get; set; }

    [OrmColumn(Length = 20)]
    public string Status { get; set; } = "DRAFT";
}

public sealed record InvoiceFilter(
    string? Search,
    Guid? ClientId,
    Guid? ProjectId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

public static async Task<object> SearchInvoicesAsync(OrmClient ormClient, InvoiceFilter filter)
{
    var query = ormClient.Queryable<InvoiceEntity>();

    if (!string.IsNullOrWhiteSpace(filter.Search))
    {
        query = query.Where(x => x.Number.Contains(filter.Search));
    }

    if (filter.ClientId is not null)
    {
        var clientId = filter.ClientId.Value;
        query = query.Where(x => x.ClientId == clientId);
    }

    if (filter.ProjectId is not null)
    {
        var projectId = filter.ProjectId.Value;
        query = query.Where(x => x.ProjectId == projectId);
    }

    if (filter.FromDate is not null)
    {
        var from = filter.FromDate.Value;
        query = query.Where(x => x.IssueDate >= from);
    }

    if (filter.ToDate is not null)
    {
        var to = filter.ToDate.Value;
        query = query.Where(x => x.IssueDate <= to);
    }

    // Grąžina puslapį ir bendrą įrašų skaičių
    var page = await query
        .OrderByDescending(x => x.IssueDate)
        .ToPageListAsync(pageNumber: filter.Page, pageSize: filter.PageSize);

    return page;
}
```

```csharp
using TORM;

public static async Task<Guid> CreateInvoiceAsync(OrmClient ormClient, Guid clientId, Guid projectId, string number)
{
    var invoiceId = Guid.NewGuid();

    await ormClient.UseTranAsync(async () =>
    {
        var invoice = new InvoiceEntity
        {
            Id = invoiceId,
            Number = number,
            ClientId = clientId,
            ProjectId = projectId,
            IssueDate = DateTime.UtcNow,
            Status = "DRAFT",
            TotalAmount = 0m
        };

        await ormClient.Insertable(invoice).ExecuteAsync();

        // Čia galėtum įterpti ir InvoiceLine eilutes bei perskaičiuoti TotalAmount
        await ormClient
            .Updatable<InvoiceEntity>()
            .Set(x => x.TotalAmount, 1250.00m)
            .Where(x => x.Id == invoiceId)
            .ExecuteAsync();
    });

    return invoiceId;
}
```

Pastaba: prieš naudojant produkcijoje verta patikrinti, kaip TORM tavo aplinkoje tvarko migracijas/schemos pokyčius ir ar AOT build veikia be papildomų rankinių konfigūracijų.

## 3. Ką rinkčiausi šiam projektui

Šiam projektui rinkčiausi tokį skaidymą:

- sąrašai, filtrai, paieška, eksportai ir ataskaitos per `Dapper`;
- komandų scenarijai, jei patogu, per `EF Core`;
- jei vėliau paaiškės, kad AOT yra labai griežtas reikalavimas, komandų pusę taip pat galima perkelti į `Dapper`;
- `TORM` matyčiau kaip papildomą kandidatą tik po trumpo PoC, o ne kaip pirmą pasirinkimą be validacijos.

## 4. Praktinė taisyklė

Jei vienas endpointas grąžina sąrašą, beveik visada geriau naudoti `Dapper`.

Jei endpointas keičia kelių tarpusavyje susijusių objektų būseną, patogiau pradėti nuo `EF Core`.

Jei AOT tampa absoliučiai svarbus, ilgainiui viską galima sutraukti į `MySqlConnector + Dapper`.