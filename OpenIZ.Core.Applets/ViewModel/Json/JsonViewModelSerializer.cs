﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using OpenIZ.Core.Applets.ViewModel.Description;
using OpenIZ.Core.Model;
using OpenIZ.Core.Model.Interfaces;
using OpenIZ.Core.Model.EntityLoader;
using OpenIZ.Core.Diagnostics;
using Newtonsoft.Json;
using System.Collections;
using OpenIZ.Core.Model.Reflection;
using OpenIZ.Core.Model.Attributes;

namespace OpenIZ.Core.Applets.ViewModel.Json
{
    /// <summary>
    /// Represents a JSON view model serializer
    /// </summary>
    public class JsonViewModelSerializer : IViewModelSerializer
    {

        // Formatters
        private Dictionary<Type, IJsonViewModelTypeFormatter> m_formatters = new Dictionary<Type, IJsonViewModelTypeFormatter>();

        // Classifiers
        private Dictionary<Type, IViewModelClassifier> m_classifiers = new Dictionary<Type, IViewModelClassifier>();

        // Sync lock
        private Object m_syncLock = new object();

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(JsonViewModelSerializer));

        // Related load methods
        private Dictionary<Type, MethodInfo> m_relatedLoadMethods = new Dictionary<Type, MethodInfo>();

        // Reloated load association
        private Dictionary<Type, MethodInfo> m_relatedLoadAssociations = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// Creates a json view model serializer
        /// </summary>
        public JsonViewModelSerializer()
        {
            this.ViewModel = new ViewModelDescription()
            {
                Model = new List<TypeModelDescription>()
                {
                    new TypeModelDescription()
                    {
                        TypeName = "IdentifiedData",
                        All = true
                    }
                }
            };
        }

        /// <summary>
        /// Gets or sets the view model definition for this view model serializer
        /// </summary>
        public ViewModelDescription ViewModel
        {
            get; set;
        }

        /// <summary>
        /// De-serializes the specified object from the stream
        /// </summary>
        public TModel DeSerialize<TModel>(Stream s)
        {
            return (TModel)this.DeSerialize(s, typeof(TModel));
        }

        /// <summary>
        /// De-serialize data from the string
        /// </summary>
        public TModel DeSerialize<TModel>(String s)
        {
            using (var sr = new StringReader(s))
                return (TModel)this.DeSerialize(sr, typeof(TModel));
        }

        /// <summary>
        /// De-serializes the data from the stream as the specified type
        /// </summary>
        public Object DeSerialize(Stream s, Type t)
        {
            using (StreamReader sr = new StreamReader(s))
                return this.DeSerialize(sr, t);
        }

