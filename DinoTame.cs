using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;
using Streamer.bot.Common.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Globalization;

public class CPHInline : CPHInlineBase
{
    private static readonly Random rnd = new Random();
    private List<DinoEntry> dinoList = new List<DinoEntry>();
    private Dictionary<string, int> kibbleCosts;
    private Dictionary<string, double> kibbleBonus;
    private string currentDino;
    private double currentDinoTameChance;
    private double currentDinoSpawnChance;
    private string currentDinoIcon;
    private int tameDuration;
    private bool tamingActive = false;
    private List<TameEntry> tameEntries = new List<TameEntry>();
    private string dinosFilePath;
    private string dinoIconPath;
    private bool isInitialized = false;
    private string obsSceneName;
    private string obsSourceGroup;
    private string obsSourceFrame;
    private string obsSourceName;
    private string obsSourceIcon;
    private string obsSourceSpawnChanceTitle;
    private string obsSourceSpawnChance;
    private string obsSourceTameChanceTitle;
    private string obsSourceTameChance;
    private string obsSourceProgressBar;
    private string obsSourceProgressBarPath;
    private string saveFolderPath;

    // Helper class for deserialization
    public class DinoEntry
    {
        public string name { get; set; }
        public double spawn_chance { get; set; }
        public double tame_chance { get; set; }
        public List<string> maps { get; set; }
        public string icon { get; set; }
        public double health { get; set; }
        public double damage { get; set; }
    }

    public class TamedDinoEntry
    {
        public string tame_date { get; set; }
    }

    class TameEntry
    {
        public string user { get; set; }
        public string kibbleType { get; set; }
    }

