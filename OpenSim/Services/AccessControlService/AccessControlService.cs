using OpenSim.Services.Interfaces;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Framework;
using System;
using System.Linq;

namespace OpenSim.Services.AccessControlService
{
    public class AccessControlService :
            AccessControlServiceBase, IAccessControlService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static bool Initialized = false;

        public AccessControlService(IConfigSource config) :
                base(config)
        {
            if (!Initialized)
            {
                Initialized = true;
                RegisterCommands();
            }

            m_log.Info("[ACCESS] Access service started!");
        }
        public bool OnlyHexInString(string test)
        {
            // For C-style hex notation (0xFF) you can use @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z"
            return System.Text.RegularExpressions.Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z");
        }
        public bool ValidateIPv4(string ipString)
        {
            if (String.IsNullOrWhiteSpace(ipString))
            {
                return false;
            }

            string[] splitValues = ipString.Split('.');
            if (splitValues.Length != 4)
            {
                return false;
            }

            byte tempForParsing;

            return splitValues.All(r => byte.TryParse(r, out tempForParsing));
        }

        #region Console Commands
        private void RegisterCommands()
        {
            MainConsole.Instance.Commands.AddCommand("Access", false, "ban mac",
                    "ban mac <mac_hash>",
                    "Adds the supplied mac address to the banned macs list.", HandleBanMacCommand);

            MainConsole.Instance.Commands.AddCommand("Access", false, "unban mac",
                    "unban mac <mac_hash>",
                    "Removes the supplied mac from the banned macs list.", HandleUnBanMacCommand);

            MainConsole.Instance.Commands.AddCommand("Access", false, "ban id0",
                    "ban id0 <id0_hash>",
                    "Adds the supplied id0 to the banned id0s list.", HandleBanID0Command);

            MainConsole.Instance.Commands.AddCommand("Access", false, "unban id0",
                    "unban id0 <id0_hash>",
                    "Removes the supplied id0 from the banned id0s list.", HandleUnBanID0Command);

            MainConsole.Instance.Commands.AddCommand("Access", false, "ban ip",
                    "ban ip <ip_address>",
                    "Adds the supplied IP Address to the banned IPs list.", HandleBanIPCommand);

            MainConsole.Instance.Commands.AddCommand("Access", false, "unban ip",
                    "unban ip <ip_address>",
                    "Removes the supplied IP Address from the banned IPs list.", HandleUnBanIPCommand);
        }

        private void HandleBanIPCommand(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string ip = cmd[2];
                if (ValidateIPv4(ip))
                {
                    m_log.InfoFormat("Banning {0}", ip);
                    m_Database.BanIPAddress(ip);
                }
                else m_log.Info("Invalid IP Address!");
            }
            else m_log.Info("[ACCESS] No IP Address supplied!");
        }

        private void HandleUnBanIPCommand(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string ip = cmd[2];
                if (ValidateIPv4(ip))
                {
                    m_log.InfoFormat("Unbanning {0}", ip);
                    m_Database.UnbanIPAddress(ip);
                }
                else m_log.Info("Invalid IP Address!");
            }
            else m_log.Info("[ACCESS] No IP Address supplied!");
        }

        private void HandleBanMacCommand(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string mac = cmd[2];
                if (OnlyHexInString(mac) && mac.Length == 32)
                {
                    m_log.InfoFormat("Banning {0}", mac);
                    m_Database.BanMacAddress(mac);
                }
                else m_log.Info("Invalid Mac hash!");
            }
            else m_log.Info("[ACCESS] No Mac Address supplied!");
        }

        private void HandleUnBanMacCommand(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string mac = cmd[2];
                if(OnlyHexInString(mac) && mac.Length == 32)
                {
                    m_log.InfoFormat("Unbanning {0}", mac);
                    m_Database.UnbanMacAddress(mac);
                }
                else m_log.Info("Invalid Mac hash!");
            }
            else m_log.Info("[ACCESS] No Mac Address supplied!");
        }

        private void HandleBanID0Command(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string id0 = cmd[2];
                if (OnlyHexInString(id0) && id0.Length == 32)
                {
                    m_log.InfoFormat("Banning {0}", id0);
                    m_Database.BanID0(id0);
                }
                else m_log.Info("Invalid ID0 hash!");
            }
            else m_log.Info("[ACCESS] No ID0 supplied!");
        }

        private void HandleUnBanID0Command(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                string id0 = cmd[2];
                if (OnlyHexInString(id0) && id0.Length == 32)
                {
                    m_log.InfoFormat("Unbanning {0}", id0);
                    m_Database.UnbanID0(id0);
                }
                else m_log.Info("Invalid ID0 hash!");
            }
            else m_log.Info("[ACCESS] No ID0 supplied!");
        }
        #endregion
    }
}
