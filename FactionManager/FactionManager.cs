/* FactionManager



*/

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Web;
using System.Linq;


namespace PRoConEvents
{

    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;
    //Aliases
    using EventType = PRoCon.Core.Events.EventType;

    public class FactionManager : PRoConPluginAPI, IPRoConPluginInterface
    {
        string PluginVersion = "0.1.0.0";

        bool pluginEnabled;
        bool restrictCommandsToMe;
        bool preventRepeatedFactions;

        int rotationIndex;
        string setRotationIndex = "";

        public enum MessageType { Warning, Error, Exception, Normal };
        public enum SquadStatus { OK, Locked, Full, NoSquad };
        public enum Color { Black, Maroon, Green, Orange, DarkBlue, LightBlue, Violet, Pink, Red, Gray };
        public enum Format { Bold, Normal, Italicized };
        public enum DebugType
        {
            Disabled, MapInfo, MapEvents, MapEventHandler, MapList, ServerInfo, EventFirings, Factions
        };
        public enum Mode { Rotation, Randomize };

        Mode operatingMode = Mode.Randomize;

        List<DebugType> debugList = new List<DebugType>() { DebugType.Disabled, DebugType.Disabled, DebugType.Disabled, DebugType.Disabled };
        List<string> factionRotationList = new List<string>();
        Dictionary<string, int> gameModes = new Dictionary<string, int>();
        Dictionary<int, string> factions = new Dictionary<int, string>();

        EventWaitHandle mapListHandle;
        ServerInfo server;
        Random randFaction = new Random(Environment.TickCount);

        public FactionManager()
        {
            server = new ServerInfo(this);
            preventRepeatedFactions = false;
            pluginEnabled = false;

            rotationIndex = 0;

            gameModes.Add("ConquestLarge0", 2);
            gameModes.Add("ConquestSmall0", 2);
            gameModes.Add("Domination0", 2);
            gameModes.Add("Elimination0", 2);
            gameModes.Add("Obliteration", 2);
            gameModes.Add("RushLarge0", 2);
            gameModes.Add("SquadDeathMatch0", 4);
            gameModes.Add("SquadDeathMatch1", 4);
            gameModes.Add("TeamDeathMatch0", 2);
            gameModes.Add("AirSuperiority0", 2);
            gameModes.Add("CarrierAssaultLarge0", 2);
            gameModes.Add("CarrierAssaultSmall0", 2);
            gameModes.Add("CaptureTheFlag0", 2);
            gameModes.Add("Chainlink0", 2);
            gameModes.Add("SquadObliteration0", 2);
            gameModes.Add("GunMaster0", 2);
            gameModes.Add("GunMaster1", 2);

            factions.Add(0, "US");
            factions.Add(1, "RU");
            factions.Add(2, "CN");

            factionRotationList.Add("US, US");
            factionRotationList.Add("RU, RU");
            factionRotationList.Add("CN, CN");
        }


        #region Details tab details
        public string GetPluginName()
        {
            return "FactionManager";
        }

        public string GetPluginVersion()
        {
            return PluginVersion;
        }

        public string GetPluginAuthor()
        {
            return "S1ngular1ty";
        }

        public string GetPluginWebsite()
        {
            return "TBD";
        }

        public string GetPluginDescription()
        {
            return @"
<h1>THIS PLUGIN IS NOT READY FOR USE YET!</h1>
<p>TBD</p>

<h2>Description</h2>
<p>TBD</p>

<h2>Commands</h2>
<p>TBD</p>

<h2>Settings</h2>
<p>TBD</p>

<h2>Development</h2>
<p>TBD</p>
<h3>Changelog</h3>";
        }
        #endregion