        /// <summary>
        /// De-serializes data from the reader as specified type
        /// </summary>
        public Object DeSerialize(TextReader r, Type t)
        {
            try
            {
                using (JsonReader jr = new JsonTextReader(r))
                {
                    // Seek to the start object token
                    while (jr.TokenType != JsonToken.StartObject && jr.Read()) ;

                    return this.ReadElementUtil(jr, t, new JsonSerializationContext(null, this, null));
                }

            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error de-serializing: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Read the specified element
        /// </summary>
        public object ReadElementUtil(JsonReader r, Type t, JsonSerializationContext context)
        {
            var nonGenericT = t.StripGeneric();
            var classifier = this.GetClassifier(nonGenericT);
            switch (r.TokenType)
            {
                case JsonToken.StartObject:
                    {
                        var formatter = this.GetFormatter(nonGenericT);
                        // Classifier???
                        if (classifier == null || !typeof(IList).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                            return formatter.Deserialize(r, t, context);
                        else
                        {
                            // Classifier each of these properties aren't real properties, rather they are classified things
                            int depth = r.Depth;
                            Dictionary<String, Object> values = new Dictionary<string, object>();
                            while (r.Read() && !(r.TokenType == JsonToken.EndObject && r.Depth == depth))
                            {
                                // Classifier
                                if (r.TokenType != JsonToken.PropertyName) throw new JsonException($"Expected PropertyName token got {r.TokenType}");
                                string propertyName = (String)r.Value;
                                r.Read(); // Read proeprty name
                                values.Add(propertyName, this.ReadElementUtil(r, r.TokenType == JsonToken.StartObject ? nonGenericT : t, new JsonSerializationContext(propertyName, this, values, context)));
                            }
                            return classifier.Compose(values, t);
                        }
                    }
                // Array read, we want to re-call the specified parse
                case JsonToken.StartArray:
                    {
                        if (!typeof(IList).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                            throw new JsonSerializationException($"{t} does not implement IList");
                        int depth = r.Depth;
                        var listInstance = Activator.CreateInstance(t) as IList;
                        while (r.Read() && !(r.TokenType == JsonToken.EndArray && r.Depth == depth))
                            listInstance.Add(this.ReadElementUtil(r, nonGenericT, context));
                        return listInstance;

                    }
                case JsonToken.Null:
                case JsonToken.Boolean:
                case JsonToken.Bytes:
                case JsonToken.Float:
                case JsonToken.Date:
                case JsonToken.Integer:
                case JsonToken.String:
                    if (typeof(IdentifiedData).GetTypeInfo().IsAssignableFrom(nonGenericT.GetTypeInfo())) // Complex object
                    {
                        var formatter = this.GetFormatter(nonGenericT);
                        return formatter.FromSimpleValue(r.Value);
                    }
                    else
                    {
                        // Not a simple value attribute so let's just handle it as normal
                        switch (r.TokenType)
                        {
                            case JsonToken.Null:
                                return null;
                            case JsonToken.Boolean:
                            case JsonToken.Bytes:
                                return r.Value;
                            case JsonToken.Float:
                                if (t.StripNullable() == typeof(Decimal))
                                    return Convert.ToDecimal(r.Value);
                                else
                                    return (Double)r.Value;
                            case JsonToken.Date:
                                t = t.StripNullable();
                                if (t == typeof(DateTime))
                                    return (DateTime)r.Value;
                                else if (t == typeof(String))
                                    return ((DateTime)r.Value).ToString("o");
                                else
                                    return new DateTimeOffset((DateTime)r.Value);
                            case JsonToken.Integer:
                                t = t.StripNullable();
                                if (t.GetTypeInfo().IsEnum)
                                    return Enum.ToObject(t, r.Value);
                                return Convert.ChangeType(r.Value, t);
                            case JsonToken.String:
                                if (String.IsNullOrEmpty((string)r.Value))
                                    return null;
                                else if (t.StripNullable() == typeof(Guid))
                                    return Guid.Parse((string)r.Value);
                                else
                                    return r.Value;
                            default:
                                return r.Value;
                        }
                    }
                default:
                    throw new JsonSerializationException("Invalid serialization");
            }
        }

        /// <summary>
        /// Load related object
        /// </summary>
        internal object LoadRelated(Type propertyType, Guid key)
        {
            MethodInfo methodInfo = null;
            if (!this.m_relatedLoadMethods.TryGetValue(propertyType, out methodInfo))
            {
                methodInfo = this.GetType().GetRuntimeMethod(nameof(LoadRelated), new Type[] { typeof(Guid) }).MakeGenericMethod(propertyType);
                lock (this.m_syncLock)
                    if (!this.m_relatedLoadMethods.ContainsKey(propertyType))
                        this.m_relatedLoadMethods.Add(propertyType, methodInfo);

            }
            return methodInfo.Invoke(this, new object[] { key });
        }

        /// <summary>
        /// Load collection
        /// </summary>
        internal IList LoadCollection(Type propertyType, Guid key)
        {
            MethodInfo methodInfo = null;
            if (!this.m_relatedLoadAssociations.TryGetValue(propertyType, out methodInfo))
            {
                methodInfo = this.GetType().GetRuntimeMethod(nameof(LoadCollection), new Type[] { typeof(Guid) }).MakeGenericMethod(propertyType.StripGeneric());
                lock (this.m_syncLock)
                    if (!this.m_relatedLoadAssociations.ContainsKey(propertyType))
                        this.m_relatedLoadAssociations.Add(propertyType, methodInfo);

            }
            return methodInfo.Invoke(this, new object[] { key }) as IList;
        }

        /// <summary>
        /// Delay loads the specified collection association
        /// </summary>
        public IEnumerable<TAssociation> LoadCollection<TAssociation>(Guid sourceKey) where TAssociation : IdentifiedData, ISimpleAssociation, new()
        {
            return EntitySource.Current.Provider.GetRelations<TAssociation>(sourceKey);
        }

        /// <summary>
        /// Load the related information
        /// </summary>
        public TRelated LoadRelated<TRelated>(Guid objectKey) where TRelated : IdentifiedData, new()
        {
            return EntitySource.Current.Provider.Get<TRelated>(objectKey);
        }

        /// <summary>
        /// Write property utility
        /// </summary>
        public void WritePropertyUtil(JsonWriter w, String propertyName, Object instance, SerializationContext context)
        {

            // first write the property
            if (!String.IsNullOrEmpty(propertyName))  // In an array so don't emit the property name
            {
                w.WritePropertyName(propertyName);
                // Are we to never serialize this?
                if (context?.ShouldSerialize(propertyName) == false)
                    return;
            }

            // Emit type
            if (instance == null)
                w.WriteNull();
            else if (instance is IdentifiedData)
            {
                var identifiedData = instance as IdentifiedData;

                // Complex type .. allow the formatter to handle this
                IJsonViewModelTypeFormatter typeFormatter = this.GetFormatter(instance.GetType());

                var simpleValue = typeFormatter.GetSimpleValue(instance);
                if (simpleValue != null && propertyName != "$other") // Special case for $other
                    w.WriteValue(simpleValue);
                else
                {
                    w.WriteStartObject();

                    var subContext = new JsonSerializationContext(propertyName, this, instance, context as JsonSerializationContext);
                    this.WriteSimpleProperty(w, "$type", identifiedData.Type);
                    this.WriteSimpleProperty(w, "$id", String.Format("obj{0}", subContext.ObjectId));

                    // Write ref
                    var parentObjectId = context?.GetParentObjectId(identifiedData);
                    if (parentObjectId.HasValue) // Recursive
                        this.WriteSimpleProperty(w, "$ref", String.Format("#obj{0}", parentObjectId.Value));
                    else
                        typeFormatter.Serialize(w, instance as IdentifiedData, subContext);

                    w.WriteEndObject();
                }
            }
            else if (instance is IList)
            {
                // Classifications?
                var classifier = this.GetClassifier(instance.GetType().StripNullable());

                if (classifier == null) // no classifier
                {
                    w.WriteStartArray();
                    foreach (var itm in instance as IList)
                        this.WritePropertyUtil(w, null, itm, new JsonSerializationContext(propertyName, this, instance, context as JsonSerializationContext));
                    w.WriteEndArray();
                }
                else
                {
                    w.WriteStartObject();
                    foreach (var cls in classifier.Classify(instance as IList))
                    {
                        Object value = new List<Object>(cls.Value as IEnumerable<Object>);
                        if (cls.Value.Count == 1)
                            value = cls.Value[0];
                        // Now write
                        this.WritePropertyUtil(w, cls.Key, value, new JsonSerializationContext(propertyName, this, instance, context as JsonSerializationContext));
                    }
                    w.WriteEndObject();
                }
            }
            else
                w.WriteValue(instance);

        }

        /// <summary>
        /// Gets the appropriate classifier for the specified type
        /// </summary>
        private IViewModelClassifier GetClassifier(Type type)
        {
            IViewModelClassifier retVal = null;
            if (!this.m_classifiers.TryGetValue(type, out retVal))
            {
                var classifierAtt = type.StripGeneric().GetTypeInfo().GetCustomAttribute<ClassifierAttribute>();
                if (classifierAtt != null)
                    retVal = new JsonReflectionClassifier(type);
                lock (this.m_syncLock)
                    if(!this.m_classifiers.ContainsKey(type))
                        this.m_classifiers.Add(type, retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Write a simple property
        /// </summary>
        /// <param name="propertyName">The name of the JSON property</param>
        /// <param name="value">The value of the JSON property</param>
        /// <param name="w">The value of the property</param>
        private void WriteSimpleProperty(JsonWriter w, String propertyName, Object value)
        {
            w.WritePropertyName(propertyName);
            w.WriteValue(value);
        }

        /// <summary>
        /// Get the specified formatter
        /// </summary>
        /// <param name="type">The type to retrieve the formatter for</param>
        private IJsonViewModelTypeFormatter GetFormatter(Type type)
        {
            IJsonViewModelTypeFormatter typeFormatter = null;
            if (!this.m_formatters.TryGetValue(type, out typeFormatter))
            {
                typeFormatter = new JsonReflectionTypeFormatter(type);
                lock (this.m_syncLock)
                    this.m_formatters.Add(type, typeFormatter);
            }
            return typeFormatter;
        }

        /// <summary>
        /// Load the specified serializer assembly
        /// </summary>
        public void LoadSerializerAssembly(Assembly asm)
        {
            var typeFormatters = asm.ExportedTypes.Where(o => typeof(IJsonViewModelTypeFormatter).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo()) && o.GetTypeInfo().IsClass)
                .Select(o => Activator.CreateInstance(o) as IJsonViewModelTypeFormatter)
                .Where(o => !this.m_formatters.ContainsKey(o.HandlesType));
            var classifiers = asm.ExportedTypes.Where(o => typeof(IViewModelClassifier).GetTypeInfo().IsAssignableFrom(o.GetTypeInfo()) && o.GetTypeInfo().IsClass)
                .Select(o => Activator.CreateInstance(o) as IViewModelClassifier)
                .Where(o => !this.m_classifiers.ContainsKey(o.HandlesType));
            foreach (var fmtr in typeFormatters)
                this.m_formatters.Add(fmtr.HandlesType, fmtr);
            foreach (var cls in classifiers)
                this.m_classifiers.Add(cls.HandlesType, cls);
        }

        /// <summary>
        /// Serialize object to string
        /// </summary>
        public String Serialize(IdentifiedData data)
        {
            using (StringWriter sw = new StringWriter())
            {
                this.Serialize(sw, data);
                return sw.ToString();
            }
        }

        /// <summary>
        /// Serialize the specified data
        /// </summary>
        public void Serialize(Stream s, IdentifiedData data)
        {

            using (StreamWriter tw = new StreamWriter(s))
                this.Serialize(tw, data);
        }


        /// <summary>
        /// Serialize to the specified text writer
        /// </summary>
        public void Serialize(TextWriter tw, IdentifiedData data)
        {
            try
            {
                using (JsonWriter jw = new JsonTextWriter(tw))
                {
                    this.WritePropertyUtil(jw, null, data, null);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error serializing {0} : {1}", data, e);
                throw;
            }
        }
    }
}