﻿using OpenIZ.Core.Model.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using MARC.HI.EHRS.SVC.Core.Data;
using OpenIZ.Persistence.Data.MSSQL.Data;
using System.Linq.Expressions;
using OpenIZ.Persistence.Data.MSSQL.Exceptions;
using System.Reflection;
using OpenIZ.Core.Model;
using MARC.HI.EHRS.SVC.Core.Services.Security;
using System.Security.Principal;
using System.Diagnostics;

namespace OpenIZ.Persistence.Data.MSSQL.Services.Persistence
{
    /// <summary>
    /// A persistence service which can persist and query security user objects
    /// </summary>
    public class SecurityUserPersistenceService : BaseDataPersistenceService<Core.Model.Security.SecurityUser>
    {


        /// <summary>
        /// Perform a get operation
        /// </summary>
        /// <param name="containerId">The identifier of the container to retrieve</param>
        /// <param name="principal">The authorization context</param>
        /// <param name="loadFast">True if the history and historical data should be loaded</param>
        /// <param name="dataContext">The data context</param>
        /// <returns>The security user as part of the get</returns>
        internal override Core.Model.Security.SecurityUser Get(Identifier<Guid> containerId, IPrincipal principal, bool loadFast, ModelDataContext dataContext)
        {
            var dataUser = dataContext.SecurityUsers.FirstOrDefault(o => o.UserId == containerId.Id);

            if (dataUser != null)
                return this.ConvertToModel(dataUser);
            else
                return null;
        }

        /// <summary>
        /// Perform an insert
        /// </summary>
        /// <param name="storageData">The model class to be stored</param>
        /// <param name="principal">The authorization context</param>
        /// <param name="dataContext">The data context</param>
        /// <returns>The security user which was inserted</returns>
        internal override Core.Model.Security.SecurityUser Insert(Core.Model.Security.SecurityUser storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key != default(Guid)) // Trying to insert an already inserted user?
                throw new SqlFormalConstraintException(SqlFormalConstraintType.IdentityInsert);

            if (storageData.DelayLoad) // We want a frozen asset
                storageData = storageData.AsFrozen() as Core.Model.Security.SecurityUser;

            var dataUser = this.ConvertFromModel(storageData) as Data.SecurityUser;
            dataUser.CreatedBy = principal == null ? null : (Guid?)base.GetUserFromPrincipal(principal, dataContext);
            dataContext.SecurityUsers.InsertOnSubmit(dataUser);
            dataUser.SecurityStamp = Guid.NewGuid().ToString();

            if (storageData.Roles != null)
                dataContext.SecurityUserRoles.InsertAllOnSubmit(storageData.Roles.Select(r => new SecurityUserRole() { RoleId = r.EnsureExists(principal, dataContext).Key, SecurityUser = dataUser }));

            // Persist data to the db
            dataContext.SubmitChanges();

            return this.ConvertToModel(dataUser);
        }

        /// <summary>
        /// Perform an obsolete
        /// </summary>
        /// <param name="storageData">The data object to be obsoleted</param>
        /// <param name="principal">The authorization context under which the security user is obsolete</param>
        /// <param name="dataContext">The current data context</param>
        /// <returns>The obsoleted user</returns>
        internal override Core.Model.Security.SecurityUser Obsolete(Core.Model.Security.SecurityUser storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key == default(Guid))
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            if (storageData.DelayLoad) // We want a frozen asset
                storageData = storageData.AsFrozen() as Core.Model.Security.SecurityUser;

            var dataUser = dataContext.SecurityUsers.FirstOrDefault(u => u.UserId == storageData.Key);
            if (dataUser == null)
                throw new KeyNotFoundException();

            dataUser.ObsoletedBy = base.GetUserFromPrincipal(principal, dataContext);
            dataUser.ObsoletionTime = DateTimeOffset.Now;
            dataUser.SecurityStamp = Guid.NewGuid().ToString();

            // Persist data to the db
            dataContext.SubmitChanges();

            return this.ConvertToModel(dataUser);
        }

        /// <summary>
        /// Perform a query 
        /// </summary>
        internal override IQueryable<Core.Model.Security.SecurityUser> Query(Expression<Func<Core.Model.Security.SecurityUser, bool>> query, IPrincipal principal, ModelDataContext dataContext)
        {
            var queryExpression = s_mapper.MapModelExpression<Core.Model.Security.SecurityUser, Data.SecurityUser>(query);
            this.m_traceSource.TraceInformation("MSSQL: {0}: QUERY Tx {1} > {2}", this.GetType().Name, query, queryExpression);

            return dataContext.SecurityUsers.Where(queryExpression).Select(o => this.ConvertToModel(o));
        }

        /// <summary>
        /// Perform an update
        /// </summary>
        internal override Core.Model.Security.SecurityUser Update(Core.Model.Security.SecurityUser storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key == default(Guid))
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            if (storageData.DelayLoad) // We want a frozen asset
                storageData = storageData.AsFrozen() as Core.Model.Security.SecurityUser;

            var dataUser = dataContext.SecurityUsers.FirstOrDefault(u => u.UserId == storageData.Key);
            if (dataUser == null)
                throw new KeyNotFoundException();

            var newData = this.ConvertFromModel(storageData) as Data.SecurityUser;
            base.UpdatePropertyData(dataUser, newData);

            dataUser.UpdatedBy = base.GetUserFromPrincipal(principal, dataContext);
            dataUser.UpdatedTime = DateTimeOffset.Now;
            dataUser.SecurityStamp = Guid.NewGuid().ToString();

            // Roles add/remove
            var currentRoleIds = dataUser.SecurityUserRoles.Select(r => r.RoleId).ToArray();
            var remRoles = currentRoleIds.Where(r=>!(bool)storageData.Roles?.Select(mr=>mr.Key).Contains(r));
            var addRoles = storageData.Roles?.Where(r => !currentRoleIds.Contains(r.Key));
            // Remove all the roles 
            dataContext.SecurityUserRoles.DeleteAllOnSubmit(dataContext.SecurityUserRoles.Where(r => remRoles.Contains(r.RoleId) && r.UserId == dataUser.UserId));
            if(addRoles != null)
               dataContext.SecurityUserRoles.InsertAllOnSubmit(addRoles?.Select(r => new Data.SecurityUserRole() { RoleId = r.EnsureExists(principal, dataContext).Key, UserId = dataUser.UserId }));
              
            // Persist data to the db
            dataContext.SubmitChanges();

            return this.ConvertToModel(dataUser);
        }

        /// <summary>
        /// Convert a model class into a data class
        /// </summary>
        internal override object ConvertFromModel(Core.Model.Security.SecurityUser model)
        {
            return s_mapper.MapModelInstance<Core.Model.Security.SecurityUser, Data.SecurityUser>(model);
        }

        /// <summary>
        /// Convert data to model
        /// </summary>
        internal override Core.Model.Security.SecurityUser ConvertToModel(object data)
        {
            return s_mapper.MapDomainInstance<Data.SecurityUser, Core.Model.Security.SecurityUser>(data as Data.SecurityUser);
        }
    }
}