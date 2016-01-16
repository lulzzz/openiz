﻿using OpenIZ.Core.Model.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MARC.HI.EHRS.SVC.Core.Data;
using OpenIZ.Persistence.Data.MSSQL.Data;
using System.Linq.Expressions;
using System.Security.Principal;
using OpenIZ.Persistence.Data.MSSQL.Exceptions;
using OpenIZ.Core;

namespace OpenIZ.Persistence.Data.MSSQL.Services.Persistence
{
    /// <summary>
    /// Concept persistence service
    /// </summary>
    public class ConceptPersistenceService : BaseDataPersistenceService<Core.Model.DataTypes.Concept>
    {
        /// <summary>
        /// Convert from model concept into a domain model concept
        /// </summary>
        internal override object ConvertFromModel(Core.Model.DataTypes.Concept model)
        {
            return s_mapper.MapModelInstance<Core.Model.DataTypes.Concept, Data.ConceptVersion>(model);
        }

        /// <summary>
        /// Convert a data context into a model object
        /// </summary>
        internal override Core.Model.DataTypes.Concept ConvertToModel(object data)
        {
            return s_mapper.MapDomainInstance<Data.ConceptVersion, Core.Model.DataTypes.Concept>(data as Data.ConceptVersion);
        }

        /// <summary>
        /// Get the specified concept with version
        /// </summary>
        internal override Core.Model.DataTypes.Concept Get(Identifier<Guid> containerId, IPrincipal principal, bool loadFast, ModelDataContext dataContext)
        {
            if (containerId == null)
                throw new ArgumentNullException(nameof(containerId));

            // Return values
            Data.ConceptVersion retVal = null;
            if (containerId.VersionId != default(Guid))
                retVal = dataContext.ConceptVersions.FirstOrDefault(o => o.ConceptVersionId == containerId.VersionId);
            else if (containerId.Id != default(Guid))
                retVal = dataContext.ConceptVersions.FirstOrDefault(o => o.ConceptId == containerId.Id && o.ObsoletionTime == null);

            if (retVal == null)
                return null;
            else
                return this.ConvertToModel(retVal);
        }

        /// <summary>
        /// Insert the concept and dependent properties (as list)
        /// </summary>
        internal override Core.Model.DataTypes.Concept Insert(Core.Model.DataTypes.Concept storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData.Key != Guid.Empty)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.IdentityInsert);
            else if (storageData == null)
                throw new ArgumentNullException(nameof(storageData));
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            // Store the data
            var dataConceptVersion = this.ConvertFromModel(storageData) as Data.ConceptVersion;
            dataConceptVersion.Concept = new Data.Concept() { IsSystemConcept = storageData.IsSystemConcept };
            dataConceptVersion.CreatedBy = principal.GetUserGuid(dataContext);

            dataConceptVersion.StatusConceptId = dataConceptVersion.StatusConceptId == Guid.Empty ? ConceptIds.StatusActive : dataConceptVersion.StatusConceptId;
            if(storageData.Class != null)
                dataConceptVersion.ConceptClassId = storageData.Class.EnsureExists(principal, dataContext)?.Key;

            // Store the root concept
            dataContext.ConceptVersions.InsertOnSubmit(dataConceptVersion);
            dataContext.SubmitChanges();

            // Concept names
            if (storageData.ConceptNames != null)
            {
                ConceptNamePersistenceService namePersister = new ConceptNamePersistenceService();
                foreach (var cn in storageData.ConceptNames)
                {
                    cn.TargetEntityKey = dataConceptVersion.ConceptId;
                    namePersister.Insert(cn, principal, dataContext, false); // How I wish I was in sherbrooke now!!!
                }
            }

            //// Reference terms
            //if (storageData.ReferenceTerms != null)
            //{
            //    ConceptReferenceTermPersistenceService referencePersister = new ConceptReferenceTermPersistenceService();
            //    foreach (var rt in storageData.ReferenceTerms)
            //    {
            //        rt.EffectiveVersionSequenceId = storageData.VersionSequence;
            //        rt.TargetEntityKey = storageData.Key; // Oh Elcid Barrett cried the town
            //        referencePersister.Insert(rt, principal, dataContext); // How I wish I was in sherbrooke now!!!
            //    }
            //}

            //// Storage data 
            //if (storageData.Relationship != null)
            //{
            //    ConceptRelationshipPersistenceService relationshipPersister = new ConceptRelationshipPersistenceService();
            //    foreach (var rel in storageData.Relationship)
            //    {
            //        rel.EffectiveVersionSequenceId = storageData.VersionSequence;
            //        rel.TargetEntityKey = storageData.Key; // The Antelope's sloop wa a sickening sight
            //        relationshipPersister.Insert(rel, principal, dataContext); // How I wish I was in sherbrooke now!!!
            //    }
            //}

