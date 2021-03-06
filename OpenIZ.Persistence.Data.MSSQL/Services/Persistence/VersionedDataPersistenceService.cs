﻿/*
 * Copyright 2015-2017 Mohawk College of Applied Arts and Technology
 *
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: justi
 * Date: 2016-8-2
 */
using OpenIZ.Core.Model;
using OpenIZ.Persistence.Data.MSSQL.Data;
using OpenIZ.Persistence.Data.MSSQL.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using OpenIZ.Core.Model.DataTypes;
using MARC.HI.EHRS.SVC.Core;
using MARC.HI.EHRS.SVC.Core.Services;
using System.ComponentModel;
using MARC.HI.EHRS.SVC.Core.Data;
using OpenIZ.Core.Services;
using OpenIZ.Core;
using System.Data.Linq.Mapping;
using System.Reflection;
using System.Linq.Expressions;

namespace OpenIZ.Persistence.Data.MSSQL.Services.Persistence
{
    /// <summary>
    /// Versioned domain data
    /// </summary>
    public abstract class VersionedDataPersistenceService<TModel, TDomain, TDomainKey> : BaseDataPersistenceService<TModel, TDomain> 
        where TDomain : class, IDbVersionedData<TDomainKey>, new() 
        where TModel : VersionedEntityData<TModel>, new()
        where TDomainKey : class, IDbIdentified, new()
    {
        
        /// <summary>
        /// Insert the data
        /// </summary>
        public override TModel Insert(ModelDataContext context, TModel data, IPrincipal principal)
        {

            // first we map the TDataKey entity
            var nonVersionedPortion = m_mapper.MapModelInstance<TModel, TDomainKey>(data);
            
            // Domain object
            var domainObject = this.FromModelInstance(data, context, principal) as TDomain;
            domainObject.NonVersionedObject = nonVersionedPortion;
            if (nonVersionedPortion.Id == Guid.Empty &&
                domainObject.Id != Guid.Empty)
                nonVersionedPortion.Id = domainObject.Id;

            if (nonVersionedPortion.Id == null ||
                nonVersionedPortion.Id == Guid.Empty)
            {
                data.Key = Guid.NewGuid();
                nonVersionedPortion.Id = data.Key.Value;
            }
            if (domainObject.VersionId == null ||
                domainObject.VersionId == Guid.Empty)
            {
                data.VersionKey = Guid.NewGuid();
                domainObject.VersionId = data.VersionKey.Value;
            }

            // Ensure created by exists
            data.CreatedBy?.EnsureExists(context, principal);
            data.CreatedByKey = domainObject.CreatedBy = domainObject.CreatedBy == Guid.Empty ? principal.GetUser(context).UserId : domainObject.CreatedBy;
            context.GetTable<TDomain>().InsertOnSubmit(domainObject);

            context.SubmitChanges();

            data.VersionSequence = domainObject.VersionSequenceId;
            data.VersionKey = domainObject.VersionId;
            data.Key = domainObject.Id;
            data.CreationTime = (DateTimeOffset)domainObject.CreationTime;

            return data;

        }

        /// <summary>
        /// Update the data with new version information
        /// </summary>
        public override TModel Update(ModelDataContext context, TModel data, IPrincipal principal)
        {

            if (data.Key == Guid.Empty)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);

            // This is technically an insert and not an update
            var existingObject = context.GetTable<TDomain>().FirstOrDefault(ExpressionRewriter.Rewrite<TDomain>(o => o.Id == data.Key && !o.ObsoletionTime.HasValue)); // Get the last version (current)
            if (existingObject == null)
                throw new KeyNotFoundException(data.Key.ToString());
            else if (existingObject.IsReadonly)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.UpdatedReadonlyObject);

            // Map existing
            var storageInstance = this.FromModelInstance(data, context, principal);

            // Create a new version
            var user = principal.GetUser(context);
            var newEntityVersion = new TDomain();
            newEntityVersion.CopyObjectData(storageInstance);

