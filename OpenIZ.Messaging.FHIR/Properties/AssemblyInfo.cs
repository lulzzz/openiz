﻿using MARC.HI.EHRS.SVC.Core.Plugins;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("OpenIZ FHIR DSTU2 Messaging")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Mohawk College of Applied Arts and Technology")]
[assembly: AssemblyProduct("OpenIZ.Messaging.FHIR")]
[assembly: AssemblyCopyright("Copyright © 2016 Mohawk College of Applied Arts and Technology")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("33836324-c699-4139-ab9c-7524570a04d5")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Assembly plugin info
[assembly: AssemblyPlugin()]

// Depends: OpenIZ.Core v1.0.0.0
[assembly: AssemblyPluginDependency("OpenIZ.Core", "1.0.0.0")]