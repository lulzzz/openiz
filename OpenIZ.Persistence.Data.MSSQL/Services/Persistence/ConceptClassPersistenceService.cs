﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using MARC.HI.EHRS.SVC.Core.Data;
using OpenIZ.Core.Model.DataTypes;
using OpenIZ.Persistence.Data.MSSQL.Data;

namespace OpenIZ.Persistence.Data.MSSQL.Services.Persistence
{
    /// <summary>
    /// A persistence service which handles concept classes
    /// </summary>
    public class ConceptClassPersistenceService : BaseDataPersistenceService<Core.Model.DataTypes.ConceptClass>
    {
        /// <summary>
        /// Convert from model
        /// </summary>
        internal override object ConvertFromModel(Core.Model.DataTypes.ConceptClass model)
        {
            return s_mapper.MapModelInstance<Core.Model.DataTypes.ConceptClass, Data.ConceptClass>(model);
        }

        /// <summary>
        /// Convert to model
        /// </summary>
        internal override Core.Model.DataTypes.ConceptClass ConvertToModel(object data)
        {
            return s_mapper.MapDomainInstance<Data.ConceptClass, Core.Model.DataTypes.ConceptClass>(data as Data.ConceptClass);
        }

        /// <summary>
        /// Get the specified concept class
        /// </summary>
        internal override Core.Model.DataTypes.ConceptClass Get(Identifier<Guid> containerId, IPrincipal principal, bool loadFast, ModelDataContext dataContext)
        {
            var domainConceptClass = dataContext.ConceptClasses.FirstOrDefault(c => c.ConceptClassId == containerId.Id);
            if (domainConceptClass == null)
                return null;
            else
                return this.ConvertToModel(domainConceptClass);
        }

        /// <summary>
        /// Insert a concept class
        /// </summary>
        internal override Core.Model.DataTypes.ConceptClass Insert(Core.Model.DataTypes.ConceptClass storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            var domainConceptClass = this.ConvertFromModel(storageData) as Data.ConceptClass;
            dataContext.ConceptClasses.InsertOnSubmit(domainConceptClass);
            dataContext.SubmitChanges();

            // Copy properties 
            storageData.Key = domainConceptClass.ConceptClassId;
            return storageData;
        }

        /// <summary>
        /// Obsolete the concept class
        /// </summary>
        internal override Core.Model.DataTypes.ConceptClass Obsolete(Core.Model.DataTypes.ConceptClass storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            var domainConceptClass = dataContext.ConceptClasses.FirstOrDefault(o => o.ConceptClassId == storageData.Key);
            if (domainConceptClass == null)
                throw new KeyNotFoundException();
            dataContext.ConceptClasses.DeleteOnSubmit(domainConceptClass);
            dataContext.SubmitChanges();

            storageData.Key = Guid.Empty;
            return storageData;
        }

        /// <summary>
        /// Query for the concept classes
        /// </summary>
        internal override IQueryable<Core.Model.DataTypes.ConceptClass> Query(Expression<Func<Core.Model.DataTypes.ConceptClass, bool>> query, IPrincipal principal, ModelDataContext dataContext)
        {
            var domainQuery = s_mapper.MapModelExpression<Core.Model.DataTypes.ConceptClass, Data.ConceptClass>(query);
            return dataContext.ConceptClasses.Where(domainQuery).Select(o => this.ConvertToModel(o));
        }

        /// <summary>
        /// Update the concept class
        /// </summary>
        internal override Core.Model.DataTypes.ConceptClass Update(Core.Model.DataTypes.ConceptClass storageData, IPrincipal principal, ModelDataContext dataContext)
        {
            var domainConceptClass = dataContext.ConceptClasses.FirstOrDefault(o => o.ConceptClassId == storageData.Key);
            if (domainConceptClass == null)
                throw new KeyNotFoundException();
            domainConceptClass.CopyObjectData(this.ConvertFromModel(storageData) as Data.ConceptClass);
            dataContext.SubmitChanges();
            return storageData;
        }
    }
}