            // Client did not change on update, so we need to update!!!
            if (!data.VersionKey.HasValue ||
                data.VersionKey.Value == existingObject.VersionId) 
                data.VersionKey = newEntityVersion.VersionId = Guid.NewGuid();

            data.VersionSequence = newEntityVersion.VersionSequenceId = default(Decimal);
            newEntityVersion.Id = data.Key.Value;
            data.PreviousVersionKey = newEntityVersion.ReplacesVersionId = existingObject.VersionId;
            data.CreatedByKey = newEntityVersion.CreatedBy = user.UserId;
            // Obsolete the old version 
            existingObject.ObsoletedBy = user.UserId;
            existingObject.ObsoletionTime = DateTime.Now;
            context.GetTable<TDomain>().InsertOnSubmit(newEntityVersion);
            context.SubmitChanges();

            // Pull database generated fields
            data.VersionSequence = newEntityVersion.VersionSequenceId;
            data.CreationTime = newEntityVersion.CreationTime;

            return data;
            //return base.Update(context, data, principal);
        }

        /// <summary>
        /// Perform a version aware get
        /// </summary>
        internal override TModel Get(ModelDataContext context, Guid key, IPrincipal principal)
        {
            return this.Query(context, o => o.Key == key && o.ObsoletionTime == null, principal)?.FirstOrDefault();

        }

        /// <summary>
        /// Gets the specified object
        /// </summary>
        public override TModel Get<TIdentifier>(MARC.HI.EHRS.SVC.Core.Data.Identifier<TIdentifier> containerId, IPrincipal principal, bool loadFast)
        {
            var tr = 0;
            var uuid = containerId as Identifier<Guid>;

            if (uuid.Id != Guid.Empty)
            {
                var cacheItem = ApplicationContext.Current.GetService<IDataCachingService>()?.GetCacheItem<TModel>(uuid.Id) as TModel;
                if (cacheItem != null && (cacheItem.VersionKey.HasValue && uuid.VersionId == cacheItem.VersionKey.Value || uuid.VersionId == Guid.Empty))
                    return cacheItem;
            }

            // Get most recent version
            if (uuid.VersionId == Guid.Empty)
                return base.Query(o => o.Key == uuid.Id && o.ObsoletionTime == null, 0, 1, principal, out tr).FirstOrDefault();
            else
                return base.Query(o => o.Key == uuid.Id && o.VersionKey == uuid.VersionId, 0, 1, principal, out tr).FirstOrDefault();
        }

        /// <summary>
        /// Update versioned association items
        /// </summary>
        internal virtual void UpdateVersionedAssociatedItems<TAssociation, TDomainAssociation>(IEnumerable<TAssociation> storage, TModel source, ModelDataContext context, IPrincipal principal)
            where TAssociation : VersionedAssociation<TModel>, new()
            where TDomainAssociation : class, IDbVersionedAssociation, new()
        {
            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<TAssociation>>() as SqlServerBasePersistenceService<TAssociation>;
            if (persistenceService == null)
            {
                this.m_tracer.TraceEvent(System.Diagnostics.TraceEventType.Information, 0, "Missing persister for type {0}", typeof(TAssociation).Name);
                return;
            }

            Dictionary<Guid, Decimal> sourceVersionMaps = new Dictionary<Guid, decimal>();

            // Ensure the source key is set
            foreach (var itm in storage)
                if (itm.SourceEntityKey == Guid.Empty ||
                    itm.SourceEntityKey == null)
                    itm.SourceEntityKey = source.Key;
                else if(itm.SourceEntityKey != source.Key && !sourceVersionMaps.ContainsKey(itm.SourceEntityKey??Guid.Empty)) // The source comes from somewhere else
                { 
                    // First we have our association type, we need to get the property that is 
                    // linked on the association to get the map to the underlying SQL table
                    var domainType = m_mapper.MapModelType(typeof(TDomainAssociation));
                    var mappedProperty = domainType.GetRuntimeProperty("AssociatedItemKey").GetCustomAttribute<LinqPropertyMapAttribute>().LinqMember;
                    // Next we want to get the association entity which is linked to this key identifier
                    var rpi = domainType.GetRuntimeProperties().FirstOrDefault(o=>o.GetCustomAttribute<AssociationAttribute>()?.ThisKey == mappedProperty);
                    // Now we want to switch the type to the entity that is linked to the key so we can 
                    // get ist primary key
                    domainType = rpi.PropertyType;
                    var pkey = domainType.GetRuntimeProperties().FirstOrDefault(o => o.GetCustomAttribute<ColumnAttribute>()?.IsPrimaryKey == true);
                    // Now we want to get the key that we should query by that is version independent
                    domainType = typeof(TDomain);
                    pkey = domainType.GetRuntimeProperty(pkey.Name);
                    // Construct a LINQ expression to query the db
                    var parm = Expression.Parameter(domainType);
                    var delegateType = typeof(Func<,>).MakeGenericType(domainType, typeof(bool));
                    var predicate = Expression.Lambda(delegateType, Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(parm, pkey), Expression.Constant(itm.SourceEntityKey.Value)), parm);
                    // Get the SQL table instance and filter
                    var table = context.GetTable(domainType);
                    var tableEnum = table.Provider.Execute(table.Expression);
                    var methodInfo = typeof(Queryable).GetGenericMethod("FirstOrDefault", new Type[] { domainType }, new Type[]
                    {
                        typeof(IQueryable<>).MakeGenericType(domainType),
                        typeof(Expression<>).MakeGenericType(delegateType)
                    });
                    var result = methodInfo.Invoke(null, new Object[] { tableEnum, predicate }) as IDbVersionedData;
                    sourceVersionMaps.Add(itm.SourceEntityKey.Value, result.VersionSequenceId);

                    //var whereMethod = 
                    //var result = context.GetTable(domainType).Provider.Execute(predicate);
                }

            // Get existing
            // TODO: What happens which this is reverse?
            var existing = context.GetTable<TDomainAssociation>().Where(ExpressionRewriter.Rewrite<TDomainAssociation>(o => o.AssociatedItemKey == source.Key && source.VersionSequence >= o.EffectiveVersionSequenceId && (source.VersionSequence < o.ObsoleteVersionSequenceId || !o.ObsoleteVersionSequenceId.HasValue))).ToList().Select(o => m_mapper.MapDomainInstance<TDomainAssociation, TAssociation>(o) as TAssociation);
            
            // Remove old
            var obsoleteRecords = existing.Where(o => !storage.Any(ecn => ecn.Key == o.Key));
            foreach (var del in obsoleteRecords)
            {
                decimal obsVersion = 0;
                if (!sourceVersionMaps.TryGetValue(del.SourceEntityKey.Value, out obsVersion))
                    obsVersion = source.VersionSequence.GetValueOrDefault();
                del.ObsoleteVersionSequenceId = obsVersion;
                persistenceService.Update(context, del, principal);
            }

            // Update those that need it
            var updateRecords = storage.Where(o => existing.Any(ecn => ecn.Key == o.Key && o.Key != Guid.Empty && o != ecn));
            foreach (var upd in updateRecords)
                persistenceService.Update(context, upd, principal);

            // Insert those that do not exist
            var insertRecords = storage.Where(o => !existing.Any(ecn => ecn.Key == o.Key));
            foreach (var ins in insertRecords)
            {
                decimal eftVersion = 0;
                if (!sourceVersionMaps.TryGetValue(ins.SourceEntityKey.Value, out eftVersion))
                    eftVersion = source.VersionSequence.GetValueOrDefault();
                ins.EffectiveVersionSequenceId  = eftVersion;

                persistenceService.Insert(context, ins, principal);
            }
        }
    }
}
