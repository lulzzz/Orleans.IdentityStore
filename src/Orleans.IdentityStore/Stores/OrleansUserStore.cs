﻿using Microsoft.AspNetCore.Identity;
using Orleans.IdentityStore.Grains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Orleans.IdentityStore.Stores
{
    public class OrleansUserStore<TUser, TRole, TUserClaim, TUserLogin, TUserToken, TRoleClaim> :
        UserStoreBase<TUser, TRole, Guid, TUserClaim, IdentityUserRole<Guid>, TUserLogin, TUserToken, TRoleClaim>
        where TUser : IdentityUser<Guid>
        where TRole : IdentityRole<Guid>
        where TUserClaim : IdentityUserClaim<Guid>, new()
        where TUserLogin : IdentityUserLogin<Guid>, new()
        where TUserToken : IdentityUserToken<Guid>, new()
        where TRoleClaim : IdentityRoleClaim<Guid>, new()
    {
        private const string ValueCannotBeNullOrEmpty = "Value cannot be null or empty";
        private readonly IClusterClient _client;
        private readonly IRoleClaimStore<TRole> _roleStore;

        public OrleansUserStore(IClusterClient client, IRoleClaimStore<TRole> roleStore, IdentityErrorDescriber describer = null) : base(describer)
        {
            _client = client;
            _roleStore = roleStore;
        }

        public override IQueryable<TUser> Users => throw new NotSupportedException();

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            if (claims.Any())
            {
                return UserGrain(user.Id).AddClaims(claims.Select(c => CreateUserClaim(user, c)).ToList());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (login == null)
            {
                throw new ArgumentNullException(nameof(login));
            }

            return UserGrain(user.Id).AddLogin(CreateUserLogin(user, login));
        }

        /// <summary>
        /// Adds the given <paramref name="normalizedRoleName"/> to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the role to.</param>
        /// <param name="normalizedRoleName">The role to add.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException(ValueCannotBeNullOrEmpty, nameof(normalizedRoleName));
            }

            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (role != null)
            {
                await UserGrain(user.Id).AddToRole(role.Id);
            }
        }

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see
        /// cref="IdentityResult"/> of the creation operation.
        /// </returns>
        public override Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.Id == default)
            {
                user.Id = Guid.NewGuid();
            }

            return UserGrain(user.Id).Create(user);
        }

        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see
        /// cref="IdentityResult"/> of the update operation.
        /// </returns>
        public override Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return UserGrain(user.Id).Delete();
        }

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if
        /// any associated with the specified normalized email address.
        /// </returns>
        public override async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var id = await _client.GetGrain<IIdentityUserByEmailGrain>(normalizedEmail).GetId();
            if (id != null)
                return await UserGrain(id.Value).Get();

            return null;
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user
        /// matching the specified <paramref name="userId"/> if it exists.
        /// </returns>
        public override Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            return UserGrain(ConvertIdFromString(userId)).Get();
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified normalized user name.
        /// </summary>
        /// <param name="normalizedUserName">The normalized user name to search for.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user
        /// matching the specified <paramref name="normalizedUserName"/> if it exists.
        /// </returns>
        public override async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var id = await _client.GetGrain<IIdentityUserByNameGrain>(normalizedUserName).GetId();
            if (id != null)
                return await UserGrain(id.Value).Get();

            return null;
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose claims should be retrieved.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a user.</returns>
        public override async Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return (await UserGrain(user.Id).GetClaims())?
                .Select(c => c.ToClaim())
                .ToList() ?? new List<Claim>();
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see
        /// cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public override async Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return (await UserGrain(user.Id).GetLogins())?
                .Select(l => new UserLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName))
                .ToList() ?? new List<UserLoginInfo>();
        }

        /// <summary>
        /// Retrieves the roles the specified <paramref name="user"/> is a member of.
        /// </summary>
        /// <param name="user">The user whose roles should be retrieved.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that contains the roles the user is a member of.
        /// </returns>
        public override async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return (await UserGrain(user.Id).GetRoles())?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Retrieves all users with the specified claim.
        /// </summary>
        /// <param name="claim">The claim whose users should be retrieved.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that contain the specified claim.
        /// </returns>
        public override async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var ids = await _client.GetGrain<IIdentityClaimGrain>(claim.GetGrainKey()).GetUserIds();

            return (await Task.WhenAll(ids.Select(i => UserGrain(i).Get()))).ToList();
        }

        /// <summary>
        /// Retrieves all users in the specified role.
        /// </summary>
        /// <param name="normalizedRoleName">The role whose users should be retrieved.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that are in the specified role.
        /// </returns>
        public override async Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(normalizedRoleName))
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);
            var users = await _client.GetGrain<IIdentityRoleGrain<TRole>>(role.Id).GetUsers();

            return (await Task.WhenAll(users.Select(u => UserGrain(u).Get()))).ToList();
        }

        /// <summary>
        /// Returns a flag indicating if the specified user is a member of the give <paramref name="normalizedRoleName"/>.
        /// </summary>
        /// <param name="user">The user whose role membership should be checked.</param>
        /// <param name="normalizedRoleName">The role to check membership of</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> containing a flag indicating if the specified user is a
        /// member of the given group. If the user is a member of the group the returned value with
        /// be true, otherwise it will be false.
        /// </returns>
        public override async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException(ValueCannotBeNullOrEmpty, nameof(normalizedRoleName));
            }
            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (role != null)
            {
                return await UserGrain(user.Id).ContainsRole(role.Id);
            }

            return false;
        }

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            if (claims.Any())
            {
                return UserGrain(user.Id).RemoveClaims(claims.ToList());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes the given <paramref name="normalizedRoleName"/> from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the role from.</param>
        /// <param name="normalizedRoleName">The role to remove.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException(ValueCannotBeNullOrEmpty, nameof(normalizedRoleName));
            }

            var role = await FindRoleAsync(normalizedRoleName, cancellationToken);
            if (role != null)
            {
                await UserGrain(user.Id).RemoveRole(role.Id);
            }
        }

        /// <summary>
        /// Removes the <paramref name="loginProvider"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the login from.</param>
        /// <param name="loginProvider">The login to remove from the user.</param>
        /// <param name="providerKey">
        /// The key provided by the <paramref name="loginProvider"/> to identify a user.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return UserGrain(user.Id).RemoveLogin(loginProvider, providerKey);
        }

        /// <summary>
        /// Replaces the <paramref name="claim"/> on the specified <paramref name="user"/>, with the
        /// <paramref name="newClaim"/>.
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim replace.</param>
        /// <param name="newClaim">The new claim replacing the <paramref name="claim"/>.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }
            if (newClaim == null)
            {
                throw new ArgumentNullException(nameof(newClaim));
            }

            return UserGrain(user.Id).ReplaceClaims(claim, newClaim);
        }

        /// <summary>
        /// Updates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation
        /// should be canceled.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see
        /// cref="IdentityResult"/> of the update operation.
        /// </returns>
        public override Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.Id == default)
            {
                throw new ArgumentException("Id cannot be default", nameof(user.Id));
            }

            return UserGrain(user.Id).Update(user);
        }

        protected override Task AddUserTokenAsync(TUserToken token)
        {
            ThrowIfDisposed();
            throw new NotImplementedException();
        }

        protected override Task<TRole> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return _roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);
        }

        protected override Task<TUserToken> FindTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            return UserGrain(user.Id).GetToken(loginProvider, name);
        }

        protected override Task<TUser> FindUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return UserGrain(userId).Get();
        }

        protected override Task<TUserLogin> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task<TUserLogin> FindUserLoginAsync(Guid userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override async Task<IdentityUserRole<Guid>> FindUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
        {
            if (await UserGrain(userId).ContainsRole(roleId))
            {
                return new IdentityUserRole<Guid>
                {
                    RoleId = roleId,
                    UserId = userId
                };
            }
            return null;
        }

        protected override Task RemoveUserTokenAsync(TUserToken token)
        {
            throw new NotImplementedException();
        }

        private IIdentityUserGrain<TUser, TRole, TUserClaim, TUserLogin, TUserToken, TRoleClaim> UserGrain(Guid id)
        {
            return _client.GetGrain<IIdentityUserGrain<TUser, TRole, TUserClaim, TUserLogin, TUserToken, TRoleClaim>>(id);
        }
    }
}