    public bool Execute()
    {
        CPH.TryGetArg("tameDuration", out tameDuration);
        CPH.TryGetArg("obsSceneName", out obsSceneName);
        CPH.TryGetArg("obsSourceGroup", out obsSourceGroup);
        CPH.TryGetArg("obsSourceProgressBar", out obsSourceProgressBar);
        CPH.TryGetArg("obsSourceProgressBarPath", out obsSourceProgressBarPath);
        CPH.TryGetArg("dinoIconPath", out dinoIconPath);
        CPH.TryGetArg("obsSourceIcon", out obsSourceIcon);
        CPH.TryGetArg("obsSourceName", out obsSourceName);
        CPH.TryGetArg("obsSourceSpawnChanceTitle", out obsSourceSpawnChanceTitle);
        CPH.TryGetArg("obsSourceSpawnChance", out obsSourceSpawnChance);
        CPH.TryGetArg("obsSourceTameChanceTitle", out obsSourceTameChanceTitle);
        CPH.TryGetArg("obsSourceTameChance", out obsSourceTameChance);

        CPH.TryGetArg("saveFolderPath", out saveFolderPath);

        if (string.IsNullOrEmpty(obsSceneName) || string.IsNullOrEmpty(obsSourceGroup) ||
            string.IsNullOrEmpty(obsSourceName) || string.IsNullOrEmpty(obsSourceIcon) || string.IsNullOrEmpty(obsSourceSpawnChanceTitle) ||
            string.IsNullOrEmpty(obsSourceSpawnChance) || string.IsNullOrEmpty(obsSourceTameChanceTitle) || string.IsNullOrEmpty(obsSourceTameChance) ||
            string.IsNullOrEmpty(obsSourceProgressBar) || string.IsNullOrEmpty(obsSourceProgressBarPath) || tameDuration <= 0 || string.IsNullOrEmpty(dinoIconPath))
        {
            CPH.LogWarn("[DinoTame] Arguments are not set correctly.");
            return false;
        }

        currentDino = string.Empty;
        currentDinoTameChance = 0.0;
        currentDinoSpawnChance = 0.0;
        currentDinoIcon = string.Empty;

        dinosFilePath = args.ContainsKey("dinosFilePath") ? args["dinosFilePath"].ToString() : "Dinos.json";

        if (!System.IO.File.Exists(dinosFilePath))
        {
            CPH.LogWarn($"[DinoTame] File '{dinosFilePath}' not found.");
            return false;
        }

        string json = System.IO.File.ReadAllText(dinosFilePath);
        try
        {
            dinoList = JsonConvert.DeserializeObject<List<DinoEntry>>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[DinoTame] Failed to parse JSON: {ex.Message}");
            return false;
        }

        // Filter out entries that have no maps or empty maps
        dinoList = dinoList.Where(d => d.maps != null && d.maps.Any(m => !string.IsNullOrWhiteSpace(m))).ToList();
        if (dinoList == null || dinoList.Count == 0)
        {
            CPH.LogWarn("[DinoTame] No dinos with maps found in the JSON file.");
            return false;
        }

        // filter by maps if enabled and argument provided (support multiple maps, comma or semicolon separated)
        bool enableMaps = args.ContainsKey("enableMaps") && args["enableMaps"] is bool b && b;
        if (enableMaps)
        {
            CPH.LogInfo("[DinoTame] Filtering dinos by maps enabled.");
            if (args.ContainsKey("map"))
            {
                string mapArg = args["map"].ToString();
                CPH.LogInfo($"[DinoTame] Map argument found, filtering by maps: {mapArg}");
                if (string.IsNullOrWhiteSpace(mapArg))
                {
                    CPH.LogWarn("[DinoTame] Map argument is empty.");
                    return false;
                }

                var maps = mapArg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList();
                if (maps.Count == 0)
                {
                    CPH.LogWarn("[DinoTame] No valid maps provided in Map argument.");
                    return false;
                }

                dinoList = dinoList.Where(d => d.maps != null && d.maps.Any(dm => maps.Contains(dm, StringComparer.OrdinalIgnoreCase))).ToList();
                if (dinoList.Count == 0)
                {
                    CPH.LogWarn($"[DinoTame] No dinos found for maps: {string.Join(", ", maps)}.");
                    return false;
                }
            }
        }

        kibbleCosts = new Dictionary<string, int>()
        {
            {"basic", 1},
            {"simple", 2},
            {"regular", 4},
            {"superior", 8},
            {"exceptional", 16},
            {"extraordinary", 32}
        };

        // Kibble-Werte
        kibbleBonus = new Dictionary<string, double>()
        {
            {"basic", 0.5},
            {"simple", 0.75},
            {"regular", 1.0},
            {"superior", 1.25},
            {"exceptional", 1.5},
            {"extraordinary", 1.75}
        };

        CPH.LogInfo($"[DinoTame] Initialized");
        isInitialized = true;
        return true;
    }

    public bool SelectRandomDino()
    {
        // Weighted random selection based on spawn_chance
        double totalWeight = dinoList.Sum(d => d.spawn_chance);
        if (totalWeight <= 0)
        {
            CPH.LogWarn("[DinoTame] Total spawn chance is zero or negative.");
            return false;
        }

        //Random rnd = new Random();
        double roll = rnd.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var dino in dinoList)
        {
            cumulative += dino.spawn_chance;
            if (roll <= cumulative)
            {
                CPH.LogInfo($"[DinoTame] Randomly selected dino: {dino.name} (spawn_chance: {dino.spawn_chance}, tame_chance: {dino.tame_chance})");
                currentDino = dino.name;
                currentDinoTameChance = dino.tame_chance;
                currentDinoSpawnChance = dino.spawn_chance;
                currentDinoIcon = dino.icon;
                return true;
            }
        }

