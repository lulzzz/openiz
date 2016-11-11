﻿/*
 * Copyright 2015-2016 Mohawk College of Applied Arts and Technology
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
 * Date: 2016-6-22
 */

using MARC.HI.EHRS.SVC.Core;
using MARC.HI.EHRS.SVC.Core.Services;
using MARC.HI.EHRS.SVC.Core.Services.Security;
using MARC.Util.CertificateTools;
using OpenIZ.Core.Model.AMI.Auth;
using OpenIZ.Core.Model.AMI.Security;
using OpenIZ.Core.Model.DataTypes;
using OpenIZ.Core.Model.Entities;
using OpenIZ.Core.Model.Query;
using OpenIZ.Core.Model.Security;
using OpenIZ.Core.Security;
using OpenIZ.Core.Services;
using OpenIZ.Messaging.AMI.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.Pkcs;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Schema;
using System.Xml.Serialization;
using OpenIZ.Core.Model.AMI.Alerting;
using OpenIZ.Core.Alert.Alerting;
using OpenIZ.Core.Model.AMI.DataTypes;
using OpenIZ.Core.Model.Constants;
using OpenIZ.Core.Model.AMI.Diagnostics;
using OpenIZ.Core.Security.Attribute;
using System.Security.Permissions;
using OpenIZ.Core.Model.AMI.Applet;
using OpenIZ.Core.Security.Claims;

namespace OpenIZ.Messaging.AMI.Wcf
{
	/// <summary>
	/// Represents the administrative contract interface.
	/// </summary>
	[ServiceBehavior(ConfigurationName = "AMI")]
	public class AmiBehavior : IAmiContract
	{
		// Certificate tool
		private CertTool m_certTool;

		// Configuration
		private AmiConfiguration m_configuration = ApplicationContext.Current.GetService<IConfigurationManager>().GetSection("openiz.messaging.ami") as AmiConfiguration;

		// Trace source
		private TraceSource m_traceSource = new TraceSource("OpenIZ.Messaging.AMI");

		/// <summary>
		/// Creates the AMI behavior
		/// </summary>
		public AmiBehavior()
		{
			this.m_certTool = new CertTool();
			this.m_certTool.CertificationAuthorityName = this.m_configuration?.CaConfiguration.Name;
			this.m_certTool.ServerName = this.m_configuration?.CaConfiguration.ServerName;
		}

        /// <summary>
        /// Accepts a certificate signing request.
        /// </summary>
        /// <param name="id">The id of the certificate signing request to be accepted.</param>
        /// <returns>Returns the acceptance result.</returns>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public SubmissionResult AcceptCsr(string rawId)
		{
			int id = Int32.Parse(rawId);
			this.m_certTool.Approve(id);
			var submission = this.m_certTool.GetRequestStatus(id);

			var result = new SubmissionResult(submission.Message, submission.RequestId, (SubmissionStatus)submission.Outcome, submission.AuthorityResponse);
			result.Certificate = null;
			return result;
		}

