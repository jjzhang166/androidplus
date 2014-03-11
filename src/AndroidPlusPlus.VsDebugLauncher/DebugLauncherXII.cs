﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debuggers;
using Microsoft.VisualStudio.ProjectSystem.Utilities;
using Microsoft.VisualStudio.ProjectSystem.Utilities.DebuggerProviders;
using Microsoft.VisualStudio.ProjectSystem.VS.Debuggers;

using AndroidPlusPlus.Common;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsDebugLauncher
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  [ExportDebugger("AndroidPlusPlusDebugger")]

  [PartMetadata(ProjectCapabilities.Requires, ProjectCapabilities.VisualC)]

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class DebugLauncherXII : DebugLaunchProviderBase
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [Import]
    private Rules.RuleProperties DebuggerProperties { get; set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override async Task<bool> CanLaunchAsync(DebugLaunchOptions launchOptions)
    {
      return DebugLauncher.CanLaunch ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(DebugLaunchOptions launchOptions)
    {
      var settings = new DebugLaunchSettings(launchOptions);

      // The properties that are available via DebuggerProperties are determined by the property XAML files in your project.
      var debuggerProperties = await this.DebuggerProperties.GetAndroidPlusPlusDebuggerPropertiesAsync();
      //settings.Executable = await debuggerProperties.LocalDebuggerCommand.GetEvaluatedValueAtEndAsync();
      if (settings.Executable == null)
      {
        var generalProperties = await this.DebuggerProperties.GetConfigurationGeneralPropertiesAsync();
        settings.Executable = await generalProperties.TargetPath.GetEvaluatedValueAtEndAsync();
      }

      var test = DebuggerProperties.GetConfigurationGeneralPropertiesAsync ();

      //settings.Arguments = await debuggerProperties.LocalDebuggerCommandArguments.GetEvaluatedValueAtEndAsync();

      //settings.CurrentDirectory = await debuggerProperties.LocalDebuggerWorkingDirectory.GetEvaluatedValueAtEndAsync();

      settings.LaunchOperation = DebugLaunchOperation.CreateProcess;

      settings.LaunchDebugEngineGuid = DebuggerEngines.NativeOnlyEngine;

      return new IDebugLaunchSettings[] { settings };
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
