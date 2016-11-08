﻿using Jint.Native;
using Jint.Runtime;
using Newtonsoft.Json;
using OpenIZ.BusinessRules.JavaScript.JNI;
using OpenIZ.Core;
using OpenIZ.Core.Diagnostics;
using OpenIZ.Core.Model.Acts;
using OpenIZ.Core.Services;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenIZ.BusinessRules.JavaScript
{
    /// <summary>
    /// Represents the JavaScript business rules engine
    /// </summary>
    public class JavascriptBusinessRulesEngine
    {

        // Tracer for JSBRE
        private Tracer m_tracer = Tracer.GetTracer(typeof(JavascriptBusinessRulesEngine));

        // Javascript BRE instance
        private static JavascriptBusinessRulesEngine s_instance;

        // Sync lock
        private static Object s_syncLock = new object();

        // Javascript engine
        private Jint.Engine m_engine = null;

        // Bridge
        private JNI.BusinessRulesBridge m_bridge = new JNI.BusinessRulesBridge();

        // Trigger definitions
        private Dictionary<String, Dictionary<String, List<Func<Object, Object>>>> m_triggerDefinitions = new Dictionary<string, Dictionary<string, List<Func<object, object>>>>();

        // Validators
        private Dictionary<String, List<Func<Object, Object[]>>> m_validatorDefinitions = new Dictionary<string, List<Func<object, Object[]>>>();

        /// <summary>
        /// Only one BRE can be created
        /// </summary>
        private JavascriptBusinessRulesEngine()
        {

        }

        /// <summary>
        /// Initialize javascript BRE
        /// </summary>
        private void Initialize()
        {
            // Set up javascript ening 
            this.m_tracer.TraceInfo("OpenIZ Javascript Business Rules Host Initialize");

            this.m_engine = new Jint.Engine(cfg => cfg.AllowClr(
                    typeof(OpenIZ.Core.Model.BaseEntityData).GetTypeInfo().Assembly,
                    typeof(IBusinessRulesService<>).GetTypeInfo().Assembly
                ).Strict(true)
#if DEBUG
                .DebugMode(true)
#endif

                ).SetValue("OpenIZBre", this.m_bridge);
        }

        /// <summary>
        /// Current BRE
        /// </summary>
        public static JavascriptBusinessRulesEngine Current
        {
            get
            {
                if (s_instance == null)
                    lock (s_syncLock)
                    {
                        s_instance = new JavascriptBusinessRulesEngine();
                        s_instance.Initialize();
                    }
                return s_instance;
            }
        }

        /// <summary>
        /// Add the specified script
        /// </summary>
        public void AddRules(StreamReader script)
        {
            try
            {
                this.m_tracer.TraceVerbose("Adding rules to BRE");
                var executionLog = this.m_engine.Execute(script.ReadToEnd());
            }
            catch(JavaScriptException ex)
            {
                this.m_tracer.TraceError("Error executing JavaScript {0}:{1} > {2}", ex.LineNumber, ex.Column, ex);
                throw ex;
            }
        }

        /// <summary>
        /// Register a validator which is responsible for validation
        /// </summary>
        public void RegisterValidator(string target, Func<object, Object[]> _delegate)
        {
            List<Func<object, Object[]>> validatorFunc = null;
            if (!this.m_validatorDefinitions.TryGetValue(target, out validatorFunc))
            {
                this.m_tracer.TraceInfo("Will try to create BRE service for {0}", target);
                // We need to create a rule service base and register it!!! :)
                // Find the target type
                var targetType = typeof(Act).GetTypeInfo().Assembly.ExportedTypes.FirstOrDefault(o => o.GetTypeInfo().GetCustomAttribute<JsonObjectAttribute>()?.Id == target);
                if (targetType == null)
                    throw new KeyNotFoundException(target);
                var ruleService = typeof(RuleServiceBase<>).MakeGenericType(targetType);
                var serviceManager = ApplicationServiceContext.Current.GetService(typeof(IServiceManager)) as IServiceManager;
                serviceManager.AddServiceProvider(ruleService);

                // Now add
                lock (s_syncLock)
                    this.m_validatorDefinitions.Add(target, new List<Func<object, Object[]>>() { _delegate });
            }
            else
                validatorFunc.Add(_delegate);

        }

        /// <summary>
        /// Register a rule
        /// </summary>
        public void RegisterRule(string target, string trigger, Func<object, object> _delegate)
        {
            Dictionary<String, List<Func<object, object>>> triggerHandler = null;
            if (!this.m_triggerDefinitions.TryGetValue(target, out triggerHandler))
            {
                this.m_tracer.TraceInfo("Will try to create BRE service for {0}", target);
                // We need to create a rule service base and register it!!! :)
                // Find the target type
                var targetType = typeof(Act).GetTypeInfo().Assembly.ExportedTypes.FirstOrDefault(o => o.GetTypeInfo().GetCustomAttribute<JsonObjectAttribute>()?.Id == target);
                if (targetType == null)
                    throw new KeyNotFoundException(target);
                var ruleService = typeof(RuleServiceBase<>).MakeGenericType(targetType);
                var serviceManager = ApplicationServiceContext.Current.GetService(typeof(IServiceManager)) as IServiceManager;
                serviceManager.AddServiceProvider(ruleService);

                // Now add
                lock (s_syncLock)
                    this.m_triggerDefinitions.Add(target, new Dictionary<string, List<Func<object, object>>>()
                    {
                        { trigger, new List<Func<object, object>>() { _delegate } }
                    });
            }
            else
            {
                List<Func<Object, Object>> delegates = null;
                if (!triggerHandler.TryGetValue(trigger, out delegates))
                    lock (s_syncLock)
                        triggerHandler.Add(trigger, new List<Func<object, object>>() { _delegate });
                else
                    delegates.Add(_delegate);
            }
        }

        /// <summary>
        /// Get call list of action
        /// </summary>
        public List<Func<Object, Object>> GetCallList<TBinding>(String action)
        {
            var className = typeof(TBinding).GetTypeInfo().GetCustomAttribute<JsonObjectAttribute>()?.Id;

            // Try to get the binding
            Dictionary<String, List<Func<object, object>>> triggerHandler = null;
            if (this.m_triggerDefinitions.TryGetValue(className, out triggerHandler))
            {
                List<Func<Object, Object>> callList = null;
                if (triggerHandler.TryGetValue(action, out callList))
                    return callList;
            }
            return null;

        }

        /// <summary>
        /// Get all validator functions
        /// </summary>
        public List<Func<Object, Object[]>> GetValidators<TBinding>()
        {
            var className = typeof(TBinding).GetTypeInfo().GetCustomAttribute<JsonObjectAttribute>()?.Id;
            
            // Try to get the binding
            List<Func<object, Object[]>> callList = null;
            if (this.m_validatorDefinitions.TryGetValue(className, out callList))
                return callList;
            return null;
        }

        /// <summary>
        /// Perform actual invokation on all objects
        /// </summary>
        public TBinding Invoke<TBinding>(string action, TBinding data)
        {
            var callList = this.GetCallList<TBinding>(action);
            var retVal = data;
            foreach (var c in callList)
                retVal = (TBinding)c(data);
            return retVal;

        }

        /// <summary>
        /// Validate the data object if validation is available
        /// </summary>
        public List<DetectedIssue> Validate<TBinding>(TBinding data)
        {
            var callList = this.GetValidators<TBinding>();
            var retVal = new List<DetectedIssue>();
            foreach (var c in callList)
            {
                var issues = c(data);
                retVal.AddRange(issues.Cast<IDictionary<String, Object>>().Select(o=>new DetectedIssue()
                {
                    Text = o.ContainsKey("text") ? o["text"]?.ToString() : null,
                    Priority = o.ContainsKey("priority") ? (DetectedIssuePriorityType)(int)(double)o["priority"] : DetectedIssuePriorityType.Informational
                }));
            }
            return retVal;
        }
    }
}