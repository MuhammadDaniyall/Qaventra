using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HRMS.Api.Services;

public record NewPayment(decimal Amount, DateOnly PaidOn, int Periods, string? Method, string? Reference, string? Notes);

public class SubscriptionService(HrmsDbContext db, TenantAccessChecker access, ICurrentUser me)
{
    /// <summary>Months covered by one billing cycle.</summary>
    public static int Months(BillingCycle cycle) => (int)cycle;

    /// <summary>
    /// Records a payment and extends the subscription by whole billing cycles: from the day after the current end date,
    /// or from today if the subscription has already lapsed (or never started).
    /// </summary>
    public async Task<TenantPayment> RecordPaymentAsync(Guid tenantId, NewPayment input)
    {
        if (input.Amount <= 0)
            throw new DomainException("Amount must be greater than zero.");
        if (input.Periods is < 1 or > 36)
            throw new DomainException("Periods must be between 1 and 36.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        var cycle = tenant.BillingCycle ?? throw new DomainException("Set the company's billing cycle (monthly, quarterly, yearly) before recording a payment.");

        var today = access.LocalToday(tenant.TimeZoneId);
        var from = tenant.SubscriptionEndsOn is { } end && end >= today ? end.AddDays(1) : today;
        var to = from.AddMonths(Months(cycle) * input.Periods).AddDays(-1);

        var payment = new TenantPayment
        {
            TenantId = tenantId,
            Amount = input.Amount,
            PaidOn = input.PaidOn,
            PeriodFrom = from,
            PeriodTo = to,
            Method = input.Method?.Trim(),
            Reference = input.Reference?.Trim(),
            Notes = input.Notes?.Trim(),
            RecordedByUserId = me.UserId,
        };
        db.TenantPayments.Add(payment);
        tenant.SubscriptionEndsOn = to;
        await db.SaveChangesAsync();
        access.Invalidate(tenantId);
        return payment;
    }

    /// <summary>Permanently deletes the company and every row it owns. Audit logs are kept as the permanent record.</summary>
    public async Task DeleteCompanyAsync(Guid tenantId, string confirmName)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        if (!string.Equals(tenant.Name.Trim(), confirmName?.Trim(), StringComparison.Ordinal))
            throw new DomainException("Type the company name exactly to confirm deletion.");

        await using var tx = await db.Database.BeginTransactionAsync();

        // Built from the EF model so tables added later are covered without touching this method.
        var tables = CompanyTables();
        Task Run(string sql) => db.Database.ExecuteSqlRawAsync(sql, tenantId);

        // 1. Clear optional links into company rows (department head <-> employee, managers, self references) so no cycle blocks the deletes.
        foreach (var link in db.Model.GetEntityTypes().Where(t => !t.IsOwned() && t.GetTableName() != null)
                     .SelectMany(t => t.GetForeignKeys()).Where(fk => tables.ContainsKey(fk.PrincipalEntityType) && fk.Properties.Any(p => p.IsNullable)))
        {
            var table = Table(link.DeclaringEntityType);
            var set = string.Join(", ", link.Properties.Where(p => p.IsNullable).Select(p => $"a.{Column(p, table)} = NULL"));
            await Run($"UPDATE a SET {set} FROM {Name(table)} a WHERE {Match(link, "a", 0, tables)}");
        }

        // 2. Delete the company's rows table by table; a table still referenced by another (SQL error 547) is retried in the next pass.
        var left = tables.Keys.ToList();
        while (left.Count > 0)
        {
            var before = left.Count;
            SqlException? blocked = null;
            foreach (var type in left.ToList())
            {
                try
                {
                    await Run($"DELETE a FROM {Name(Table(type))} a WHERE {tables[type]("a", 0)}");
                    left.Remove(type);
                }
                catch (SqlException e) when (e.Number == 547)
                {
                    blocked = e;
                }
            }
            if (blocked != null && left.Count == before)
                throw blocked;
        }

        // 3. The subscription records and the company itself.
        await db.TenantPayments.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();

        await tx.CommitAsync();
        access.Invalidate(tenantId);
    }

    /// <summary>
    /// Every table holding company rows, with a SQL condition (for a table alias) that picks this company's rows: tenant tables by TenantId,
    /// child tables without their own TenantId (payslip lines, salary components, template items) through a required link to a company row.
    /// The condition takes the company id as parameter {0}.
    /// </summary>
    private Dictionary<IEntityType, Func<string, int, string>> CompanyTables()
    {
        var types = db.Model.GetEntityTypes().Where(t => !t.IsOwned() && t.GetTableName() != null).ToList();
        var tables = new Dictionary<IEntityType, Func<string, int, string>>();
        foreach (var type in types.Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
        {
            var column = Column(type.FindProperty(nameof(ITenantEntity.TenantId))!, Table(type));
            tables[type] = (alias, _) => $"{alias}.{column} = {{0}}";
        }

        bool added;
        do
        {
            added = false;
            foreach (var type in types.Where(t => !tables.ContainsKey(t)))
            {
                var links = type.GetForeignKeys().Where(fk => fk.IsRequired && tables.ContainsKey(fk.PrincipalEntityType)).ToList();
                if (links.Count == 0)
                    continue;
                tables[type] = (alias, depth) => "(" + string.Join(" OR ", links.Select(fk => Match(fk, alias, depth, tables))) + ")";
                added = true;
            }
        } while (added);
        return tables;
    }

    /// <summary>SQL condition: the row <paramref name="alias"/> points through <paramref name="fk"/> at a row of this company.</summary>
    private static string Match(IForeignKey fk, string alias, int depth, Dictionary<IEntityType, Func<string, int, string>> tables)
    {
        var parent = $"p{depth}";
        var parentTable = Table(fk.PrincipalEntityType);
        var childTable = Table(fk.DeclaringEntityType);
        var join = string.Join(" AND ", fk.Properties.Zip(fk.PrincipalKey.Properties,
            (child, key) => $"{parent}.{Column(key, parentTable)} = {alias}.{Column(child, childTable)}"));
        return $"EXISTS (SELECT 1 FROM {Name(parentTable)} {parent} WHERE {join} AND {tables[fk.PrincipalEntityType](parent, depth + 1)})";
    }

    private static StoreObjectIdentifier Table(IEntityType type) => StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());

    private static string Name(StoreObjectIdentifier table) => table.Schema is null ? Quote(table.Name) : $"{Quote(table.Schema)}.{Quote(table.Name)}";

    private static string Column(IProperty property, StoreObjectIdentifier table) => Quote(property.GetColumnName(table)!);

    private static string Quote(string name) => $"[{name.Replace("]", "]]")}]";
}
