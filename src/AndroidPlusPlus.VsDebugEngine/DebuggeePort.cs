﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using AndroidPlusPlus.Common;
using AndroidPlusPlus.VsDebugCommon;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsDebugEngine
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class DebuggeePort : IDebugPort2, IDebugPortNotify2, IConnectionPoint, IConnectionPointContainer
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private class Enumerator : DebugEnumerator<IDebugPort2, IEnumDebugPorts2>, IEnumDebugPorts2
    {
      public Enumerator (List<IDebugPort2> ports)
        : base (ports)
      {
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private readonly IDebugPortSupplier2 m_portSupplier;

    private readonly AndroidDevice m_portDevice;

    private readonly Guid m_portGuid;

    private Dictionary<uint, DebuggeeProcess> m_portProcesses;

    private Dictionary<int, IDebugPortEvents2> m_eventConnectionPoints;

    private int m_eventConnectionPointCookie = 1;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggeePort (IDebugPortSupplier2 portSupplier, AndroidDevice device)
    {
      m_portSupplier = portSupplier;

      m_portDevice = device;

      m_portGuid = Guid.NewGuid ();

      m_portProcesses = new Dictionary<uint, DebuggeeProcess> ();

      m_eventConnectionPoints = new Dictionary<int, IDebugPortEvents2> ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidDevice PortDevice 
    {
      get
      {
        return m_portDevice;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggeeProcess GetProcessForPid (uint pid)
    {
      DebuggeeProcess process = null;

      m_portProcesses.TryGetValue (pid, out process);

      return process;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int RefreshProcesses ()
    {
      // 
      // Check which processes are currently running on the target device (port).
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        m_portDevice.RefreshProcesses ();

        m_portProcesses.Clear ();

        // 
        // Register a new process with this port if it was spawned by 'zygote'.
        // 

        {
          uint [] zygotePids = m_portDevice.GetPidsFromName ("zygote");

          uint [] activeZygoteSpawnedPids = m_portDevice.GetChildPidsFromPpid (zygotePids [0]);

          for (int i = 0; i < activeZygoteSpawnedPids.Length; ++i)
          {
            uint pid = activeZygoteSpawnedPids [i];

            AndroidProcess nativeProcess = m_portDevice.GetProcessFromPid (pid);

            m_portProcesses.Add (pid, new DebuggeeProcess (this, nativeProcess));
          }
        }

        // 
        // Register a new process with this port if it was spawned by 'zygote64' (it's a 64-bit process).
        // 

        {
          uint [] zygote64Pids = m_portDevice.GetPidsFromName ("zygote64");

          uint [] activeZygote64SpawnedPids = m_portDevice.GetChildPidsFromPpid (zygote64Pids [0]);

          for (int i = 0; i < activeZygote64SpawnedPids.Length; ++i)
          {
            uint pid = activeZygote64SpawnedPids [i];

            AndroidProcess nativeProcess = m_portDevice.GetProcessFromPid (pid);

            m_portProcesses.Add (pid, new DebuggeeProcess (this, nativeProcess));
          }
        }

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugPort2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int EnumProcesses (out IEnumDebugProcesses2 ppEnum)
    {
      // 
      // Returns a list of all the processes running on a port.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        RefreshProcesses ();

        DebuggeeProcess [] processes = new DebuggeeProcess [m_portProcesses.Count];

        m_portProcesses.Values.CopyTo (processes, 0);

        ppEnum = new DebuggeeProcess.Enumerator (processes);

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        ppEnum = null;

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortId (out Guid pguidPort)
    {
      // 
      // Gets the port identifier.
      // 

      LoggingUtils.PrintFunction ();

      pguidPort = m_portGuid;

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortName (out string pbstrName)
    {
      // 
      // Gets the port name.
      // 

      LoggingUtils.PrintFunction ();

      pbstrName = "adb://" + m_portDevice.ID;

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortRequest (out IDebugPortRequest2 ppRequest)
    {
      // 
      // Gets the description of a port that was previously used to create the port (if available).
      // 

      LoggingUtils.PrintFunction ();

      ppRequest = null;

      return Constants.E_PORT_NO_REQUEST;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetPortSupplier (out IDebugPortSupplier2 ppSupplier)
    {
      // 
      // Gets the port supplier for this port.
      // 

      LoggingUtils.PrintFunction ();

      ppSupplier = m_portSupplier;

      return Constants.S_OK;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetProcess (AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess)
    {
      // 
      // Gets the specified process running on a port.
      // 

      LoggingUtils.PrintFunction ();

      ppProcess = null;

      try
      {
        if (ProcessId.ProcessIdType == (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
        {
          LoggingUtils.RequireOk (RefreshProcesses ());

          DebuggeeProcess process = GetProcessForPid (ProcessId.dwProcessId);

          if (process == null)
          {
            throw new InvalidOperationException (string.Format ("Could not locate requested process. Pid: {0}", ProcessId.dwProcessId));
          }

          ppProcess = process as IDebugProcess2;
        }
        else /*if (ProcessId.ProcessIdType == (uint) enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID)*/
        {
          throw new NotImplementedException ();
        }

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IDebugPortNotify2 Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [InheritGuid (typeof (IDebugProgramCreateEvent2))]
    public sealed class ProgramCreate : ImmediateDebugEvent, IDebugProgramCreateEvent2
    {
      // Immediate-mode implementation of a 'ProgramCreate' event.
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int AddProgramNode (IDebugProgramNode2 pProgramNode)
    {
      // 
      // Registers a program that can be debugged with the port it is running on.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        IDebugProcess2 process;

        DebuggeeProgram program = pProgramNode as DebuggeeProgram;

        LoggingUtils.RequireOk (program.GetProcess (out process));

        foreach (IDebugPortEvents2 connectionPoint in m_eventConnectionPoints.Values)
        {
          ProgramCreate debugEvent = new ProgramCreate ();

          Guid eventGuid = ComUtils.GuidOf (debugEvent);

          int handle = connectionPoint.Event (null, this, process, program, debugEvent, ref eventGuid);

          if (handle == unchecked ((int)0x80010108)) // RPC_E_DISCONNECTED
          {
            continue; // Connection point was previously used.
          }

          LoggingUtils.RequireOk (handle);
        }

        return Constants.S_OK;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_FAIL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int RemoveProgramNode (IDebugProgramNode2 pProgramNode)
    {
      // 
      // Unregisters a program that can be debugged from the port it is running on.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        throw new NotImplementedException ();
      }
      catch (NotImplementedException e)
      {
        LoggingUtils.HandleException (e);

        return Constants.E_NOTIMPL;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IConnectionPoint Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private sealed class ConnectionEnumerator : DebugConnectionEnumerator<System.Runtime.InteropServices.ComTypes.CONNECTDATA, IEnumConnections>, IEnumConnections
    {
      public ConnectionEnumerator (List<System.Runtime.InteropServices.ComTypes.CONNECTDATA> connections)
        : base (connections)
      {
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Advise (object pUnkSink, out int pdwCookie)
    {
      // 
      // Establishes an advisory connection between the connection point and the caller's sink object.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        IDebugPortEvents2 portEvent = (IDebugPortEvents2) pUnkSink;

        m_eventConnectionPoints.Add (m_eventConnectionPointCookie, portEvent);

        pdwCookie = m_eventConnectionPointCookie++;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        pdwCookie = 0;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void EnumConnections (out IEnumConnections ppEnum)
    {
      // 
      // Creates an enumerator object for iteration through the connections that exist to this connection point.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        List<System.Runtime.InteropServices.ComTypes.CONNECTDATA> connections = new List<System.Runtime.InteropServices.ComTypes.CONNECTDATA> ();

        foreach (KeyValuePair <int, IDebugPortEvents2> keyPair in m_eventConnectionPoints)
        {
          connections.Add (new System.Runtime.InteropServices.ComTypes.CONNECTDATA ()
          {
            dwCookie = keyPair.Key,
            pUnk = (object) keyPair.Value
          });
        }

        ppEnum = new ConnectionEnumerator (connections);
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        ppEnum = null;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void GetConnectionInterface (out Guid pIID)
    {
      // 
      // Returns the IID of the outgoing interface managed by this connection point.
      // 

      LoggingUtils.PrintFunction ();

      pIID = ComUtils.GuidOf (typeof (IDebugPortEvents2));
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void GetConnectionPointContainer (out IConnectionPointContainer ppCPC)
    {
      // 
      // Retrieves the IConnectionPointContainer interface pointer to the connectable object that conceptually owns this connection point.
      // 

      LoggingUtils.PrintFunction ();

      ppCPC = this;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Unadvise (int dwCookie)
    {
      // 
      // Terminates an advisory connection previously established through the System.Runtime.InteropServices.ComTypes.IConnectionPoint.Advise(System.Object,System.Int32@) method.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        m_eventConnectionPoints.Remove (dwCookie);
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #region IConnectionPointContainer Members

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private sealed class ConnectionPointEnumerator : DebugConnectionEnumerator<IConnectionPoint, IEnumConnectionPoints>, IEnumConnectionPoints
    {
      public ConnectionPointEnumerator (List<IConnectionPoint> points)
        : base (points)
      {
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void EnumConnectionPoints (out IEnumConnectionPoints ppEnum)
    {
      // 
      // Creates an enumerator of all the connection points supported in the connectable object, one connection point per IID.
      // 

      LoggingUtils.PrintFunction ();

      ppEnum = null;

      try
      {
        List<IConnectionPoint> connectionPoints = new List<IConnectionPoint> ();

        connectionPoints.Add (this); // one connection point per IID.

        ppEnum = new ConnectionPointEnumerator (connectionPoints);
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        ppEnum = null;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void FindConnectionPoint (ref Guid riid, out IConnectionPoint ppCP)
    {
      // 
      // Asks the connectible object if it has a connection point for a particular IID, 
      // and if so, returns the IConnectionPoint interface pointer to that connection point.
      // 

      LoggingUtils.PrintFunction ();

      ppCP = null;

      try
      {
        Guid connectionPort;

        GetConnectionInterface (out connectionPort);

        if (riid.Equals (connectionPort))
        {
          ppCP = this;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    #endregion

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
