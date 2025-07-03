using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins {
    using Core.Libraries;
    using Newtonsoft.Json.Linq;

    [Info("True Welcomer", "ItzNxthaniel", "1.2.0")]
    [Description("This plugin makes it easy to welcome new users and welcome back users rejoining! Also supports Disconnect Messages.")]
    public class TrueWelcomer : RustPlugin {

        #region Vars/Fields

        private static List<string> _onlinePlayers = new List<string>();

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
                DebugMode = false,
                ShowJoinMsgs = true,
                ShowWelcomeMessages = true, 
                ShowLeaveMsgs = true,
                ClearOnWipe = true,
                SteamIconID = 0,
                HidePlayersWithAuthlevel = false,
                AuthLevelToHide = 0,
                HidePlayersWithPermission = false,
                PrivateMessages = false,
                ShowCountryCode = false
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
            internal bool DebugMode;

            [JsonProperty("Show Join Messages")]
            internal bool ShowJoinMsgs;

            [JsonProperty("Show Welcome Messages")]
            internal bool ShowWelcomeMessages;

            [JsonProperty("Show Leave Messages")]
            internal bool ShowLeaveMsgs;

            [JsonProperty("Clears the Data List on wipe")]
            internal bool ClearOnWipe;

            [JsonProperty("Steam User Icon ID")]
            internal ulong SteamIconID;

            [JsonProperty("Hide Players with AuthLevel")]
            internal bool HidePlayersWithAuthlevel;

            [JsonProperty("AuthLevel to Hide. 0 - Both, 1 - AuthLevel1, 2 - AuthLevel2 ")]
            internal AuthLevelEnum AuthLevelToHide;

            [JsonProperty("Hide Players With Permission")]
            internal bool HidePlayersWithPermission;
            
            [JsonProperty("Private Messages Only - Disables Leave Messages by Default")]
            internal bool PrivateMessages;

            [JsonProperty("Show Country Code - If Disabled does not make any API Calls.")]
            internal bool ShowCountryCode;
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
            if (_config.DebugMode) Puts(message);
            if (!_config.PrivateMessages) {
                GetCountryCode(player, countryCode => {
                    Server.Broadcast(message, countryCode, _config.SteamIconID);    
                });
            } else {
                if (messageKey.Equals("OnLeave")) return;

                player.ChatMessage(message);
            }
        }

        private string GetMessage(string key, string id, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        private void GetCountryCode(BasePlayer player, Action<string> callback) {
            if (!_config.ShowCountryCode) {
                callback(null);
                return;
            }

            var ip = player.Connection.IPAddressWithoutPort();
            if (string.IsNullOrEmpty(ip)) {
                callback(null);
                return;
            }

            var url = $"http://ip-api.com/json/{ip}?fields=status,message,countryCode";
            webrequest.Enqueue(url, null, (code, response) => {
                if (code != 200 || string.IsNullOrWhiteSpace(response)) {
                    Puts($"Country request failed. Code: {code}, Response: {response}");
                    callback(null);
                    return;
                }

                try {
                    var json = JObject.Parse(response);
                    var countryCode = $"[{json.GetValue("countryCode")}]";
                    callback(countryCode);
                } catch (Exception ex) {
                    Puts($"Error parsing JSON: {ex}");
                    callback(null);
                }
            }, this);
        }
        #endregion

        #region Data
        private DynamicConfigFile _cachedPlayers = Interface.Oxide.DataFileSystem.GetDatafile("TrueWelcomer");
        #endregion

        #region Hooks
        private void Init() {
            permission.RegisterPermission("truewelcomer.admin", this);
            permission.RegisterPermission("truewelcomer.canSetPreference", this);
            permission.RegisterPermission("truewelcomer.hideUser", this);

            foreach (BasePlayer player in BasePlayer.activePlayerList) {
                _onlinePlayers.Add(player.UserIDString);
            }
        }

        private void OnNewSave() {
            if (!_config.ClearOnWipe) return;

            _cachedPlayers.Clear();
            _cachedPlayers.Save();
        }

        private bool ShouldAnnounce(BasePlayer player) { 
            if (_config.HidePlayersWithPermission && permission.UserHasPermission(player.UserIDString, "truewelcomer.hideUser")) return false;
            if (!_config.HidePlayersWithAuthlevel) return true;
            ServerUsers.User user = ServerUsers.Get(player.userID);

            switch(_config.AuthLevelToHide) {
                case AuthLevelEnum.Both when user.group is ServerUsers.UserGroup.Moderator or ServerUsers.UserGroup.Owner:
                case AuthLevelEnum.AuthLevel1 when user.group is ServerUsers.UserGroup.Moderator:
                case AuthLevelEnum.AuthLevel2 when user.group is ServerUsers.UserGroup.Owner: return false;
                default: return true;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player) {
            if (player == null) return;
            
            _onlinePlayers.Remove(player.UserIDString);

            if (_config.ShowLeaveMsgs && ShouldAnnounce(player)) 
                Broadcast(player, "OnLeave", player.displayName);
        }

        private void OnPlayerSleepEnded(BasePlayer player) {
            if (player == null ) return;
            if (_onlinePlayers.Contains(player.UserIDString)) return;
            
            bool isOnline = false;
            foreach (BasePlayer p in BasePlayer.activePlayerList) {
                if (p.UserIDString == player.UserIDString) isOnline = true;
            }

            if (isOnline) {
                var keyToUse = "";
                if (_config.ShowJoinMsgs) keyToUse = "OnJoin"; 
                
                _onlinePlayers.Add(player.UserIDString);
                if (_cachedPlayers[player.UserIDString] == null ) {
                    if (_config.ShowWelcomeMessages) keyToUse = "OnWelcome";
                    
                    _cachedPlayers[player.UserIDString] = player.displayName;
                    _cachedPlayers.Save();
                }
                
                if (!string.IsNullOrEmpty(keyToUse) && ShouldAnnounce(player))
                    Broadcast(player, keyToUse, player.displayName);
            }
        }

        #endregion

        #region Commands

        [Command("hidewelcome")] 
        private void HideWelcomeCommand(BasePlayer player) {
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


            finalMsg += _config.HidePlayersWithPermission ? "" : $"\n{GetMessage("ServerConfigAlert", player.UserIDString)}";
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
                if (_cachedPlayers[player.UserIDString] != null) num += 0;
                else {
                    _cachedPlayers[player.UserIDString] = player.displayName;
                    _cachedPlayers.Save();
                    num += 1;
                    msg += $"Player {player.displayName} has been added.\n";
                }
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList) {
                if (_cachedPlayers[player.UserIDString] != null) num += 0;
                else {
                    _cachedPlayers[player.UserIDString] = player.displayName;
                    _cachedPlayers.Save();
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

            _cachedPlayers.Clear();
            _cachedPlayers.Save();
            arg.ReplyWith("The Data File for True Welcomer has been reset!");
        }

        #endregion

    }
}
