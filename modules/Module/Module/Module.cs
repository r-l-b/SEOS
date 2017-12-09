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

        const string K_BASE     = "SEOS";
        const string K_ACTION   = "ACT";
        const string K_RETURN   = "RTN";
        const string K_UNASSIGN = "REL";
        const string K_LOAD     = "LD";
        const string K_PING     = "PING";
        const string K_REBOOT   = "RBT";
        
        IMyTerminalBlock micro;
        IMyTimerBlock timer;
        
        const string M_LABEL    = "Module 1";

        Dictionary<string, Action<string>> actions;

        public void Release()
        {
            micro.CustomData += K_BASE + ':' + K_UNASSIGN + ':' + M_LABEL + '\n';
            timer.Trigger();
        }

        public void Return(string data)
        {
            micro.CustomData += K_BASE + ':' + K_RETURN + ':' + M_LABEL + ':' + data + '\n';
            timer.Trigger();
        }

        public void Ping()
        {
            micro.CustomData += K_BASE + ':' + K_PING + ':' + M_LABEL + '\n';
            timer.Trigger();
        }

        public void Reboot()
        {
            string data = "";
            foreach (var pair in actions)
                data += ':' + pair.Key;
            micro.CustomData += K_BASE + ':' + K_LOAD + ':' + (K_BASE + ' ' + M_LABEL) + ':' + M_LABEL + data + '\n';
            timer.Trigger();
        }

        // really basic module
        // one text panel
        // Commands:
        //      read
        //      write
        //      copy (read from other module)
        //      transmit (write to other module)

        const string M_READ = "read_1'";
        const string M_WRITE = "write_1";

        IMyTextPanel panel;

        public Program()
        {
            panel = GridTerminalSystem.GetBlockWithName(M_LABEL + " Panel") as IMyTextPanel;
            micro = GridTerminalSystem.GetBlockWithName("SEOS-Micro");
            timer = GridTerminalSystem.GetBlockWithName("SEOS-Timer") as IMyTimerBlock;

            actions = new Dictionary<string, Action<string>>();
            actions.Add(M_READ, Read);
            actions.Add(M_WRITE, Write);
        }

        public void Save()
        {
            
        }

        public void Main(string argument)
        {
            try
            {
                string[] data = argument.Split(':');
                switch (data[0])
                {
                    case K_BASE:
                        switch (data[1])
                        {
                            case K_ACTION:
                                actions[data[2]](data[3]);
                                break;
                            case K_RETURN:
                                StringBuilder sb = new StringBuilder();
                                foreach (var d in data.Skip(3))
                                    sb.Append(':' + d);
                                panel.WritePublicText(sb.ToString().Replace("\\n", "\n"));
                                break;
                            case K_PING:
                                Ping();
                                break;
                            case K_REBOOT:
                                Reboot();
                                break;
                            default:
                                break;
                        }
                    break;
                    case "TRANSMIT":
                        Transmit(data[1]);
                        break;
                    case "COPY":
                        Copy();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                panel.WritePublicText("Exception occurred:\n" + ex.ToString());
            }
        }

        public void Read(string data = null)
        {
            data = panel.GetPublicText().Replace("\n", "\\n");
            Return(data);
        }

        public void Write(string data)
        {
            panel.WritePublicText(data.Replace("\\n","\n"));
            Release();
        }

        public void Copy()
        {
            micro.CustomData += K_BASE + ':' + K_ACTION + ':' + M_LABEL + ':' + "read_2" + ':' + ' ' + '\n';
            timer.Trigger();
        }

        public void Transmit(string data)
        {
            micro.CustomData += K_BASE + ':' + K_ACTION + ':' + M_LABEL + ':' + "write_2" + ':' + data + '\n';
            timer.Trigger();
        }
    }
}