		/// <summary>
		/// Changes the password of a user.
		/// </summary>
		/// <param name="id">The id of the user whose password is to be changed.</param>
		/// <param name="password">The new password of the user.</param>
		/// <returns>Returns the updated user.</returns>
		public SecurityUser ChangePassword(string id, string password)
		{
			Guid userKey = Guid.Empty;

			if (!Guid.TryParse(id, out userKey))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(id)));
			}

			var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (securityRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			return securityRepository.ChangePassword(userKey, password);
		}

		/// <summary>
		/// Creates an alert.
		/// </summary>
		/// <param name="alertMessageInfo">The alert message to be created.</param>
		/// <returns>Returns the created alert.</returns>
		public AlertMessageInfo CreateAlert(AlertMessageInfo alertMessageInfo)
		{
			var alertRepositoryService = ApplicationContext.Current.GetService<IAlertRepositoryService>();

			if (alertRepositoryService == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAlertRepositoryService)));
			}

			var createdAlert = alertRepositoryService.Insert(alertMessageInfo.AlertMessage);

			return new AlertMessageInfo(createdAlert);
		}

        /// <summary>
        /// Creates an assigning authority.
        /// </summary>
        /// <param name="assigningAuthorityInfo">The assigning authority to be created.</param>
        /// <returns>Returns the created assigning authority.</returns>
        public AssigningAuthorityInfo CreateAssigningAuthority(AssigningAuthorityInfo assigningAuthorityInfo)
        {
            var assigningAuthorityRepositoryService = ApplicationContext.Current.GetService<IAssigningAuthorityRepositoryService>();

            if (assigningAuthorityRepositoryService == null)
            {
                throw new InvalidOperationException(string.Format("{0} not found", nameof(IAssigningAuthorityRepositoryService)));
            }

            var createdAssigningAuthority = assigningAuthorityRepositoryService.Insert(assigningAuthorityInfo.AssigningAuthority);

            return new AssigningAuthorityInfo(createdAssigningAuthority);
        }

		/// <summary>
		/// Creates an applet.
		/// </summary>
		/// <param name="appletManifestInfo">The applet manifest info to be created.</param>
		/// <returns>Returns the created applet manifest info.</returns>
		public AppletManifestInfo CreateApplet(AppletManifestInfo appletManifestInfo)
		{
			throw new NotImplementedException();
		}

		/// <summary>
        /// Creates a device in the IMS.
        /// </summary>
        /// <param name="device">The device to be created.</param>
        /// <returns>Returns the newly created device.</returns>
        public SecurityDevice CreateDevice(SecurityDevice device)
		{
			var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (securityRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			return securityRepository.CreateDevice(device);
		}

        /// <summary>
        /// Create a diagnostic report 
        /// </summary>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.Login)]
        public DiagnosticReport CreateDiagnosticReport(DiagnosticReport report)
        {
            var persister = ApplicationContext.Current.GetService<IDataPersistenceService<DiagnosticReport>>();
            if (persister == null)
                throw new InvalidOperationException("Cannot find appriopriate persister");
            return persister.Insert(report, AuthenticationContext.Current.Principal, TransactionMode.Commit);
        }

        /// <summary>
        /// Creates a place in the IMS.
        /// </summary>
        /// <param name="place">The place to be created.</param>
        /// <returns>Returns the newly created place.</returns>
        public Place CreatePlace(Place place)
		{
			var placeRepository = ApplicationContext.Current.GetService<IPlaceRepositoryService>();

			if (placeRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} cannot be null", nameof(IPlaceRepositoryService)));
			}

			return placeRepository.Insert(place);
		}

		/// <summary>
		/// Creates a security policy.
		/// </summary>
		/// <param name="policy">The security policy to be created.</param>
		/// <returns>Returns the newly created security policy.</returns>
		public SecurityPolicyInfo CreatePolicy(SecurityPolicyInfo policy)
		{
			var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (securityRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			SecurityPolicy policyToCreate = new SecurityPolicy
			{
				CanOverride = policy.CanOverride,
				Name = policy.Name,
				Oid = policy.Oid
			};

			return new SecurityPolicyInfo(securityRepository.CreatePolicy(policyToCreate));
		}

		/// <summary>
		/// Creates a security role.
		/// </summary>
		/// <param name="role">The security role to be created.</param>
		/// <returns>Returns the newly created security role.</returns>
		public SecurityRoleInfo CreateRole(SecurityRoleInfo role)
		{
			var roleRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			var roleToCreate = new SecurityRole()
			{
				Name = role.Name
			};
			return new SecurityRoleInfo(roleRepository.CreateRole(roleToCreate));
		}

        /// <summary>
        /// Creates security reset information
        /// </summary>
        public void SendTfaSecret(TfaRequestInfo resetInfo)
        {

            var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

            var securityUser = securityRepository.GetUser(resetInfo.UserName);
            if (securityUser == null)
                throw new KeyNotFoundException();

            // Identity provider
            var identityProvider = ApplicationContext.Current.GetService<IIdentityProviderService>();
            var tfaSecret = identityProvider.GenerateTfaSecret(securityUser.UserName);

            // Add a claim
            if (resetInfo.Purpose == "PasswordReset")
            {
                new PolicyPermission(PermissionState.Unrestricted, PermissionPolicyIdentifiers.LoginAsService);
                identityProvider.AddClaim(securityUser.UserName, new System.Security.Claims.Claim(OpenIzClaimTypes.OpenIZPasswordlessAuth, "true"));
            }

            var tfaRelay = ApplicationContext.Current.GetService<ITfaRelayService>();
            if (tfaRelay == null)
                throw new InvalidOperationException("TFA relay not specified");

            // Now issue the TFA secret
            tfaRelay.SendSecret(resetInfo.ResetMechanism, securityUser, resetInfo.Verification, tfaSecret);

        }

        /// <summary>
        /// Creates a security user.
        /// </summary>
        /// <param name="user">The security user to be created.</param>
        /// <returns>Returns the newly created security user.</returns>
        public SecurityUserInfo CreateUser(SecurityUserInfo user)
		{
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			var roleProviderService = ApplicationContext.Current.GetService<IRoleProviderService>();

			var userToCreate = new Core.Model.Security.SecurityUser()
			{
				UserName = user.UserName,
				Email = user.Email,
                UserClass = user.User.UserClass == Guid.Empty ? UserClassKeys.HumanUser : user.User.UserClass
			};

			if (user.Lockout.HasValue && user.Lockout.Value)
			{
				userToCreate.Lockout = DateTime.MaxValue;
			}

			var securityUser = userRepository.CreateUser(userToCreate, user.Password);

			if (user.Roles != null)
				roleProviderService.AddUsersToRoles(new String[] { user.UserName }, user.Roles.Select(o => o.Name).ToArray(), AuthenticationContext.Current.Principal);

			return new SecurityUserInfo(securityUser);
		}

		/// <summary>
		/// Deletes an applet.
		/// </summary>
		/// <param name="appletId">The id of the applet to be deleted.</param>
		/// <returns>Returns the deleted applet.</returns>
		public AppletManifestInfo DeleteApplet(string appletId)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Deletes an assigning authority.
		/// </summary>
		/// <param name="assigningAuthorityId">The id of the assigning authority to be deleted.</param>
		/// <returns>Returns the deleted assigning authority.</returns>
		public AssigningAuthorityInfo DeleteAssigningAuthority(string assigningAuthorityId)
		{
			throw new NotImplementedException();
		}

		/// <summary>
        /// Deletes a specified certificate.
        /// </summary>
        /// <param name="id">The id of the certificate to be deleted.</param>
        /// <param name="reason">The reason the certificate is to be deleted.</param>
        /// <returns>Returns the deletion result.</returns>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public SubmissionResult DeleteCertificate(string rawId, OpenIZ.Core.Model.AMI.Security.RevokeReason reason)
		{
			int id = Int32.Parse(rawId);
			var result = this.m_certTool.GetRequestStatus(id);

			if (String.IsNullOrEmpty(result.AuthorityResponse))
				throw new InvalidOperationException("Cannot revoke an un-issued certificate");
			// Now get the serial key
			SignedCms importer = new SignedCms();
			importer.Decode(Convert.FromBase64String(result.AuthorityResponse));

			foreach (var cert in importer.Certificates)
				if (cert.Subject != cert.Issuer)
					this.m_certTool.RevokeCertificate(cert.SerialNumber, (MARC.Util.CertificateTools.RevokeReason)reason);

			result.Outcome = SubmitOutcome.Revoked;
			result.AuthorityResponse = null;
			return new SubmissionResult(result.Message, result.RequestId, (SubmissionStatus)result.Outcome, result.AuthorityResponse);
		}

		/// <summary>
		/// Deletes a device.
		/// </summary>
		/// <param name="deviceId">The id of the device to be deleted.</param>
		/// <returns>Returns the deleted device.</returns>
		public SecurityDevice DeleteDevice(string deviceId)
		{
			Guid deviceKey = Guid.Empty;

			if (!Guid.TryParse(deviceId, out deviceKey))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(deviceId)));
			}

			var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (securityRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			return securityRepository.ObsoleteDevice(deviceKey);
		}

		/// <summary>
		/// Deletes a place.
		/// </summary>
		/// <param name="placeId">The id of the place to be deleted.</param>
		/// <returns>Returns the deleted place.</returns>
		public Place DeletePlace(string placeId)
		{
			Guid placeKey = Guid.Empty;

			if (!Guid.TryParse(placeId, out placeKey))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(placeId)));
			}

			var placeRepository = ApplicationContext.Current.GetService<IPlaceRepositoryService>();

			if (placeRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IPlaceRepositoryService)));
			}

			return placeRepository.Obsolete(placeKey);
		}

		/// <summary>
		/// Deletes a security policy.
		/// </summary>
		/// <param name="policyId">The id of the policy to be deleted.</param>
		/// <returns>Returns the deleted policy.</returns>
		public SecurityPolicyInfo DeletePolicy(string policyId)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Deletes a security role.
		/// </summary>
		/// <param name="roleId">The id of the role to be deleted.</param>
		/// <returns>Returns the deleted role.</returns>
		public SecurityRoleInfo DeleteRole(string rawRoleId)
		{
			Guid roleId = Guid.Empty;
			if (!Guid.TryParse(rawRoleId, out roleId))
				throw new ArgumentException(nameof(rawRoleId));
			var roleRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new SecurityRoleInfo(roleRepository.ObsoleteRole(roleId));
		}

		/// <summary>
		/// Deletes a security user.
		/// </summary>
		/// <param name="userId">The id of the user to be deleted.</param>
		/// <returns>Returns the deleted user.</returns>
		public SecurityUserInfo DeleteUser(string rawUserId)
		{
			Guid userId = Guid.Parse(rawUserId);
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new SecurityUserInfo(userRepository.ObsoleteUser(userId));
		}

		/// <summary>
		/// Gets a specific alert.
		/// </summary>
		/// <param name="id">The id of the alert to retrieve.</param>
		/// <returns>Returns the alert.</returns>
		public AlertMessageInfo GetAlert(string id)
		{
			var alertRepository = ApplicationContext.Current.GetService<IAlertRepositoryService>();

			if (alertRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAlertRepositoryService)));
			}

			var alert = alertRepository.Get(Guid.Parse(id));

			return new AlertMessageInfo(alert);
		}

		/// <summary>
		/// Gets a list of alert for a specific query.
		/// </summary>
		/// <returns>Returns a list of alert which match the specific query.</returns>
		public AmiCollection<AlertMessageInfo> GetAlerts()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<AlertMessage>(this.CreateQuery(parameters));

			var alertRepository = ApplicationContext.Current.GetService<IAlertRepositoryService>();

			if (alertRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAlertRepositoryService)));
			}

			AmiCollection<AlertMessageInfo> alerts = new AmiCollection<AlertMessageInfo>();

			int totalCount = 0;

			alerts.CollectionItem = alertRepository.Find(expression, 0, null, out totalCount).Select(a => new AlertMessageInfo(a)).ToList();
			alerts.Size = totalCount;

			return alerts;
		}

		/// <summary>
		/// Gets a list of applets for a specific query.
		/// </summary>
		/// <returns>Returns a list of applet which match the specific query.</returns>
		public AmiCollection<AppletManifestInfo> GetApplets()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets a list of assigning authorities for a specific query.
		/// </summary>
		/// <returns>Returns a list of assigning authorities which match the specific query.</returns>
		public AmiCollection<AssigningAuthorityInfo> GetAssigningAuthorities()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<AssigningAuthority>(this.CreateQuery(parameters));

			var assigningAuthorityRepositoryService = ApplicationContext.Current.GetService<IAssigningAuthorityRepositoryService>();

			if (assigningAuthorityRepositoryService == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAssigningAuthorityRepositoryService)));
			}

			AmiCollection<AssigningAuthorityInfo> assigningAuthorities = new AmiCollection<AssigningAuthorityInfo>();

			int totalCount = 0;

			assigningAuthorities.CollectionItem = assigningAuthorityRepositoryService.Find(expression, 0, null, out totalCount).Select(a => new AssigningAuthorityInfo(a)).ToList();
			assigningAuthorities.Size = totalCount;

			return assigningAuthorities;
		}

		/// <summary>
		/// Gets a specific certificate.
		/// </summary>
		/// <param name="id">The id of the certificate to retrieve.</param>
		/// <returns>Returns the certificate.</returns>
		public byte[] GetCertificate(string rawId)
		{
			int id = Int32.Parse(rawId);
			WebOperationContext.Current.OutgoingResponse.ContentType = "application/x-pkcs12";
			WebOperationContext.Current.OutgoingResponse.Headers.Add("Content-Disposition", String.Format("attachment; filename=\"crt-{0}.p12\"", id));
			var result = this.m_certTool.GetRequestStatus(id);
			return Encoding.UTF8.GetBytes(result.AuthorityResponse);
		}

		/// <summary>
		/// Gets a list of certificates.
		/// </summary>
		/// <returns>Returns a list of certificates.</returns>
		public AmiCollection<X509Certificate2Info> GetCertificates()
		{
			AmiCollection<X509Certificate2Info> collection = new AmiCollection<X509Certificate2Info>();
			var certs = this.m_certTool.GetCertificates();
			foreach (var cert in certs)
				collection.CollectionItem.Add(new X509Certificate2Info(cert.Attribute));
			return collection;
		}

		/// <summary>
		/// Gets a list of concepts.
		/// </summary>
		/// <returns>Returns a list of concepts.</returns>
		public AmiCollection<Concept> GetConcepts()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<Concept>(this.CreateQuery(parameters));

			var conceptRepository = ApplicationContext.Current.GetService<IConceptRepositoryService>();

			if (conceptRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IConceptRepositoryService)));
			}

			return new AmiCollection<Concept>()
			{
				CollectionItem = conceptRepository.FindConcepts(expression).ToList()
			};
		}

		/// <summary>
		/// Gets a list of concept sets.
		/// </summary>
		/// <returns>Returns a list of concept sets.</returns>
		public AmiCollection<ConceptSet> GetConceptSets()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<ConceptSet>(this.CreateQuery(parameters));

			var conceptRepository = ApplicationContext.Current.GetService<IConceptRepositoryService>();

			if (conceptRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IConceptRepositoryService)));
			}

			return new AmiCollection<ConceptSet>()
			{
				CollectionItem = conceptRepository.FindConceptSets(expression).ToList()
			};
		}

		/// <summary>
		/// Gets the certificate revocation list.
		/// </summary>
		/// <returns>Returns the certificate revocation list.</returns>
		public byte[] GetCrl()
		{
			WebOperationContext.Current.OutgoingResponse.ContentType = "application/x-pkcs7-crl";
			WebOperationContext.Current.OutgoingResponse.Headers.Add("Content-Disposition", "attachment; filename=\"openiz.crl\"");
			return Encoding.UTF8.GetBytes(this.m_certTool.GetCRL());
		}

		/// <summary>
		/// Gets a specific certificate signing request.
		/// </summary>
		/// <param name="id">The id of the certificate signing request to be retrieved.</param>
		/// <returns>Returns the certificate signing request.</returns>
		public SubmissionResult GetCsr(string rawId)
		{
			int id = Int32.Parse(rawId);
			var submission = this.m_certTool.GetRequestStatus(id);

			var result = new SubmissionResult(submission.Message, submission.RequestId, (SubmissionStatus)submission.Outcome, submission.AuthorityResponse);
			return result;
		}

		/// <summary>
		/// Gets a list of submitted certificate signing requests.
		/// </summary>
		/// <returns>Returns a list of certificate signing requests.</returns>
		public AmiCollection<SubmissionInfo> GetCsrs()
		{
			AmiCollection<SubmissionInfo> collection = new AmiCollection<SubmissionInfo>();
			var certs = this.m_certTool.GetCertificates();
			foreach (var cert in certs)
			{
				SubmissionInfo info = new SubmissionInfo();
				foreach (var kv in cert.Attribute)
				{
					string key = kv.Key.Replace("Request.", "");
					PropertyInfo pi = typeof(CertificateInfo).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
					if (pi != null)
						pi.SetValue(info, kv.Value, null);
				}
				info.XmlStatusCode = (SubmissionStatus)this.m_certTool.GetRequestStatus(Int32.Parse(info.RequestID)).Outcome;
				if (info.XmlStatusCode == SubmissionStatus.Submission)
					collection.CollectionItem.Add(info);
			}
			return collection;
		}

		/// <summary>
		/// Gets a list of devices.
		/// </summary>
		/// <returns>Returns a list of devices.</returns>
		public AmiCollection<SecurityDevice> GetDevices()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<SecurityDevice>(this.CreateQuery(parameters));

			var securityRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (securityRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			return new AmiCollection<SecurityDevice>
			{
				CollectionItem = securityRepository.FindDevices(expression).ToList()
			};
		}

		/// <summary>
		/// Gets a list of places.
		/// </summary>
		/// <returns>Returns a list of places.</returns>
		public AmiCollection<Place> GetPlaces()
		{
			var parameters = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;

			if (parameters.Count == 0)
			{
				throw new ArgumentException(string.Format("{0} cannot be empty", nameof(parameters)));
			}

			var expression = QueryExpressionParser.BuildLinqExpression<Place>(this.CreateQuery(parameters));

			var placeRepository = ApplicationContext.Current.GetService<IPlaceRepositoryService>();

			if (placeRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IPlaceRepositoryService)));
			}

			return new AmiCollection<Place>()
			{
				CollectionItem = placeRepository.Find(expression).ToList()
			};
		}

		/// <summary>
		/// Gets a list of policies.
		/// </summary>
		/// <returns>Returns a list of policies.</returns>
		public AmiCollection<SecurityPolicyInfo> GetPolicies()
		{
			var expression = QueryExpressionParser.BuildLinqExpression<SecurityPolicy>(this.CreateQuery(WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters));
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new AmiCollection<SecurityPolicyInfo>() { CollectionItem = userRepository.FindPolicies(expression).Select(o => new SecurityPolicyInfo(o)).ToList() };
		}

		/// <summary>
		/// Gets a specific security policy.
		/// </summary>
		/// <param name="policyId">The id of the security policy to be retrieved.</param>
		/// <returns>Returns the security policy.</returns>
		public SecurityPolicyInfo GetPolicy(string policyId)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets a specific security role.
		/// </summary>
		/// <param name="roleId">The id of the security role to be retrieved.</param>
		/// <returns>Returns the security role.</returns>
		public SecurityRoleInfo GetRole(string rawRoleId)
		{
			Guid roleId = Guid.Empty;
			if (!Guid.TryParse(rawRoleId, out roleId))
				throw new ArgumentException(nameof(rawRoleId));
			var roleRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new SecurityRoleInfo(roleRepository.GetRole(roleId));
		}

		/// <summary>
		/// Gets a list of security roles.
		/// </summary>
		/// <returns>Returns a list of security roles.</returns>
		public AmiCollection<SecurityRoleInfo> GetRoles()
		{
			var expression = QueryExpressionParser.BuildLinqExpression<SecurityRole>(this.CreateQuery(WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters));
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new AmiCollection<SecurityRoleInfo>() { CollectionItem = userRepository.FindRoles(expression).Select(o => new SecurityRoleInfo(o)).ToList() };
		}

		/// <summary>
		/// Gets the schema for the administrative interface.
		/// </summary>
		/// <param name="schemaId">The id of the schema to be retrieved.</param>
		/// <returns>Returns the administrative interface schema.</returns>
		public XmlSchema GetSchema(int schemaId)
		{
			try
			{
				XmlSchemas schemaCollection = new XmlSchemas();

				XmlReflectionImporter importer = new XmlReflectionImporter("http://openiz.org/ami");
				XmlSchemaExporter exporter = new XmlSchemaExporter(schemaCollection);

				foreach (var cls in typeof(IAmiContract).GetCustomAttributes<ServiceKnownTypeAttribute>().Select(o => o.Type))
					exporter.ExportTypeMapping(importer.ImportTypeMapping(cls, "http://openiz.org/ami"));

				if (schemaId > schemaCollection.Count)
				{
					WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.NotFound;
					return null;
				}
				else
				{
					WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
					WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
					return schemaCollection[schemaId];
				}
			}
			catch (Exception e)
			{
				WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
				this.m_traceSource.TraceEvent(TraceEventType.Error, e.HResult, e.ToString());
				return null;
			}
		}

        /// <summary>
        /// Get a list of TFA mechanisms
        /// </summary>
        /// <returns>Returns a list of TFA mechanisms.</returns>
        public AmiCollection<TfaMechanismInfo> GetTfaMechanisms()
        {
            var tfaRelay = ApplicationContext.Current.GetService<ITfaRelayService>();
            if (tfaRelay == null)
                throw new InvalidOperationException("TFA Relay missing");
            return new AmiCollection<TfaMechanismInfo>()
            {
                CollectionItem = tfaRelay.Mechanisms.Select(o => new TfaMechanismInfo()
                {
                    Id = o.Id,
                    Name = o.Name,
                    ChallengeText = o.Challenge
                }).ToList()
            };
        }

        /// <summary>
        /// Gets a specific security user.
        /// </summary>
        /// <param name="userId">The id of the security user to be retrieved.</param>
        /// <returns>Returns the security user.</returns>
        public SecurityUserInfo GetUser(string rawUserId)
		{
			Guid userId = Guid.Empty;
			if (!Guid.TryParse(rawUserId, out userId))
				throw new ArgumentException(nameof(rawUserId));
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new SecurityUserInfo(userRepository.GetUser(userId));
		}

		/// <summary>
		/// Gets a list of security users.
		/// </summary>
		/// <returns>Returns a list of security users.</returns>
		public AmiCollection<SecurityUserInfo> GetUsers()
		{
			var expression = QueryExpressionParser.BuildLinqExpression<SecurityUser>(this.CreateQuery(WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters));
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			return new AmiCollection<SecurityUserInfo>() { CollectionItem = userRepository.FindUsers(expression).Select(o => new SecurityUserInfo(o)).ToList() };
		}

        /// <summary>
        /// Rejects a specified certificate signing request.
        /// </summary>
        /// <param name="certId">The id of the certificate signing request to be rejected.</param>
        /// <param name="reason">The reason the certificate signing request is to be rejected.</param>
        /// <returns>Returns the rejection result.</returns>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public SubmissionResult RejectCsr(string rawId, OpenIZ.Core.Model.AMI.Security.RevokeReason reason)
		{
			int id = Int32.Parse(rawId);
			this.m_certTool.DenyRequest(id);
			var status = this.m_certTool.GetRequestStatus(id);

			var result = new SubmissionResult(status.Message, status.RequestId, (SubmissionStatus)status.Outcome, status.AuthorityResponse);
			result.Certificate = null;
			return result;
		}

        /// <summary>
        /// Submits a specific certificate signing request.
        /// </summary>
        /// <param name="s">The certificate signing request.</param>
        /// <returns>Returns the submission result.</returns>
        [PolicyPermission(SecurityAction.Demand, PolicyId = PermissionPolicyIdentifiers.UnrestrictedAdministration)]
        public SubmissionResult SubmitCsr(SubmissionRequest s)
		{
			var submission = this.m_certTool.SubmitRequest(s.CmcRequest, s.AdminContactName, s.AdminAddress);

			var result = new SubmissionResult(submission.Message, submission.RequestId, (SubmissionStatus)submission.Outcome, submission.AuthorityResponse);
			if (this.m_configuration.CaConfiguration.AutoApprove)
				return this.AcceptCsr(result.RequestId.ToString());
			else
				return result;
		}

		/// <summary>
		/// Updates an alert.
		/// </summary>
		/// <param name="alertId">The id of the alert to be updated.</param>
		/// <param name="alert">The alert containing the updated information.</param>
		/// <returns>Returns the updated alert.</returns>
		public AlertMessageInfo UpdateAlert(string alertId, AlertMessageInfo alert)
		{
			Guid key = Guid.Empty;

			if (!Guid.TryParse(alertId, out key))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(alertId)));
			}

			var alertRepository = ApplicationContext.Current.GetService<IAlertRepositoryService>();

			if (alertRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAlertRepositoryService)));
			}

			var updatedAlert = alertRepository.Save(alert.AlertMessage);

			return new AlertMessageInfo(updatedAlert);
		}

		/// <summary>
		/// Updates an applet.
		/// </summary>
		/// <param name="appletId">The id of the applet to be updated.</param>
		/// <param name="appletManifestInfo">The applet containing the updated information.</param>
		public AppletManifestInfo UpdateApplet(string appletId, AppletManifestInfo appletManifestInfo)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Updates an assigning authority.
		/// </summary>
		/// <param name="assigningAuthorityId">The id of the assigning authority to be updated.</param>
		/// <param name="assigningAuthorityInfo">The assigning authority containing the updated information.</param>
		/// <returns>Returns the updated assigning authority.</returns>
		public AssigningAuthorityInfo UpdateAssigningAuthority(string assigningAuthorityId, AssigningAuthorityInfo assigningAuthorityInfo)
		{
			Guid id = Guid.Empty;

			if (!Guid.TryParse(assigningAuthorityId, out id))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(assigningAuthorityId)));
			}

			if (assigningAuthorityInfo.Id != id)
			{
				throw new ArgumentException(string.Format("Unable to update assigning authority using id: {0}, and id: {1}", id, assigningAuthorityInfo.Id));
			}

			var assigningAuthorityRepositoryService = ApplicationContext.Current.GetService<IAssigningAuthorityRepositoryService>();

			if (assigningAuthorityRepositoryService == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IAssigningAuthorityRepositoryService)));
			}

			var result = assigningAuthorityRepositoryService.Save(assigningAuthorityInfo.AssigningAuthority);

			return new AssigningAuthorityInfo(result);
		}

		/// <summary>
		/// Updates a concept.
		/// </summary>
		/// <param name="conceptId">The id of the concept to update.</param>
		/// <param name="concept">The concept containing the updated model.</param>
		/// <returns>Returns the newly updated concept.</returns>
		public Concept UpdateConcept(string conceptId, Concept concept)
		{
			Guid key = Guid.Parse(conceptId);

			if (concept.Key != key)
			{
				throw new ArgumentException(nameof(conceptId));
			}

			var conceptRepository = ApplicationContext.Current.GetService<IConceptRepositoryService>();

			if (conceptRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IConceptRepositoryService)));
			}

			return conceptRepository.SaveConcept(concept);
		}

		/// <summary>
		/// Updates a place.
		/// </summary>
		/// <param name="place">The place containing the update information.</param>
		/// <returns>Returns the updated place.</returns>
		public Place UpdatePlace(string placeId, Place place)
		{
			Guid key = Guid.Parse(placeId);

			if (place.Key != key)
			{
				throw new ArgumentException(nameof(placeId));
			}

			var placeRepository = ApplicationContext.Current.GetService<IPlaceRepositoryService>();

			if (placeRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(IPlaceRepositoryService)));
			}

			return placeRepository.Save(place);
		}

		/// <summary>
		/// Updates a policy.
		/// </summary>
		/// <param name="policyId">The id of the policy to be updated.</param>
		/// <param name="policyInfo">The policy containing the updated information.</param>
		/// <returns>Returns the updated policy.</returns>
		public SecurityPolicyInfo UpdatePolicy(string policyId, SecurityPolicyInfo policyInfo)
		{
			Guid id = Guid.Empty;

			if (!Guid.TryParse(policyId, out id))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(policyId)));
			}

			if (policyInfo.Policy.Key != id)
			{
				throw new ArgumentException(string.Format("Unable to update role using id: {0}, and id: {1}", id, policyInfo.Policy.Key));
			}

			var policyRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (policyRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			var policy = policyRepository.SavePolicy(policyInfo.Policy);

			return new SecurityPolicyInfo(policy);
		}

		/// <summary>
		/// Updates a role.
		/// </summary>
		/// <param name="roleId">The id of the role to be updated.</param>
		/// <param name="roleInfo">The role containing the updated information.</param>
		/// <returns>Returns the updated role.</returns>
		public SecurityRoleInfo UpdateRole(string roleId, SecurityRoleInfo roleInfo)
		{
			Guid id = Guid.Empty;

			if (!Guid.TryParse(roleId, out id))
			{
				throw new ArgumentException(string.Format("{0} must be a valid GUID", nameof(roleId)));
			}

			if (roleInfo.Id != id)
			{
				throw new ArgumentException(string.Format("Unable to update role using id: {0}, and id: {1}", id, roleInfo.Id));
			}

			var roleRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

			if (roleRepository == null)
			{
				throw new InvalidOperationException(string.Format("{0} not found", nameof(ISecurityRepositoryService)));
			}

			var role = roleRepository.SaveRole(roleInfo.Role);

			return new SecurityRoleInfo(role);
		}

		/// <summary>
		/// Updates a security user.
		/// </summary>
		/// <param name="userId">The id of the security user to be updated.</param>
		/// <param name="info">The security user containing the updated information.</param>
		/// <returns>Returns the updated security user.</returns>
		public SecurityUserInfo UpdateUser(string rawUserId, SecurityUserInfo info)
		{
			Guid userId = Guid.Parse(rawUserId);
			// First change password if needed
			var userRepository = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
			var idpService = ApplicationContext.Current.GetService<IIdentityProviderService>();
            if (!String.IsNullOrEmpty(info.Password))
            {
                var user = userRepository.ChangePassword(userId, info.Password);
                idpService.RemoveClaim(user.UserName, OpenIzClaimTypes.OpenIZPasswordlessAuth);
            }

			if (info.Email != null)
			{
				SecurityUserInfo userInfo = new SecurityUserInfo(userRepository.SaveUser(new Core.Model.Security.SecurityUser()
				{
					Key = userId,
					Email = info.Email
				}));
			}

			if (info.Lockout.HasValue)
			{
				if (info.Lockout.Value)
					userRepository.LockUser(userId);
				else
					userRepository.UnlockUser(userId);
			}

			// First, we remove the roles
			if (info.Roles != null && info.Roles.Count > 0)
			{
				var irps = ApplicationContext.Current.GetService<IRoleProviderService>();

				var roles = irps.GetAllRoles(info.UserName);

				// if the roles provided are not equal to the current roles of the user, only then change the roles of the user
				if (roles != info.Roles.Select(r => r.Name).ToArray())
				{
					irps.RemoveUsersFromRoles(new String[] { info.UserName }, info.Roles.Select(o => o.Name).ToArray(), AuthenticationContext.Current.Principal);
					irps.AddUsersToRoles(new String[] { info.UserName }, info.Roles.Select(o => o.Name).ToArray(), AuthenticationContext.Current.Principal);
				}
			}

			return info;
		}

		/// <summary>
		/// Creates a query
		/// </summary>
		/// <param name="nvc">The name value collection to use to create the query.</param>
		/// <returns>Returns the created query.</returns>
		private NameValueCollection CreateQuery(System.Collections.Specialized.NameValueCollection nvc)
		{
			var retVal = new OpenIZ.Core.Model.Query.NameValueCollection();

			foreach (var k in nvc.AllKeys)
			{
				retVal.Add(k, new List<String>(nvc.GetValues(k)));
			}

			return retVal;
		}
	}
}