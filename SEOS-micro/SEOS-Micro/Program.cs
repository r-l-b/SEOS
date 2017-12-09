using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // The following dictionaries contain all module info

        public Dictionary<string, IMyProgrammableBlock> modules;    // addresses of all modules
        public Dictionary<string, string>               actions;    // inputs for all modules
        public Dictionary<string, string>               assigns;    // assigns of all modules - if unassigned, value is null

        // Miscellaneous

        IMyProgrammableBlock kernel;
        IMyTextPanel panel;
        StringBuilder log;
        bool isVerbose = true;

        // The following constants are standard arguments

        const string K_BASE     = "SEOS";   // SEOS     system identifier (required)
        const string K_ACTION   = "ACT";    // action   system call
        const string K_RETURN   = "RTN";    // return   return data to caller
        const string K_UNASSIGN = "REL";    // release  unassign module
        const string K_LOAD     = "LD";     // load     initial setup
        const string K_PING     = "PING";   // ping     module ping
        const string K_REBOOT   = "RBT";    // reboot   complete reboot
        const string K_SAVE     = "SAVE";   // save     saves the state (to Storage)

        // The following constants are assignment states

        const string K_ERR      = "ERR";    // error    state of crashed module

        // Standard command formats
        // K_BASE : K_ACT  : source     : action : (list of data)
        // K_BASE : K_LD   : CustomName : source : (list of actions)
        // K_BASE : K_RTN  : source     : (data)
        // K_BASE : K_REL  : source
        // K_BASE : K_PING : source
        // K_BASE : K_RBT

        public void Main(string argument)
        {
            try
            {
                if (argument == null || argument == "") argument = kernel.CustomData;
                string[] lines = argument.Split('\n');
                List<string> pings = new List<string>();
                foreach (var line in lines)
                {
                    if (line == "") continue;
                    if (kernel.CustomData != null && kernel.CustomData != "")
                    {
                        kernel.CustomData = kernel.CustomData.Remove(0, line.Length + 1);
                    }
                    string[] data = line.Split(':');
                    switch (data[0])
                    {
                        case K_BASE:
                            switch (data[1])
                            {
                                case K_LOAD:        // load
                                    Load(data);
                                    break;
                                case K_ACTION:      // syscall
                                    Action(data);
                                    break;
                                case K_RETURN:      // return
                                    Return(data);
                                    break;
                                case K_UNASSIGN:    // release
                                    Unassign(data);
                                    break;
                                case K_PING:        // receive pings
                                    ReceivePing(data, pings);
                                    break;
                                default:        // do nothing!
                                    log.Append("ERROR 1: Unrecognized command: \"" + data[1] + "\"!\n");
                                    break;
                            }
                        break;
                        case K_SAVE:    // save
                            Save();
                            break;
                        case K_REBOOT:  // reboot
                            Reboot();
                            break;
                        case K_PING:    // send pings
                            SendPing();
                            break;
                        default:
                            log.Append("ERROR 0: Unrecognized/malformed command: \"" + line + "\"!\n");
                            break;
                    }
                }
                //if (kernel.CustomData != null && kernel.CustomData != "") kernel.CustomData = "";
                if (pings.Count() != 0)
                    foreach (var module in modules.Keys)
                        if (!pings.Contains(module))
                            log.Append("ERROR 14: Module unresponsive: \"" + module + "\"!\n");
                WriteLog();
            }
            catch (Exception ex)
            {
                log.Append("FATAL ERROR: Exception has occurred!\n" + ex.ToString() + '\n');
                WriteLog();
            }
        }

        // recompiles the program
        public Program()
        {
            try {
                // initialize log, time, kernel, and diagnostics panel
                log = new StringBuilder();
                kernel = GridTerminalSystem.GetBlockWithName("SEOS-Micro") as IMyProgrammableBlock;
                panel = GridTerminalSystem.GetBlockWithName("SEOS-Micro Log") as IMyTextPanel;
                panel.WritePublicText("");

                //if (Storage == null || Storage == "")
                    Boot();
                //else
                //    Configure();
                WriteLog();
            }
            catch (Exception ex)
            {
                log.Append("FATAL ERROR: Exception has occurred!\n" + ex.ToString() + '\n');
                WriteLog();
            }
        }

        // loads individual modules
        public void Load(string[] data)
        {
            // check if module already exists
            if (modules.ContainsKey(data[3]))
            {
                log.Append("ERROR 10: Cannot load module: \"" + data[3] + "\" (module already exists)!\n");
                // what to do here? Replace/update/ignore?
                // settle for replace
            }
            else
            {
                // load data into dictionaries
                modules.Add(data[3], GridTerminalSystem.GetBlockWithName(data[2]) as IMyProgrammableBlock);
                assigns.Add(data[3], null);
                if (isVerbose) log.Append("Module \"" + data[3] + "\" loading:\n");
                foreach (var action in data.Skip(4))
                {
                    // check if action name is already here
                    if (actions.ContainsKey(action))
                    {
                        log.Append("ERROR 11: Cannot attach action \"" + action + "\" to module \"" + data[3] + "\", already attached to module \"" + actions[action] + "\"!\n");
                    }
                    else
                    {
                        actions.Add(action, data[3]);
                        if (isVerbose) log.Append("Action \"" + action + "\" attached.\n");
                    }
                }
            }
        }

        // starts the reboot process
        public void Reboot()
        {
            log.Append("Rebooting...\n");
            panel.WritePublicText("");
            Storage = "";
            Boot();
        }

        // boots the system from scratch, first half
        public void Boot()
        {
            // reset dictionaries
            modules = new Dictionary<string, IMyProgrammableBlock>();
            actions = new Dictionary<string, string>();
            assigns = new Dictionary<string, string>();
            if (isVerbose) log.Append("Resetting dictionaries...\n");
            if (actions.Count != 0) log.Append("Failed!!!\n");

            // initialize & load list of modules
            List<IMyProgrammableBlock> foundModules = new List<IMyProgrammableBlock>();
            List<IMyProgrammableBlock> toRemove = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(foundModules);

            foreach (var module in foundModules)
            {
                // make "SEOS" identifying mark and check if kernel
                if (!module.CustomName.Contains(K_BASE) || module == kernel)
                    toRemove.Add(module);
                // reboot all modules
                else
                {
                    module.TryRun(K_BASE + ':' + K_REBOOT);
                    if (isVerbose) log.Append("Rebooting module at \"" + module.CustomName + "\"...\n");
                }
            }
            foreach (var module in toRemove)
                foundModules.Remove(module);
            // now wait for all modules to respond...
        }

        // saves the system to the Storage field
        public void Save()
        {
            if (isVerbose) log.Append("Saving system settings...\n");
            StringBuilder sb = new StringBuilder();
            foreach (var module in modules.Keys)
            {
                // header info: address, name
                sb.Append(K_BASE + ':' + K_LOAD + ':' + modules[module].CustomName.ToString() + ':' + module);
                // data: actions
                foreach (var action in actions.Keys)
                {
                    // running through all actions to find a subset: problematic?
                    if (actions[action] == module)
                        sb.Append(':' + action);
                }
                sb.Append('\n');
            }
            Storage = sb.ToString();
        }

        // calls Load on Storage field
        public void Configure()
        {
            // reset dictionaries
            modules = new Dictionary<string, IMyProgrammableBlock>();
            actions = new Dictionary<string, string>();
            assigns = new Dictionary<string, string>();

            // module data entered in lines in Storage
            foreach (var line in Storage.Split('\n'))
            {
                var data = line.Split(':');
                log.Append(line + '\n');
                // call Load
                Load(data);
            }
        }

        // checks and forwards system calls
        public void Action(string[] data)
        {
            // check if action is valid
            if (!actions.ContainsKey(data[3]))
            {
                log.Append("ERROR 20: Unrecognized action: \"" + data[3] + "\"!\n");
                return;
            }
            // lookup module for action
            string module = actions[data[3]];
            // check assignment status
            if (assigns[module] == null)
            {
                // serialize data
                StringBuilder sb = new StringBuilder();
                foreach (string d in data.Skip(4))
                    sb.Append(':' + d);
                // forward action
                modules[module].TryRun(K_BASE + ':' + K_ACTION + ':' + data[3] + sb.ToString());
                // assign module
                assigns[module] = data[2];
                // write log
                if (isVerbose) log.Append("Action \"" + data[3] + "\" forwarded from module \"" + data[2] + "\" to module \"" + module + "\".\n");
            }
            else
            {
                // write log
                log.Append("ERROR 21: Action forbidden: action \"" + data[3] + "\" from module \"" + data[2] + "\" to module \"" + module + "\" (module \"" + data[2] + "\" already assigned to \"" + assigns[module] + "\")!\n");
            }
        }

        // returns data from module to caller
        public void Return(string[] data)
        {
            // does caller exist?
            if (!modules.ContainsKey(data[2]))
            {
                log.Append("ERROR 12: Unrecognized module \"" + data[2] + "\"!\n");
                return;
            }
            // double check caller
            if (assigns[data[2]] == K_ERR || assigns[data[2]] == null)
            {
                log.Append("ERROR 13: module has no caller: \"" + data[2] + "\"!\n");
            }
            else
            {
                // lookup caller
                string caller = assigns[data[2]];
                // serialize data
                StringBuilder sb = new StringBuilder();
                foreach (var d in data.Skip(3))
                    sb.Append(':' + d);
                // forward data
                modules[caller].TryRun(K_BASE + ":" + K_RETURN + ":" + sb.ToString());
                // release module?
                assigns[data[2]] = null;
                // write log
                if (isVerbose) log.Append("Data returned from module \"" + data[2] + "\" to caller \"" + caller + "\".\n");
            }
        }

        // releases modules from assigns
        // Note: have modules run this when recompiled, to recover from crashes!
        public void Unassign(string[] data)
        {
            // check if module exists
            if (!modules.ContainsKey(data[2]))
            {
                log.Append("ERROR 12: unrecognized module: \"" + data[2] + "!\n");
                return;
            }
            // Module already unassigned!
            if (assigns[data[2]] == null)
            {
                log.Append("WARNING: module already unassigned: \"" + data[2] + "\"!\n");
            }
            else
            {
                // Module recovering from error
                if (assigns[data[2]] == K_ERR)
                {
                    if (isVerbose) log.Append("Module \"" + data[2] + "\" has been recompiled.\n");
                }
                // Module to be released
                else
                {
                    if (isVerbose) log.Append("Module \"" + data[2] + "\" released from caller \"" + assigns[data[2]] + "\".\n");
                }
                // release module
                assigns[data[2]] = null;
            }
        }

        // sends out ping
        public void SendPing()
        {
            if (isVerbose) log.Append("Pinging all modules...\n");
            foreach (string module in modules.Keys)
            {
                modules[module].TryRun(K_BASE + ':' + K_PING);
            }
        }

        // listens for ping from all modules
        public void ReceivePing(string[] data, List<string> pings)
        {
            // check if valid!
            if (!modules.ContainsKey(data[3]))
            {
                log.Append("ERROR 12: unrecognized module: \"" + data[3] + "\"!\n");
                return;
            }
            pings.Add(data[3]);
            if (isVerbose) log.Append("Received ping from module \"" + data[3] + "\".\n");
        }

        public void WriteLog()
        {
            if (isVerbose && log != null && log.ToString() != "")
                panel.WritePublicText(DateTime.Now.ToString() + '\n' + log.ToString() + '\n', true);
            log.Clear();
        }
    }
}