        CPH.LogWarn("[DinoTame] Failed to select a dino.");
        return false;
    }

    public bool SpawnDino()
    {
        if (!SelectRandomDino())
        {
            CPH.LogWarn("[DinoTame] Failed to select a random dino.");
            return false;
        }

        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogWarn("No dino selected to spawn.");
            return false;
        }

        CPH.LogInfo($"[DinoTame] Spawning dino: {currentDino}");
        tamingActive = true;
        ObsOverlay();
        CPH.SetTimerInterval("[DinoTame] Tame Duration", tameDuration); // Set timer for taming duration
        CPH.EnableTimer("[DinoTame] Tame Duration");
        CPH.TryGetArg("spawnDinoMessage", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "currentDino", currentDino }
        });
        CPH.SendMessage(message);
        return true;
    }

    public bool BuyEggPaste()
    {
        var user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        int eggPasteAmountToAdd = args.ContainsKey("eggPasteAmountToAdd") ? int.Parse(args["eggPasteAmountToAdd"].ToString()) : 10;

        CPH.TryGetArg("rewardName", out string input);
        Match match = Regex.Match(input, @"\d+");

        if (match.Success)
        {
            eggPasteAmountToAdd = int.Parse(match.Value);
        }
        else
        {
            CPH.TryGetArg("rewardId", out string rewardId);
            CPH.TryGetArg("redemptionId", out string redemptionId);

            CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            return false;
        }


        eggPaste += eggPasteAmountToAdd;
        CPH.SetTwitchUserVar(user, "dinoTame_egg_paste", eggPaste, true);
        CPH.TryGetArg("buyEggPasteMessage", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "user", user },
            { "eggPaste", eggPaste },
            { "eggPasteAmountToAdd", eggPasteAmountToAdd }
        });
        CPH.SendMessage(message);
        CPH.LogInfo($"[DinoTame] Buy Egg Paste {user} | {eggPaste - eggPasteAmountToAdd} | +{eggPasteAmountToAdd} | {eggPaste}");
        return true;
    }

    public string ReplaceWithArgs(string input, Dictionary<string, object> args)
    {
        foreach (var arg in args)
        {
            input = input.Replace("{" + arg.Key + "}", arg.Value?.ToString() ?? "");
        }
        return input;
    }

    public bool UseKibbleForTaming()
    {
        string user = args["user"].ToString();
        string input = args["rawInput"].ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
        {
            input = "basic";
        }
        string message;
        if (!tamingActive)
        {
            CPH.TryGetArg("useKibbleMessageNotActive", out message);
            CPH.SendMessage(message);
            return false;
        }

        if (GetUserTamedDino())
        {
            CPH.TryGetArg("useKibbleMessageAlreadyTamed", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino }
            });
            CPH.SendMessage(message);
            return false;
        }

        // Optional: prüfen ob User bereits mitgemacht hat
        if (tameEntries.Count > 0 && tameEntries.Any(e => e.user.Equals(user, StringComparison.OrdinalIgnoreCase)))
        {
            CPH.TryGetArg("useKibbleMessageAlreadyTaming", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        if (!kibbleCosts.ContainsKey(input))
        {
            CPH.TryGetArg("useKibbleMessageInvalidType", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        int cost = kibbleCosts[input];
        int currentPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        if (currentPaste < cost)
        {
            CPH.TryGetArg("useKibbleMessageNotEnoughPaste", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "currentPaste", currentPaste },
                { "cost", cost }
            });
            CPH.SendMessage(message);
            return false;
        }



        tameEntries.Add(new TameEntry { user = user, kibbleType = input });
        // Kibble abziehen
        CPH.SetTwitchUserVar(user, "dinoTame_egg_paste", currentPaste - cost, true);
        CPH.TryGetArg("useKibbleMessageSuccess", out message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "user", user },
            { "currentDino", currentDino },
            { "input", input },
            { "currentPaste", currentPaste - cost }
        });
        CPH.SendMessage(message);
        return true;
    }

    public bool EvaluateTaming()
    {
        double baseTameChance = currentDinoTameChance;
        tamingActive = false;
        ObsOverlay();
        CPH.DisableTimer("[DinoTame] Tame Duration");

        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogError("[DinoTame] Fehler bei der Auswertung: Daten fehlen.");
            return false;
        }
        else if (baseTameChance < 0 || baseTameChance > 100)
        {
            CPH.LogError("[DinoTame] Ungültige Fangchance für den Dino.");
            return false;
        }

        if (tameEntries == null || tameEntries.Count == 0)
        {
            CPH.LogInfo("[DinoTame] Niemand hat versucht, den Dino zu zähmen.");

            CPH.TryGetArg("pickWinnerMessageNoEntries", out string message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "currentDino", currentDino }
            });
            CPH.SendMessage(message);

            return false;
        }

        //Random rnd = new Random();
        List<string> winners = new();
        double roll = rnd.NextDouble() * 100;

        foreach (var entry in tameEntries)
        {
            string user = entry.user;
            string kibble = entry.kibbleType.ToLower();
            if (!kibbleBonus.TryGetValue(kibble, out double bonus))
            {
                CPH.LogWarn($"[DinoTame] Ungültiger Kibble-Typ von {user}: {entry.kibbleType}");
                continue;
            }

            double finalChance = baseTameChance * bonus;
            finalChance = Math.Min(finalChance, 100); // Maximal 100%
            bool success = roll <= finalChance;
            CPH.LogInfo($"[DinoTame] {user} | Kibble: {kibble} | Chance: {finalChance:F1}% | Roll: {roll:F1}% | Success: {success}");
            if (success)
            {
                winners.Add(user);
                AddUserTamedDino(user);
            }
        }

        if (winners.Count > 0)
        {
            CPH.TryGetArg("pickWinnerMessage", out string message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "currentDino", currentDino },
                { "winners", string.Join(", ", winners) }
            });
            CPH.SendMessage(message);
            AutoCommitAndPush();
        }
        else
        {
            CPH.TryGetArg("pickWinnerMessageFail", out string message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "currentDino", currentDino }
            });
            CPH.SendMessage(message);
        }

        // Aufräumen
        tameEntries.Clear();
        currentDino = string.Empty;
        currentDinoTameChance = 0.0;
        currentDinoSpawnChance = 0.0;
        currentDinoIcon = string.Empty;
        return true;
    }

    public bool GetEggPasteCount()
    {
        string user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        CPH.TryGetArg("getEggPasteMessage", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "user", user },
            { "eggPaste", eggPaste }
        });
        CPH.SendMessage(message);
        return true;
    }

    public bool GetUserTamedDino(string user = null)
    {
        if (string.IsNullOrEmpty(user))
        {
            user = args["user"].ToString();
        }

        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return false;
        }

        // JSON in Liste umwandeln
        Dictionary<string, TamedDinoEntry> tamedDinos = JsonConvert.DeserializeObject<Dictionary<string, TamedDinoEntry>>(json);

        // Abfragen, ob der Dino drin ist (Groß-/Kleinschreibung beachten!)
        bool alreadyTamed = tamedDinos.ContainsKey(currentDino);

        return alreadyTamed;
    }

    public int GetUserTamedDinoCount(string user = null)
    {
        if (string.IsNullOrEmpty(user))
        {
            user = args["user"].ToString();
        }

        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return 0;
        }

        // JSON in Liste umwandeln
        Dictionary<string, TamedDinoEntry> tamedDinos = JsonConvert.DeserializeObject<Dictionary<string, TamedDinoEntry>>(json);
        return tamedDinos.Count;
    }

    public bool AddUserTamedDino(string user)
    {
        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogWarn("[DinoTame] Kein aktueller Dino zum Hinzufügen.");
            return false;
        }

        // Aktuelle Tamed Dinos des Users abrufen
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        Dictionary<string, TamedDinoEntry> tamedDinos;
        
        if (string.IsNullOrWhiteSpace(json))
        {
            tamedDinos = new Dictionary<string, TamedDinoEntry>();
        }
        else
        {
            tamedDinos = JsonConvert.DeserializeObject<Dictionary<string, TamedDinoEntry>>(json);
        }

        // Prüfen, ob der Dino bereits getamed wurde
        if (tamedDinos.ContainsKey(currentDino))
        {
            CPH.LogWarn($"[DinoTame] {user} hat den Dino {currentDino} bereits getamed.");
            return false;
        }

        // Dino hinzufügen
        tamedDinos.Add(currentDino, new TamedDinoEntry
        {
            tame_date = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
        });

        string newJson = JsonConvert.SerializeObject(tamedDinos, Formatting.Indented);

        CPH.SetTwitchUserVar(user, "dinoTame_TamedDinos", newJson, true);
        CPH.LogInfo($"[DinoTame] {user} hat den Dino {currentDino} erfolgreich getamed.");

        string filePath = Path.Combine(saveFolderPath, $"{user}.json");
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }
        // Speichern
        File.WriteAllText(filePath, newJson);

        return true;

    }

    public bool CheckUserTamedDino()
    {
        string user = args["user"].ToString();
        bool alreadyTamed = GetUserTamedDino();
        int countTamed = GetUserTamedDinoCount();
        string message = string.Empty;

        if (!tamingActive)
        {
            CPH.TryGetArg("checkUserTamedMessageNotActive", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "countTamed", countTamed },
                { "countDinos", dinoList.Count }
            });
            CPH.SendMessage(message);
            return false;
        }

        if (alreadyTamed)
        {
            CPH.TryGetArg("checkUserTamedMessageYes", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino }
            });
        }
        else
        {
            CPH.TryGetArg("checkUserTamedMessageNo", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino }
            });
        }

        CPH.SendMessage(message);
        return true;
    }

    public void ObsOverlay()
    {
        int obsId = 0;
        // get OBS ID
        if ( CPH.ObsIsConnected(1) ) obsId = 1;

        if (!isInitialized)
        {
            CPH.LogWarn("[DinoTame] Plugin not initialized. Cannot update OBS overlay.");
            return;
        }

        if (tamingActive)
        {
            CPH.ObsSetGdiText(obsSceneName, obsSourceName, currentDino, obsId);
            CPH.ObsSetGdiText(obsSceneName, obsSourceSpawnChanceTitle, "Spawn Chance:", obsId);
            CPH.ObsSetGdiText(obsSceneName, obsSourceSpawnChance, currentDinoSpawnChance.ToString("F2") + "%", obsId);
            CPH.ObsSetGdiText(obsSceneName, obsSourceTameChanceTitle, "Tame Chance:", obsId);
            CPH.ObsSetGdiText(obsSceneName, obsSourceTameChance, currentDinoTameChance.ToString("F2") + "%", obsId);
            CPH.ObsSetImageSourceFile(obsSceneName, obsSourceIcon, dinoIconPath + "\\" + currentDinoIcon, obsId);
            CPH.ObsSetBrowserSource(obsSceneName, obsSourceProgressBar, $"file:///{obsSourceProgressBarPath}?duration={tameDuration}", obsId);

            CPH.ObsShowSource(obsSceneName, obsSourceGroup, obsId);
        }
        else if (!tamingActive)
        {
            CPH.ObsHideSource(obsSceneName, obsSourceGroup, obsId);
        }
        else
        {
            CPH.LogError("[DinoTame] OBS overlay cannot be updated.");
        }
    }

    public bool GetKibble()
    {
        string message = string.Empty;
        foreach (var kibble in kibbleCosts)
        {
            string name = kibble.Key;
            int costs = kibble.Value;
            double bonus = kibbleBonus.ContainsKey(name) ? kibbleBonus[name] : 0.0;

            if (string.IsNullOrEmpty(message))
            {
                message = "Kibble-Liste (Kibble / Kosten / Bonus): ";
            }
            else
            {
                message += "; ";
            }
            message += ($"{name} / {costs} / {bonus}");
        }

        CPH.SendMessage(message);
        return true;
    }

    public bool FightRequest()
    {
        string requestUser = args["user"].ToString();
        CPH.TryGetArg("input0", out string targetUser);
        string message = string.Empty;

        if (CPH.GetGlobalVar<string>("dinoTame_FightRequest", false) != null)
        {
            return false;
        }

        if (GetUserTamedDinoCount() <= 0)
        {
            CPH.TryGetArg("fightMessageNoDinos", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", requestUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {requestUser} has no tamed dinos to fight with.");
            return false;
        }

        if (string.IsNullOrEmpty(targetUser))
        {
            CPH.TryGetArg("fightMessageNoTarget", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", requestUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn("[DinoTame] Fight target is empty.");
            return false;
        }

        var targetUserInfo = CPH.TwitchGetExtendedUserInfoByLogin(targetUser) as TwitchUserInfoEx;
        if (targetUserInfo == null)
        {
            CPH.TryGetArg("fightMessageNoUser", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", requestUser },
                { "targetUser", targetUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] No user found for {targetUser}");
            return false;
        }
        targetUser = targetUserInfo.UserName;

        if (targetUser.Equals(requestUser, StringComparison.OrdinalIgnoreCase))
        {
            CPH.TryGetArg("fightMessageSelf", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", requestUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {requestUser} cannot fight against themselves.");
            return false;
        }

        if (GetUserTamedDinoCount(targetUser) <= 0)
        {
            CPH.TryGetArg("fightMessageTargetNoDinos", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", requestUser },
                { "targetUser", targetUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {targetUser} has no tamed dinos to fight with.");
            return false;
        }

        string requestUserDino = GetRandomTamedDino(requestUser);
        string targetUserDino = GetRandomTamedDino(targetUser);

        CPH.TryGetArg("fightMessage", out message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "user", requestUser },
            { "targetUser", targetUser },
            { "requestUserDino", requestUserDino },
            { "targetUserDino", targetUserDino }
        });
        CPH.SendMessage(message);

        CPH.EnableTimer("[DinoTame] Fight Request Timer");
        CPH.SetGlobalVar("dinoTame_FightTarget", targetUser, false);
        CPH.SetGlobalVar("dinoTame_FightRequest", requestUser, false);
        CPH.SetGlobalVar("dinoTame_FightDinoTarget", targetUserDino, false);
        CPH.SetGlobalVar("dinoTame_FightDinoRequest", requestUserDino, false);
        return true;
    }

    public bool FightAnswer()
    {
        if ( !CPH.TryGetArg("user", out string targetUser))
        {
            targetUser = CPH.GetGlobalVar<string>("dinoTame_FightRequest", false);
        }

        string requestUser = CPH.GetGlobalVar<string>("dinoTame_FightRequest", false);

        if (string.IsNullOrEmpty(requestUser))
        {
            CPH.LogWarn($"[DinoTame] {targetUser} has no fight request.");
            return false;
        }

        CPH.TryGetArg("commandName", out string command);
        CPH.TryGetArg("triggerName", out string triggerName);
        if (command == "[DinoTame] Fight Accept")
        {
            CPH.DisableTimer("[DinoTame] Fight Request Timer");
            int result = FightResult(requestUser, targetUser);

            string message;
            if (result == 0)
            {
                CPH.TryGetArg("fightMessageDraw", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });
            }
            else if (result == 1)
            {
                CPH.TryGetArg("fightMessageWin", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });
            }
            else
            {
                CPH.TryGetArg("fightMessageLose", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });
            }
            CPH.UnsetGlobalVar("dinoTame_FightRequest", false);
            CPH.UnsetGlobalVar("dinoTame_FightTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoRequest", false);
            CPH.SendMessage(message);
            return true;
        }
        else if (triggerName == "Timed Actions" || command == "[DinoTame] Fight Reject")
        {
            targetUser = CPH.GetGlobalVar<string>("dinoTame_FightTarget", false);

            CPH.UnsetGlobalVar("dinoTame_FightRequest", false);
            CPH.UnsetGlobalVar("dinoTame_FightTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoRequest", false);
            CPH.TryGetArg("fightMessageReject", out string message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "targetUser", targetUser },
                { "requestUser", requestUser }
            });
            CPH.SendMessage(message);
            return false;
        }
        else
        {
            CPH.LogWarn($"[DinoTame] Unknown command: {command}");
            return false;
        }
    }

    public string GetRandomTamedDino(string user)
    {
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            CPH.LogWarn($"[DinoTame] {user} has no tamed dinos.");
            return string.Empty;
        }

        Dictionary<string, TamedDinoEntry> tamedDinos = JsonConvert.DeserializeObject<Dictionary<string, TamedDinoEntry>>(json);
        //Random rnd = new Random();
        int index = rnd.Next(tamedDinos.Count);
        return tamedDinos.ElementAt(index).Key;
    }

    public int FightResult(string requestUser, string targetUser)
    {
        string userDinoName = CPH.GetTwitchUserVar<string>(requestUser, "dinoTame_FightDino", false);
        string targetUserDinoName = CPH.GetTwitchUserVar<string>(targetUser, "dinoTame_FightDino", false);
        var requestUserDino = dinoList.Find(d => d.name.Equals(userDinoName, StringComparison.OrdinalIgnoreCase));
        var targetUserDino = dinoList.Find(d => d.name.Equals(targetUserDinoName, StringComparison.OrdinalIgnoreCase));

        double requestUserDinoHealth = requestUserDino.health;
        double targetUserDinoHealth = targetUserDino.health;

        CPH.LogInfo($"[DinoTame] Fight: {requestUser} ({userDinoName} HP:{requestUserDino.health} DMG:{requestUserDino.damage}) vs {targetUser} ({targetUserDinoName} HP:{targetUserDino.health} DMG:{targetUserDino.damage})");

        CPH.TryGetArg("fightMessageAccept", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "requestUser", requestUser },
                { "targetUser", targetUser },
                { "requestUserDino", userDinoName },
                { "targetUserDino", targetUserDinoName },
                { "requestUserDinoHealth", requestUserDinoHealth },
                { "targetUserDinoHealth", targetUserDinoHealth },
                { "requestUserDinoDamage", requestUserDino.damage },
                { "targetUserDinoDamage", targetUserDino.damage }
            });
        CPH.SendMessage(message);

        while (requestUserDinoHealth > 0 && targetUserDinoHealth > 0)
        {
            double fightCritDmg = 1.1; // Crit damage multiplier
            bool userCrit = false;
            bool targetUserCrit = false;
            double requestUserDinoDamage = requestUserDino.damage;
            double targetUserDinoDamage = targetUserDino.damage;

            if (FightCritChance())
            {
                requestUserDinoDamage *= fightCritDmg; // Crit damage
                userCrit = true;
            }
            if (FightCritChance())
            {
                targetUserDinoDamage *= fightCritDmg; // Crit damage
                targetUserCrit = true;
            }
            targetUserDinoHealth -= requestUserDinoDamage;
            requestUserDinoHealth -= targetUserDinoDamage;
            CPH.LogInfo($"[DinoTame] {requestUser} ({requestUserDinoHealth} HP {userCrit}) vs {targetUser} ({targetUserDinoHealth} HP {targetUserCrit})");
        }

        if (requestUserDinoHealth <= 0 && targetUserDinoHealth <= 0)
        {
            return 0; // Both dinos are dead, no winner
        }
        else if (requestUserDinoHealth > 0)
        {
            return 1; // User wins
        }
        else
        {
            return -1; // Target user wins
        }


    }

    public bool FightCritChance(int critChance = 50)
    {
        //Random rnd = new Random();
        int chance = rnd.Next(1, 101);
        CPH.LogInfo($"[DinoTame] Fight Crit Chance Roll: {chance} (Crit Chance: {critChance})");
        return chance <= critChance;
    }

    private void RunGitCommand(string arguments)
    {

        string RepoPath = @"C:\Users\rumsp\Documents\GitHub\DinoTamedViewer";

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = RepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        process.WaitForExit();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Git-Fehler bei '{arguments}': {error}");
        }
        else
        {
            Console.WriteLine($"Git-Ausgabe '{arguments}': {output}");
        }
    }

    public void AutoCommitAndPush()
    {
        string message = $"Automatischer Commit am {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        RunGitCommand("add .");
        RunGitCommand($"commit -m \"{message}\"");
        RunGitCommand("push");
    }


}