using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections.Generic;

namespace Oxide.Plugins {

    [Info("True Welcomer", "ItzNxthaniel", "1.1.0")]
    [Description("This plugin makes it easy to welcome new users and welcome back users rejoining! Also supports Disconnect Messages.")]
    public class TrueWelcomer : RustPlugin {

        #region Vars/Fields

        private static List<string> OnlinePlayers = new List<string>();

        #endregion

        #region Config

        private Configuration _config;

        protected override void LoadConfig() {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void SaveConfig() =>
            Config.WriteObject(_config);

        protected override void LoadDefaultConfig() {
            _config = new Configuration {
                debugMode = false,
                showJoinMsgs = true,
                showWelcomeMessages = true, showLeaveMsgs = true,
                clearOnWipe = true,
                steamIconID = 0,
                hidePlayersWithAuthlevel = false,
                authLevelToHide = 0,
                hidePlayersWithPermission = false
            };

            SaveConfig();
        }


        private enum AuthLevelEnum {
            Both = 0,
            AuthLevel1 = 1,
            AuthLevel2 = 2
        }

        private class Configuration {
            [JsonProperty("Debug Mode")]
            internal bool debugMode;

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

            [JsonProperty("Hide Players with AuthLevel")]
            internal bool hidePlayersWithAuthlevel;

            [JsonProperty("AuthLevel to Hide. 0 - Both, 1 - AuthLevel1, 2 - AuthLevel2 ")]
            internal AuthLevelEnum authLevelToHide;

            [JsonProperty("Hide Players With Permission")]
            internal bool hidePlayersWithPermission;
        }

        #endregion

        #region Language

        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                                      ["OnWelcome"] = "Welcome, <color=#ff7675>{0}</color>, to the server!",
                                      ["OnJoin"] = "Welcome back, <color=#ff7675>{0}</color>, to the server!",
                                      ["OnLeave"] = "Goodbye, <color=#ff7675>{0}</color>!",
                                      ["NoPermission"] = "You do not have permission to run this command!",
                                      ["NowHiding"] = "Users will no longer be alerted when you join or leave the server.",
                                      ["NowShowing"] = "Users will now be alerted when you join or leave the server.",
                                      ["ServerConfigAlert"] = "Due to the Server's Config, your preference will be ignored."
                                  },
                                  this);
        }

        private void Broadcast(BasePlayer player, string messageKey, params object[] args) {
            if (player == null) return;

            var message = GetMessage(messageKey, player.UserIDString, args);
            if (_config.debugMode) Puts(message);
            Server.Broadcast(message, null, _config.steamIconID);
        }

        private string GetMessage(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Data
        private DynamicConfigFile cachedPlayers = Interface.Oxide.DataFileSystem.GetDatafile("TrueWelcomer");
        #endregion

        #region Hooks

        private void Init() {
            permission.RegisterPermission("truewelcomer.admin", this);
            permission.RegisterPermission("truewelcomer.canSetPreference", this);
            permission.RegisterPermission("truewelcomer.hideUser", this);

            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                OnlinePlayers.Add(player.UserIDString);
            }
        }

        private void OnNewSave() {
            if (!_config.clearOnWipe) return;

            cachedPlayers.Clear();
            cachedPlayers.Save();
        }

        private bool ShouldAnnounce(BasePlayer player) { 
            if (_config.hidePlayersWithPermission && permission.UserHasPermission(player.UserIDString, "truewelcomer.hideUser")) return false;
            if (!_config.hidePlayersWithAuthlevel) return true;
            ServerUsers.User user = ServerUsers.Get(player.userID);

            switch(_config.authLevelToHide) {
                case AuthLevelEnum.Both when user.group is ServerUsers.UserGroup.Moderator or ServerUsers.UserGroup.Owner:
                case AuthLevelEnum.AuthLevel1 when user.group is ServerUsers.UserGroup.Moderator:
                case AuthLevelEnum.AuthLevel2 when user.group is ServerUsers.UserGroup.Owner: return false;
                default: return true;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player) {
            string uid = player.UserIDString;
            OnlinePlayers.Remove(uid);

            switch(ShouldAnnounce(player)) {
                case true:
                    Broadcast(player, "OnLeave", player.displayName);
                    break;
                default: return;
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player) {
            bool isOnline = false;
            foreach (BasePlayer p in BasePlayer.activePlayerList) {
                if (p.UserIDString == player.UserIDString) isOnline = true;
            }

            string keyToUse = "OnJoin";

            if (!OnlinePlayers.Contains(player.UserIDString) && isOnline) {
                OnlinePlayers.Add(player.UserIDString);
                if (cachedPlayers[player.UserIDString] != null) keyToUse = "OnJoin";
                else {
                    keyToUse = "OnWelcome";
                    cachedPlayers[player.UserIDString] = player.displayName;
                    cachedPlayers.Save();
                }
            }

            switch(ShouldAnnounce(player)) {
                case true:
                    Broadcast(player, keyToUse, player.displayName);
                    break;
                default: return;
            }
        }

        [Command("hidewelcome")] private void HideWelcomeCommand(BasePlayer player) {
            if (!permission.UserHasPermission(player.UserIDString, "truewelcomer.canSetPreference")) {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                return;
            }

            string finalMsg;

            if (permission.UserHasPermission(player.UserIDString, "truewelcomer.hideUser")) {
                permission.RevokeUserPermission(player.UserIDString, "truewelcomer.hideUser");
                finalMsg = GetMessage("NowShowing", player.UserIDString);
            } else {
                permission.GrantUserPermission(player.UserIDString, "truewelcomer.hideUser", this);
                finalMsg = GetMessage("NowHiding", player.UserIDString);
            }


            finalMsg += _config.hidePlayersWithPermission ? "" : $"\n{GetMessage("ServerConfigAlert", player.UserIDString)}";
            player.ChatMessage(finalMsg);
        }

        [ConsoleCommand("tw.data_refresh")] 
        private void RefreshDataCommand(ConsoleSystem.Arg arg) {
            BasePlayer player2 = arg.Player();
            if (player2 != null && !permission.UserHasPermission(player2.UserIDString, "truewelcomer.admin")) {
                arg.ReplyWith("You are lacking permission, to run this command.");
                return;
            }

            string msg = "";
            int num = 0;
            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                if (cachedPlayers[player.UserIDString] != null) num += 0;
                else {
                    cachedPlayers[player.UserIDString] = player.displayName;
                    cachedPlayers.Save();
                    num += 1;
                    msg += $"Player {player.displayName} has been added.\n";
                }
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList) {
                if (cachedPlayers[player.UserIDString] != null) num += 0;
                else {
                    cachedPlayers[player.UserIDString] = player.displayName;
                    cachedPlayers.Save();
                    num += 1;
                    msg += $"Player {player.displayName} has been added.\n";
                }
            }

            if (num == 0) arg.ReplyWith("No players have been added.");
            else {
                string message = $"{num} players have been added to the Data List.\n{msg}";
                arg.ReplyWith(message);
            }
        }

        [ConsoleCommand("tw.data_reset")] 
        private void ResetDataCommand(ConsoleSystem.Arg arg) {
            BasePlayer player2 = arg.Player();
            if (player2 != null && !permission.UserHasPermission(player2.UserIDString, "truewelcomer.admin")) {
                arg.ReplyWith("You are lacking permission, to run this command.");
                return;
            }

            cachedPlayers.Clear();
            cachedPlayers.Save();
            arg.ReplyWith("The Data File for True Welcomer has been reset!");
        }

        #endregion

    }
}
