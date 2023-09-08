using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("True Welcomer", "ItzNxthaniel", "1.0.1")]
    [Description("This plugin makes it easy to welcome new users and welcome back users rejoining! Also supports Disconnect Messages.")]
    public class TrueWelcomer : CarbonPlugin
    {
        #region // Vars/Fields \\
        private static List<string> OnlinePlayers = new List<string>();
		#endregion

		#region // Config \\
		private Configuration _config;

		protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
              showJoinMsgs = true,
              showWelcomeMessages = true,
              showLeaveMsgs = true,
              clearOnWipe = true,
              steamIconID = 0
            };

            SaveConfig();
        }

        private class Configuration
        {
            [JsonProperty("Show Join Messages")]
            internal bool showJoinMsgs;

            [JsonProperty("Show Welcome Messages")]
            internal bool showWelcomeMessages;

            [JsonProperty("Show Leave Messages")]
            internal bool showLeaveMsgs;

            [JsonProperty("Clears the Data List on wipe")]
            internal bool clearOnWipe;

            [JsonProperty("Steam User Icon ID")]
            internal ulong steamIconID;
        }
        #endregion

        #region // Language \\
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["OnWelcome"] = "Welcome, <color=#ff7675>{0}</color>, to the server!",
                ["OnJoin"] = "Welcome back, <color=#ff7675>{0}</color>, to the server!",
                ["OnLeave"] = "Goodbye, <color=#ff7675>{0}</color>!"
            }, this);
        }

        private void Broadcast(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null) return;

            var message = GetMessage(messageKey, player.UserIDString, args);
            Server.Broadcast(message, null, _config.steamIconID);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }
		#endregion

		#region // Data \\
		DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile("TrueWelcomer");
		#endregion

		#region // Hooks \\
		private void Init()
        {
            permission.RegisterPermission("truewelcomer.admin", this);

            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                OnlinePlayers.Add(player.UserIDString);
            }
        }

        private void OnNewSave() {
	        if (!_config.clearOnWipe) return;
	        
	        dataFile.Clear();
	        dataFile.Save();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            string uid = player.UserIDString;
            OnlinePlayers.Remove(uid);

            Broadcast(player, "OnLeave", player.displayName);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            bool isOnline = false;
            foreach (BasePlayer p in BasePlayer.activePlayerList) {
                if (p.UserIDString == player.UserIDString) isOnline = true;
            }

			if (!OnlinePlayers.Contains(player.UserIDString) && isOnline)
			{
				OnlinePlayers.Add(player.UserIDString);
				if (dataFile[player.UserIDString] != null)
				{
					Broadcast(player, "OnJoin", player.displayName);
				}
				else
				{
					Broadcast(player, "OnWelcome", player.displayName);
					dataFile[player.UserIDString] = player.displayName;
					dataFile.Save();
				}
			}
        }

        [ConsoleCommand("tw.data_refresh")]
        private void RefreshDataCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player2 = arg.Player();
            if (player2 != null && !permission.UserHasPermission(player2.UserIDString, "truewelcomer.admin"))
            {
                arg.ReplyWith("You are lacking permission, to run this command.");
            }
    
            string msg = "";
            int num = 0;
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
				if (dataFile[player.UserIDString] != null)
				{
					num += 0;
				}
				else
				{
					dataFile[player.UserIDString] = player.displayName;
					dataFile.Save();
					num += 1;
					msg += $"Player {player.displayName} has been added.\n";
				}
				
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
				if (dataFile[player.UserIDString] != null)
				{
					num += 0;
				}
				else
				{
					dataFile[player.UserIDString] = player.displayName;
					dataFile.Save();
					num += 1;
					msg += $"Player {player.displayName} has been added.\n";
				}
				
            }

            if (num == 0)
            {
                arg.ReplyWith("No players have been added.");
                return;
            } 
            else 
            {
                string message = $"{num} players have been added to the Data List.\n{msg}";
                arg.ReplyWith(message);
            }
        }

        [ConsoleCommand("tw.data_reset")]
        private void ResetDataCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player2 = arg.Player();
            if (player2 != null && !permission.UserHasPermission(player2.UserIDString, "truewelcomer.admin"))
            {
                arg.ReplyWith("You are lacking permission, to run this command.");
            }

            dataFile.Clear();
			dataFile.Save();
            arg.ReplyWith("The Data File for True Welcomer has been reset!");
        }
        #endregion
    }
}
