using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Domain.Tenancy;
using TopCut.Infrastructure.Persistence;

namespace TopCut.Infrastructure.Identity;

public sealed class AccountService(UserManager<TopCutUser> users, TimeProvider clock) : IAccountService
{
    public async Task<RegistrationResult> RegisterPlayerAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Enforced here rather than only in the form, so posting straight to the API cannot skip it.
        if (!request.ConsentAccepted)
        {
            return new RegistrationResult(false, null, ["Hay que aceptar el aviso de protección de datos."]);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return new RegistrationResult(false, null, ["Indica el nombre con el que quieres aparecer."]);
        }

        var user = new TopCutUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
            ConsentAcceptedAt = clock.GetUtcNow(),
            ConsentVersion = PrivacyNotice.CurrentVersion
        };

        var created = await users.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            return new RegistrationResult(false, null, [.. created.Errors.Select(e => e.Description)]);
        }

        await users.AddToRoleAsync(user, TopCutRoles.Player);

        return new RegistrationResult(true, user.Id, []);
    }
}

public sealed class StoreOnboarding(
    UserManager<TopCutUser> users,
    TopCutDbContext db) : IStoreOnboarding
{
    public async Task<OperationResult> ApplyAsync(
        string userId,
        StoreApplication application,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId);

        if (user is null)
        {
            return OperationResult.Fail("No existe esa cuenta.");
        }

        if (user.StoreId is not null)
        {
            return OperationResult.Fail("Esa cuenta ya tiene una tienda asociada.");
        }

        Store store;

        try
        {
            store = new Store(
                application.StoreName,
                new Address(
                    application.Street,
                    application.City,
                    application.Province,
                    application.PostalCode));
        }
        catch (DomainException error)
        {
            return OperationResult.Fail(error.Message);
        }

        db.Stores.Add(store);
        await db.SaveChangesAsync(cancellationToken);

        // The account gets the Store role right away, but the store is still PendingApproval, so
        // the CanOrganize policy keeps refusing until a Superadmin acts.
        user.StoreId = store.Id;
        await users.UpdateAsync(user);
        await users.AddToRoleAsync(user, TopCutRoles.Store);

        return OperationResult.Ok();
    }

    public Task<OperationResult> ApproveAsync(Guid storeId, CancellationToken cancellationToken = default) =>
        ChangeStatus(storeId, store => store.Approve(), cancellationToken);

    public Task<OperationResult> RevokeAsync(Guid storeId, CancellationToken cancellationToken = default) =>
        ChangeStatus(storeId, store => store.RevokeApproval(), cancellationToken);

    private async Task<OperationResult> ChangeStatus(
        Guid storeId,
        Action<Store> change,
        CancellationToken cancellationToken)
    {
        // The Superadmin carries no tenant claim, so this read has to cross the tenant boundary.
        // It is explicit on purpose (ADR-0011).
        var store = await db.Stores
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.Id == storeId, cancellationToken);

        if (store is null)
        {
            return OperationResult.Fail("No existe esa tienda.");
        }

        try
        {
            change(store);
        }
        catch (DomainException error)
        {
            return OperationResult.Fail(error.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
