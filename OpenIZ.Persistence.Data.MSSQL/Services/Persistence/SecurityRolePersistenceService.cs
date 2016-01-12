﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using MARC.HI.EHRS.SVC.Core.Data;
using OpenIZ.Core.Model.Security;
using OpenIZ.Persistence.Data.MSSQL.Data;
using System.Diagnostics;
using OpenIZ.Persistence.Data.MSSQL.Exceptions;
using System.Security.Principal;

namespace OpenIZ.Persistence.Data.MSSQL.Services.Persistence
{
    /// <summary>
    /// Security role persistence service
    /// </summary>
    public class SecurityRolePersistenceService : BaseDataPersistenceService<Core.Model.Security.SecurityRole>
    {
        /// <summary>
        /// Perform a get operation
        /// </summary>
        internal override Core.Model.Security.SecurityRole Get(Identifier<Guid> containerId, IPrincipal principal, bool loadFast, ModelDataContext dataContext)
        {
            if (containerId == null)
                throw new ArgumentNullException(nameof(containerId));

            var dataRole = dataContext.SecurityRoles.FirstOrDefault(o => o.RoleId == containerId.Id);

            if (dataRole != null)
                return this.ConvertToModel(dataRole);
            else
                return null;
        }

        /// <summary>
        /// Insert the security role
        /// </summary>
        internal override Core.Model.Security.SecurityRole Insert(Core.Model.Security.SecurityRole storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key != default(Guid)) // Trying to insert an already inserted user?
                throw new SqlFormalConstraintException(SqlFormalConstraintType.IdentityInsert);
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            if (storageData.DelayLoad) // We want a frozen asset
                storageData = storageData.AsFrozen() as Core.Model.Security.SecurityRole;

            var dataRole = this.ConvertFromModel(storageData) as Data.SecurityRole;
            dataRole.CreatedBy = base.GetUserFromPrincipal(principal, dataContext);
            dataContext.SecurityRoles.InsertOnSubmit(dataRole);
            
            if (storageData.Users != null)
                dataContext.SecurityUserRoles.InsertAllOnSubmit(storageData.Users.Select(u => new SecurityUserRole() { UserId = u.EnsureExists(principal, dataContext).Key, SecurityRole = dataRole }));

            // Policies
            dataContext.SecurityRolePolicies.InsertAllOnSubmit(storageData.Policies.Select(p => new SecurityRolePolicy()
            {
                IsDeny = p.GrantType < MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Grant,
                CanOverride = p.GrantType > MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Deny,
                PolicyId = p.Policy.EnsureExists(principal, dataContext).Key,
                SecurityRole = dataRole
            }));

            // Persist data to the db
            dataContext.SubmitChanges();

            return this.ConvertToModel(dataRole);
        }

        /// <summary>
        /// Obsolete an existing role
        /// </summary>
        internal override Core.Model.Security.SecurityRole Obsolete(Core.Model.Security.SecurityRole storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key == Guid.Empty)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            var dataRole = dataContext.SecurityRoles.FirstOrDefault(r => r.RoleId == storageData.Key);
            dataRole.ObsoletedBy = base.GetUserFromPrincipal(principal, dataContext);
            dataRole.ObsoletionTime = DateTimeOffset.Now;
            
            // Persist
            dataContext.SubmitChanges();

            return this.ConvertToModel(dataRole);
        }

        /// <summary>
        /// Perform a query
        /// </summary>
        internal override IQueryable<Core.Model.Security.SecurityRole> Query(Expression<Func<Core.Model.Security.SecurityRole, bool>> query, IPrincipal principal, ModelDataContext dataContext)
        {
            var domainQuery = s_mapper.MapModelExpression<Core.Model.Security.SecurityRole, Data.SecurityRole>(query);
            this.m_traceSource.TraceInformation("MSSQL: {0}: QUERY Tx {1} > {2}", this.GetType().Name, query, domainQuery);
            return dataContext.SecurityRoles.Where(domainQuery).Select(o => this.ConvertToModel(o));
        }
        
        /// <summary>
        /// Update the security role
        /// </summary>
        internal override Core.Model.Security.SecurityRole Update(Core.Model.Security.SecurityRole storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key == Guid.Empty)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            var dataRole = dataContext.SecurityRoles.FirstOrDefault(r => r.RoleId == storageData.Key);
            var newData = this.ConvertFromModel(storageData) as Data.SecurityRole;
            base.UpdatePropertyData(dataRole, newData);

            // Users to be added 
            if(storageData.Users != null)
            {
                var currentUserIds = dataRole.SecurityUserRoles.Select(u => u.UserId).ToArray();
                var addUsers = storageData.Users?.Where(r => !currentUserIds.Contains(r.Key));
                var remUsers = currentUserIds.Where(i => !storageData.Users.Select(u => u.Key).Contains(i));
                dataContext.SecurityUserRoles.InsertAllOnSubmit(addUsers.Select(r => new SecurityUserRole() { SecurityRole = dataRole, UserId = r.Key }));
                dataContext.SecurityUserRoles.DeleteAllOnSubmit(dataRole.SecurityUserRoles.Where(o => remUsers.Contains(o.UserId)));
            }

            // Update or insert policies
            foreach(var p in storageData.Policies)
            {
                var existingPolicy = dataRole.SecurityRolePolicies.FirstOrDefault(o => o.PolicyId == p.Policy.EnsureExists(principal, dataContext).Key);
                if (existingPolicy != null)
                {
                    existingPolicy.IsDeny = p.GrantType < MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Grant;
                    existingPolicy.CanOverride = p.GrantType > MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Deny;
                }
                else
                {
                    dataContext.SecurityRolePolicies.InsertOnSubmit(new SecurityRolePolicy()
                    {
                        IsDeny = p.GrantType < MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Grant,
                        CanOverride = p.GrantType > MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Deny,
                        PolicyId = p.Policy.EnsureExists(principal, dataContext).Key,
                        SecurityRole = dataRole
                    });
                }
            }

            // Remove any removed policies
            var currentPolicyIds = dataRole.SecurityRolePolicies.Select(p => p.PolicyId);
            var remPolicies = currentPolicyIds.Where(i => !storageData.Policies.Select(p => p.Policy.Key).Contains(i));
            dataContext.SecurityRolePolicies.DeleteAllOnSubmit(dataRole.SecurityRolePolicies.Where(p => remPolicies.Contains(p.PolicyId)));

            dataContext.SubmitChanges();

            return this.ConvertToModel(dataRole);
        }

        /// <summary>
        /// Convert a data item from model
        /// </summary>
        internal override object ConvertFromModel(Core.Model.Security.SecurityRole model)
        {
            return s_mapper.MapModelInstance<Core.Model.Security.SecurityRole, Data.SecurityRole>(model);

        }

        /// <summary>
        /// Convert to model
        /// </summary>
        internal override Core.Model.Security.SecurityRole ConvertToModel(object data)
        {
            var securityRole = data as Data.SecurityRole;
            var retVal = s_mapper.MapDomainInstance<Data.SecurityRole, Core.Model.Security.SecurityRole>(securityRole);
            // No delay load on policies
            retVal.Policies.AddRange(securityRole.SecurityRolePolicies.Select(p => new SecurityPolicyInstance()
            {
                Policy = new SecurityPolicyPersistenceService().ConvertToModel(p.Policy),
                GrantType =
                    p.IsDeny && p.CanOverride ? MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Elevate :
                    p.IsDeny ? MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Deny :
                    MARC.HI.EHRS.SVC.Core.Services.Policy.PolicyDecisionOutcomeType.Grant
            }));
            return retVal;
        }

    }
}