        #region Plugin variables

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            var debug_var_type = "enum.DebugType" + "(" + String.Join("|", Enum.GetNames(typeof(DebugType))) + ")";
            var mode_var_type = "enum.Mode" + "(" + String.Join("|", Enum.GetNames(typeof(Mode))) + ")";

            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("FactionManager|Debug type 1", debug_var_type, debugList[0].ToString()));
            lstReturn.Add(new CPluginVariable("FactionManager|Debug type 2", debug_var_type, debugList[1].ToString()));
            lstReturn.Add(new CPluginVariable("FactionManager|Debug type 3", debug_var_type, debugList[2].ToString()));
            lstReturn.Add(new CPluginVariable("FactionManager|Debug type 4", debug_var_type, debugList[3].ToString()));
            lstReturn.Add(new CPluginVariable("FactionManager|Operating Mode", mode_var_type, operatingMode.ToString()));

            if (operatingMode == Mode.Rotation)
            {
                lstReturn.Add(new CPluginVariable("FactionManager|Faction Rotation List", typeof(string[]), factionRotationList.ToArray()));
                lstReturn.Add(new CPluginVariable("FactionManager|Set Rotation List Index (integer)", setRotationIndex.GetType(), setRotationIndex));

            }
            else if (operatingMode == Mode.Randomize)
            {
                lstReturn.Add(new CPluginVariable("FactionManager|Prevent Faction Repeats?", preventRepeatedFactions.GetType(), preventRepeatedFactions));
            }

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            var debug_var_type = "enum.DebugType" + "(" + String.Join("|", Enum.GetNames(typeof(DebugType))) + ")";
            var mode_var_type = "enum.Mode" + "(" + String.Join("|", Enum.GetNames(typeof(Mode))) + ")";

            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Debug type 1", debug_var_type, debugList[0].ToString()));
            lstReturn.Add(new CPluginVariable("Debug type 2", debug_var_type, debugList[1].ToString()));
            lstReturn.Add(new CPluginVariable("Debug type 3", debug_var_type, debugList[2].ToString()));
            lstReturn.Add(new CPluginVariable("Debug type 4", debug_var_type, debugList[3].ToString()));
            lstReturn.Add(new CPluginVariable("Operating Mode", mode_var_type, operatingMode.ToString()));
            lstReturn.Add(new CPluginVariable("Set Rotation List Index (integer)", setRotationIndex.GetType(), setRotationIndex));
            lstReturn.Add(new CPluginVariable("Faction Rotation List", typeof(string[]), factionRotationList.ToArray()));
            lstReturn.Add(new CPluginVariable("Prevent Faction Repeats?", preventRepeatedFactions.GetType(), preventRepeatedFactions));

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {

            if (strVariable.CompareTo("Debug type 1") == 0)
            {
                var value = (DebugType)Enum.Parse(typeof(DebugType), strValue);
                debugList[0] = value;
            }
            else if (strVariable.CompareTo("Debug type 2") == 0)
            {
                var value = (DebugType)Enum.Parse(typeof(DebugType), strValue);
                debugList[1] = value;
            }
            else if (strVariable.CompareTo("Debug type 3") == 0)
            {
                var value = (DebugType)Enum.Parse(typeof(DebugType), strValue);
                debugList[2] = value;
            }
            else if (strVariable.CompareTo("Debug type 4") == 0)
            {
                var value = (DebugType)Enum.Parse(typeof(DebugType), strValue);
                debugList[3] = value;
            }
            else if (strVariable.CompareTo("Restrict comamnds for testing") == 0)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                restrictCommandsToMe = tmp;
            }
            else if (strVariable.CompareTo("Prevent Faction Repeats?") == 0)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                preventRepeatedFactions = tmp;
            }
            else if (strVariable.CompareTo("Faction Rotation List") == 0)
            {
                var values = CPluginVariable.DecodeStringArray(strValue);
                factionRotationList.Clear();
                int entry = 1;

                DebugWrite(DebugType.Factions, FormatText("*** Loading Faction Rotation List ***", Color.Green, Format.Bold));
                foreach (var value in values)
                {
                    var tmpFactions = value.Split(',').Select(x => x.Trim()).ToList();
                    bool lineItemOK = true;

                    foreach (var item in tmpFactions)
                    {
                        lineItemOK = true;
                        if (!factions.Any(x => x.Value == item))
                        {
                            ConsoleWarn("Line " + entry + " of Faction Rotation List.  Faction " + item
                                + " is not valid.  Please enter a correct faction name.");
                            lineItemOK = false;
                        }
                    }

                    factionRotationList.Add(value.Trim());
                    if (lineItemOK)
                        DebugWrite(DebugType.Factions, "Successfully added line " + entry + " of Rotation List: " + value.Trim());

                    entry++;
                }

                if (rotationIndex > factionRotationList.Count)
                    rotationIndex = 0;
            }
            else if (strVariable.CompareTo("Set Rotation List Index (integer)") == 0)
            {
                int tmp;
                int.TryParse(strValue, out tmp);

                if (tmp > (factionRotationList.Count - 1))
                    rotationIndex = factionRotationList.Count - 1;
                else if (tmp <= 0)
                    rotationIndex = 0;
                else
                    rotationIndex = tmp;

                ConsoleWrite(FormatText("Rotation List Index set to " + rotationIndex + " : " + factionRotationList[rotationIndex], Color.LightBlue, Format.Bold));
            }
            else if (strVariable.CompareTo("Operating Mode") == 0)
            {
                var value = (Mode)Enum.Parse(typeof(Mode), strValue);
                operatingMode = value;

                if(operatingMode == Mode.Randomize)
                    ConsoleWrite(FormatText("Faction Randomizer is Enabled - Rotation List is Disabled", Color.Violet, Format.Bold));
                if (operatingMode == Mode.Rotation)
                    ConsoleWrite(FormatText("Faction Rotation List is Enabled - Randomizer is Disabled", Color.DarkBlue, Format.Bold));
            }

        }

        #endregion

        #region Communication Methods

        public string FormatText(string msg, Color color, Format format)
        {
            string MSG = "";
            switch (color)
            {
                case Color.Black:
                    MSG = "^0" + msg + "^n";
                    break;
                case Color.Maroon:
                    MSG = "^1" + msg + "^n";
                    break;
                case Color.Green:
                    MSG = "^2" + msg + "^n";
                    break;
                case Color.Orange:
                    MSG = "^3" + msg + "^n";
                    break;
                case Color.DarkBlue:
                    MSG = "^4" + msg + "^n";
                    break;
                case Color.LightBlue:
                    MSG = "^5" + msg + "^n";
                    break;
                case Color.Violet:
                    MSG = "^6" + msg + "^n";
                    break;
                case Color.Pink:
                    MSG = "^7" + msg + "^n";
                    break;
                case Color.Red:
                    MSG = "^8" + msg + "^n";
                    break;
                case Color.Gray:
                    MSG = "^9" + msg + "^n";
                    break;
                default:
                    break;
            }

            switch (format)
            {
                case Format.Bold:
                    MSG = "^b" + MSG;
                    break;
                case Format.Normal:
                    break;
                case Format.Italicized:
                    MSG = "^i" + MSG;
                    break;
                default:
                    break;
            }

            return MSG;
        }

        public string FormatMessage(string msg, MessageType type)
        {
            String prefix = "[^bFactionManager!^n] ";

            if (type.Equals(MessageType.Warning))
                prefix += FormatText("WARNING ", Color.Orange, Format.Bold);
            else if (type.Equals(MessageType.Error))
                prefix += FormatText("ERROR ", Color.Maroon, Format.Bold);
            else if (type.Equals(MessageType.Exception))
                prefix += FormatText("EXCEPTION ", Color.Red, Format.Bold);

            return prefix + msg;
        }

        public void ChatWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.chat.write", msg);
        }

        public void LogWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(string msg, MessageType type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, MessageType.Normal);
        }

        public void ConsoleWarn(string msg)
        {
            ConsoleWrite(msg, MessageType.Warning);
        }

        public void ConsoleError(string msg)
        {
            ConsoleWrite(msg, MessageType.Error);
        }

        public void ConsoleException(string msg)
        {
            ConsoleWrite(msg, MessageType.Exception);
        }

        public void DebugWrite(DebugType type, string msg)
        {
            if (debugList.Contains(type))
                ConsoleWrite(msg, MessageType.Normal);
        }

        public void SendMessage(string message, string responseScope, string speaker)
        {
            if (responseScope == "!")
                SendGlobalMessage(message);
            else if (responseScope == "@")
                SendPlayerMessage(message, speaker);
        }

        public void SendGlobalMessage(string message)
        {
            ServerCommand("admin.say", message, "all");
            ChatWrite(message);
        }

        public void SendPlayerMessage(string message, string player)
        {
            ServerCommand("admin.say", message, "player", player);
            ChatWrite("(PlayerSay: " + player + ") " + message);
        }

        public void SendYell(string message, int duration)
        {
            ServerCommand("admin.yell", message, duration.ToString(), "all");
            ChatWrite("(Yell) " + message);
        }

        public void SendPlayerYell(string message, string player, int duration)
        {
            ServerCommand("admin.yell", message, duration.ToString(), "player", player);
            ChatWrite("(PlayerYell: " + player + ") " + message);
        }

        public void ServerCommand(params string[] args)
        {
            List<string> list = new List<string>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }

        #endregion

        // modified algorithm to ignore insertions, and case
        public string BestMatch(string name, List<string> names, out int best_distance)
        {
            best_distance = int.MaxValue;

            //do the obvious check first
            if (names.Contains(name))
            {
                best_distance = 0;
                return name;
            }

            //name is not in the list, find the best match
            string best_match = null;

            // first try to see if any of the names contains target name as substring, so we can reduce the search
            Dictionary<string, string> sub_names = new Dictionary<string, string>();

            string name_lower = name.ToLower();

            for (int i = 0; i < names.Count; i++)
            {
                string cname = names[i].ToLower();
                if (cname.Equals(name_lower))
                    return names[i];
                else if (cname.Contains(name_lower) && !sub_names.ContainsKey(cname))
                    sub_names.Add(cname, names[i]);
            }

            if (sub_names.Count > 0)
                names = new List<string>(sub_names.Keys);

            if (sub_names.Count == 1)
            {
                // we can optimize, and exit early
                best_match = sub_names[names[0]];
                best_distance = Math.Abs(best_match.Length - name.Length);
                return best_match;
            }


            // find the best/fuzzy match using modified Leveshtein algorithm              
            foreach (string cname in names)
            {
                int distance = LevenshteinDistance(name, cname);
                if (distance < best_distance)
                {
                    best_distance = distance;
                    best_match = cname;
                }
            }


            if (best_match == null)
                return null;

            best_distance += Math.Abs(name.Length - best_match.Length);

            // if we searched through sub-names, get the actual match
            if (sub_names.Count > 0 && sub_names.ContainsKey(best_match))
                best_match = sub_names[best_match];

            return best_match;
        }

        public int LevenshteinDistance(string s, string t)
        {
            s = s.ToLower();
            t = t.ToLower();

            int n = s.Length;
            int m = t.Length;

            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
                return m;

            if (m == 0)
                return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 0), d[i - 1, j - 1] + ((t[j - 1] == s[i - 1]) ? 0 : 1));

            return d[n, m];
        }

        public void SetFaction(string teamID, int faction)
        {
            ServerCommand("vars.teamFactionOverride", teamID, faction.ToString());
        }

        public void RandomizeFactions()
        {
            LogWrite(FormatText("Randomizing Factions", Color.Violet, Format.Bold));
            var tmpFactions = server.factions;

            for (int i = 0; i < 4; i++)
            {
                var newFaction = randFaction.Next(0, 3);
                if (preventRepeatedFactions && tmpFactions[i] == newFaction)
                {
                    LogWrite("Detected repeated faction - randomizing faction again.");
                    while (tmpFactions[i] == newFaction)
                    {
                        newFaction = randFaction.Next(0, 3);
                    }
                }
                var teamID = i + 1;
                LogWrite("Setting team " + teamID + " faction to: " + newFaction);
                SetFaction(teamID.ToString(), newFaction);
            }
        }

        public void RotateFactions()
        {
            var tmpFactions = server.factions;
            List<string> nextRotation = factionRotationList[rotationIndex].Split(',').Select(x => x.Trim()).ToList();
            LogWrite("Next rotation: " + factionRotationList[rotationIndex]);
            var requiredTeamCount = gameModes[server.data.GameMode];

            if (requiredTeamCount > nextRotation.Count)
            {
                ConsoleWarn("This game mode has " + requiredTeamCount + " teams but only " + nextRotation.Count
                    + " factions are present in this rotation entry.  Adding US faction for missing factions.");

                var missingEntries = requiredTeamCount - nextRotation.Count;

                for (int i = 0; i < missingEntries; i++)
                {
                    nextRotation.Add("US");
                }
            }

            for (int i = 0; i < nextRotation.Count; i++)
            {
                var teamID = i + 1;

                if (FactionValid(nextRotation[i]))
                {
                    var factionID = factions.FirstOrDefault(x => x.Value == nextRotation[i]).Key;

                    LogWrite("Setting team " + teamID + " faction to: " + nextRotation[i] + " - " + factionID);
                    SetFaction(teamID.ToString(), factionID);
                }
                else
                {
                    DebugWrite(DebugType.Factions, "Faction " + nextRotation[i] + " is not valid.  Not attempting to set team faction on server for team "
                        + teamID);
                }
            }

            if (rotationIndex > factionRotationList.Count)
                rotationIndex = 0;
            else
                rotationIndex++;
        }

        public void AnnounceFactions()
        {
            var nextGameMode = server.MapList[server.NextMapIndex].Gamemode;
            var numTeams = gameModes[nextGameMode];

            SendGlobalMessage("*** Scrambling Factions ***");
            Thread.Sleep(5000);
            SendGlobalMessage("Next game factions are: ");
            for (int i = 1; i <= numTeams; i++)
            {
                SendGlobalMessage("Team " + i + " faction = " + factions[server.factions[i - 1]]);
            }
        }

        public bool FactionValid(string faction)
        {
            bool success = false;

            if (factions.Any(x => x.Value == faction))
                success = true;

            return success;
        }

        public bool FactionListEntryValid(string factionListEntry)
        {
            bool isValid = true;
            var tmpFactions = factionListEntry.Split(',').Select(x => x.Trim()).ToList();

            foreach (var faction in tmpFactions)
            {
                if (!FactionValid(faction))
                    isValid = false;
            }

            return isValid;
        }

        #region Procon events

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnVersion", "OnPunkbusterPlayerInfo", "OnServerInfo", "OnResponseError", "OnListPlayers",
                "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat",
                "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded",
                "OnMaplistList", "OnMaplistGetMapIndices", "OnMaplistLoad", "OnMaplistSave", "OnMaplistCleared", "OnMaplistMapAppended",
                "OnMaplistNextLevelIndex", "OnMaplistMapRemoved", "OnMaplistMapInserted", "OnSquadIsPrivate", "OnSquadLeader",
                "OnPlayerTeamChange", "OnPlayerSquadChange", "OnPlayerMovedByAdmin", "OnTeamFactionOverride");

        }

        public void OnPluginEnable()
        {
            ConsoleWrite("Enabled!");


            pluginEnabled = true;

            mapListHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            server.UpdateMapDefinitions();

            ThreadPool.QueueUserWorkItem((state) =>
            {
                Thread.Sleep(20000);
                DebugWrite(DebugType.MapInfo, "Syncing map list information OnPluginEnable");
                getMapInfoSync();
            });
        }

        public void OnPluginDisable()
        {
            mapListHandle.Set();
            pluginEnabled = false;
            ConsoleWrite("Disabled!");
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {

        }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {

        }

        public override void OnTeamFactionOverride(int teamId, int faction)
        {
            server.factions[teamId - 1] = faction;
            DebugWrite(DebugType.Factions, "Team ID: " + teamId + " Faction: " + faction);
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo playerInfo)
        {

        }

        public override void OnPlayerJoin(string Name)
        {

        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {

        }

        public override void OnGlobalChat(string speaker, string message)
        {


        }

        public override void OnTeamChat(string speaker, string message, int teamId)
        {

        }

        public override void OnSquadChat(string speaker, string message, int teamId, int squadId)
        {
            if (speaker == "S1ngular1ty" && message == "!randomizeFactions")
                RandomizeFactions();
        }

        public override void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal)
        {
            DebugWrite(0, "OnLevelLoaded");

            ThreadPool.QueueUserWorkItem((state) =>
            {
                DebugWrite(DebugType.MapInfo, "Syncing map information OnLevelLoaded.");
                getMapInfoSync();
                DebugWrite(DebugType.MapInfo, "Current map is now: " + server.CurrentMapName);
            });

        } // BF3

        public override void OnLoadingLevel(string mapFileName, int roundsPlayed, int roundsTotal)
        {
            ConsoleWarn("OnLoadingLevel");
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            server.data = serverInfo;
            DebugWrite(DebugType.MapEvents, "OnServerInfo - Current map is: " + server.CurrentMapName);
        }

        public override void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            DebugWrite(DebugType.MapEvents, "OnMapListList");

            if (server.MapList.Count > 0)
                server.MapList.Clear();

            foreach (MaplistEntry map in lstMaplist)
            {
                server.MapList.Add(map);
            }

            DebugWrite(DebugType.MapList, "Current map list:");
            foreach (MaplistEntry map in server.MapList)
            {
                DebugWrite(DebugType.MapList, map.MapFileName + " " + server.MapList.IndexOf(map));
            }

            mapListHandle.Set();
        }
        public override void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            server.CurrentMapIndex = mapIndex;
            server.NextMapIndex = nextIndex;

            DebugWrite(DebugType.MapEvents, "OnMapListGetMapIndices");
            mapListHandle.Set();
        }
        public override void OnMaplistLoad() { getMapInfo(); }
        public override void OnMaplistSave() { getMapInfo(); }
        public override void OnMaplistCleared() { getMapInfo(); }
        public override void OnMaplistMapAppended(string mapFileName) { getMapInfo(); }
        public override void OnMaplistNextLevelIndex(int mapIndex) { getMapInfo(); }
        public override void OnMaplistMapRemoved(int mapIndex) { getMapInfo(); }
        public override void OnMaplistMapInserted(int mapIndex, string mapFileName) { getMapInfo(); }

        public void getMapList()
        {
            ServerCommand("mapList.list");
        }
        public void getMapIndices()
        {
            ServerCommand("mapList.getMapIndices");
        }
        public void getMapInfo()
        {
            getMapList();
            getMapIndices();
        }

        public void getMapListSync()
        {
            DebugWrite(DebugType.MapEventHandler, "Reset maplist handle for map list.");
            mapListHandle.Reset();
            DebugWrite(DebugType.MapEventHandler, "Requesting map list.");
            getMapList();
            DebugWrite(DebugType.MapEventHandler, "Waiting on map list.");
            mapListHandle.WaitOne();
        }
        public void getMapIndicesSync()
        {
            DebugWrite(DebugType.MapEventHandler, "Reset maplist handle for map indices.");
            mapListHandle.Reset();
            DebugWrite(DebugType.MapEventHandler, "Requesting map indices.");
            getMapIndices();
            DebugWrite(DebugType.MapEventHandler, "Waiting on map indices.");
            mapListHandle.WaitOne();
        }
        public void getMapInfoSync()
        {
            getMapListSync();
            getMapIndicesSync();
        }

        public override void OnRoundOver(int winningTeamId)
        {
            ConsoleWarn("OnRoundOver");
            server.ClearRoundData();

            if (operatingMode == Mode.Randomize)
                RandomizeFactions();
            else if (operatingMode == Mode.Rotation)
                RotateFactions();

            ThreadPool.QueueUserWorkItem((state) =>
            {
                AnnounceFactions();
            });

        }

        #endregion


        #region Unused Procon Events

        public override void OnPlayerTeamChange(string Name, int teamId, int squadId) { }

        public override void OnPlayerSquadChange(string soldierName, int teamId, int squadId) { }

        public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled) { }

        public override void OnSquadIsPrivate(int teamId, int squadId, bool isPrivate) { }

        public override void OnSquadLeader(int teamId, int squadId, string soldierName) { }

        public override void OnPlayerKilled(Kill kKillerVictimDetails) { }

        public override void OnPlayerSpawned(string Name, Inventory spawnedInventory) { }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores) { }

        public override void OnLevelStarted() { }

        public override void OnVersion(string serverType, string version) { }

        public override void OnResponseError(List<string> requestWords, string error) { }

        #endregion


        public class ServerInfo
        {
            FactionManager Plugin { get; set; }
            public CServerInfo data = null;
            public List<MaplistEntry> MapList { get; set; }
            public Dictionary<string, string> FriendlyMaps { get; set; }
            public List<int> factions = new List<int>() { -1, -1, -1, -1 };

            int _WinTeamId = 0;

            public int CurrentRound { get { return data.CurrentRound; } }
            public int TotalRounds { get { return data.TotalRounds; } }
            public int RoundTime { get { return data.RoundTime; } }
            public string MapFileName { get { return (MapList == null) ? data.Map : MapList[CurrentMapIndex].MapFileName; } }
            public string CurrentMapName
            {
                get
                {
                    return (MapList == null) ? FriendlyMaps[data.Map] : FriendlyMaps[MapList[CurrentMapIndex].MapFileName];
                }
            }
            public string NextMapName
            {
                get
                {
                    return (MapList == null) ? "" : FriendlyMaps[MapList[NextMapIndex].MapFileName];
                }
            }
            public string CurrentMapFileName { get { return (MapList == null) ? "" : MapList[CurrentMapIndex].MapFileName; } }
            public string NextMapFileName { get { return (MapList == null) ? "" : MapList[NextMapIndex].MapFileName; } }
            public int CurrentMapIndex { get; set; }
            public int NextMapIndex { get; set; }
            public int PlayerCount { get { return data.PlayerCount; } }
            public double TimeUp { get { return data.ServerUptime; } }
            public int WinTeamId { get { return _WinTeamId; } internal set { _WinTeamId = value; } }
            private int team1Score;
            public int Team1Score { get { return data.TeamScores[0].Score; } internal set { team1Score = value; } }
            private int team2Score;
            public int Team2Score { get { return data.TeamScores[1].Score; } internal set { team2Score = value; } }
            public DateTime RoundStart
            {
                get
                {
                    DateTime ret = DateTime.MinValue;

                    TimeSpan ts = new TimeSpan(0, 0, RoundTime);
                    ret = DateTime.Now.Subtract(ts);

                    return ret;
                }
            }

            public double RoundTimeMinutes { get { return (DateTime.Now - RoundStart).TotalMinutes; } }

            public void ClearRoundData()
            {
                Team1Score = 0;
                Team2Score = 0;
            }

            public void UpdateMapDefinitions()
            {
                if (FriendlyMaps.Count > 0)
                    FriendlyMaps.Clear();
                List<CMap> bf3_defs = Plugin.GetMapDefines();
                foreach (CMap m in bf3_defs)
                {
                    if (!FriendlyMaps.ContainsKey(m.FileName)) FriendlyMaps[m.FileName] = m.PublicLevelName;
                }
            }

            public ServerInfo(FactionManager plugin)
            {
                MapList = new List<MaplistEntry>();
                FriendlyMaps = new Dictionary<string, string>();
                Plugin = plugin;
            }
        }

    } // end FactionManager

} // end namespace PRoConEvents