            return this.ConvertToModel(dataConceptVersion);
        }

        /// <summary>
        /// Obsolete the concept 
        /// </summary>
        internal override Core.Model.DataTypes.Concept Obsolete(Core.Model.DataTypes.Concept storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (storageData == null)
                throw new ArgumentNullException(nameof(storageData));
            else if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            else if (storageData.Key == default(Guid))
                throw new SqlFormalConstraintException(SqlFormalConstraintType.NonIdentityUpdate);
            else if (storageData.IsSystemConcept)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.UpdatedReadonlyObject);

            var dataConceptVersion = dataContext.ConceptVersions.FirstOrDefault(c => c.ConceptVersionId == storageData.VersionKey);
            if (dataConceptVersion == null)
                throw new KeyNotFoundException();
            else if (dataConceptVersion.Concept.IsSystemConcept)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.UpdatedReadonlyObject);

            // Update old version as obsolete, insert a new version with obsolete status
            var newDataConceptVersion = this.ConvertFromModel(storageData) as ConceptVersion;
            dataConceptVersion.ObsoletionTime = DateTimeOffset.Now;
            newDataConceptVersion.CreatedBy = principal.GetUserGuid(dataContext);
            dataConceptVersion.ObsoletedBy = principal.GetUserGuid(dataContext);
            newDataConceptVersion.StatusConceptId = Core.ConceptIds.StatusObsolete;
            dataContext.ConceptVersions.InsertOnSubmit(newDataConceptVersion);

            dataContext.SubmitChanges();
            return this.ConvertToModel(newDataConceptVersion);

        }

        /// <summary>
        /// Query the concept versions
        /// </summary>
        internal override IQueryable<Core.Model.DataTypes.Concept> Query(Expression<Func<Core.Model.DataTypes.Concept, bool>> query, IPrincipal principal, ModelDataContext dataContext)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var domainQuery = s_mapper.MapModelExpression<Core.Model.DataTypes.Concept, Data.ConceptVersion>(query);
            return dataContext.ConceptVersions.Where(domainQuery).Select(o=>this.ConvertToModel(o));

        }

        /// <summary>
        /// Update the container
        /// </summary>
        internal override Core.Model.DataTypes.Concept Update(Core.Model.DataTypes.Concept storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            else if (storageData.IsSystemConcept)
                throw new SqlFormalConstraintException(SqlFormalConstraintType.UpdatedReadonlyObject);


            // Get the existing version 
            var domainConceptVersion = dataContext.ConceptVersions.FirstOrDefault(o => o.ConceptId == storageData.Key && o.ObsoletionTime == null);
            Decimal oldVersionSequenceId = domainConceptVersion.VersionSequenceId;

            if (domainConceptVersion == null)
                throw new KeyNotFoundException();

            // Create the new version
            domainConceptVersion = domainConceptVersion.NewVersion(principal, dataContext);
            storageData.Key = storageData.VersionKey = storageData.CreatedById = Guid.Empty; // Zero off associations
            storageData.VersionSequence = default(decimal);
            domainConceptVersion.CopyObjectData(this.ConvertFromModel(storageData));
            domainConceptVersion.Concept.IsSystemConcept = storageData.IsSystemConcept;

            dataContext.SubmitChanges(); // Submit changes to db

            // Update the dependent objects
            ConceptNamePersistenceService cnPersistenceService = new ConceptNamePersistenceService();

            // First thing, we want to remove any names that no longer appear in the storageData and/or update those 
            if (storageData.ConceptNames != null)
            {
                var existingNames = domainConceptVersion.Concept.ConceptNames.Where(o => oldVersionSequenceId >= o.EffectiveVersionSequenceId && o.ObsoleteVersionSequenceId == null).Select(o => cnPersistenceService.ConvertToModel(o)).ToList(); // active names

                // Remove old
                var obsoleteRecords = existingNames.Where(o => !storageData.ConceptNames.Exists(ecn => ecn.Key == o.Key));
                foreach (var del in obsoleteRecords)
                    cnPersistenceService.Obsolete(del, principal, dataContext, false);

                // Update those that need it
                var updateRecords = storageData.ConceptNames.Where(o => existingNames.Exists(ecn => ecn.Key == o.Key && o != ecn));
                foreach(var upd in updateRecords)
                    cnPersistenceService.Update(upd, principal, dataContext, false);

                // Insert those that do not exist
                var insertRecords = storageData.ConceptNames.Where(o => !existingNames.Exists(ecn => ecn.Key == o.Key));
                foreach (var ins in insertRecords)
                {
                    ins.TargetEntityKey = domainConceptVersion.ConceptId;
                    cnPersistenceService.Insert(ins, principal, dataContext, false);
                }

            }

            return this.ConvertToModel(domainConceptVersion);
        }
    }
}
