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
    private TwitchUserInfo broadcaster;
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
    private List<FighterEntry> fighterEntries = new List<FighterEntry>();
    private bool arenaOpen = false;
    private string dinosFilePath;
    private string dinoIconPath;
    private bool isInitialized = false;
    private int obsId = 0;
    private string obsSceneName;
    private string obsSourceGroup;
    private string obsSourceOverlay;
    private string obsSourceOverlayPath;
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
        public double health { get; set; }
        public double damage { get; set; }
        public int win { get; set; }
        public int lose { get; set; }
    }
    public class TameEntry
    {
        public string user { get; set; }
        public string kibbleType { get; set; }
    }
    public class FighterEntry
    {
        public string user { get; set; }
        public string dino { get; set; }
        public double currentHealth { get; set; }
        public double maxHealth { get; set; }
        public double damage { get; set; }
    }
    public bool Execute()
    {
        // get OBS ID
        if (CPH.ObsIsConnected(1)) obsId = 1;
        // Make broadcaster available globally in the script
        broadcaster = CPH.TwitchGetBroadcaster();

        CPH.TryGetArg("tameDuration", out tameDuration);
        CPH.TryGetArg("obsSceneName", out obsSceneName);
        CPH.TryGetArg("obsSourceGroup", out obsSourceGroup);
        CPH.TryGetArg("obsSourceOverlay", out obsSourceOverlay);
        CPH.TryGetArg("obsSourceOverlayPath", out obsSourceOverlayPath);
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
    public DinoEntry GetDinoByName(string name)
    {
        // Suche den Dino in der Liste (Groß-/Kleinschreibung ignorieren)
        return dinoList.FirstOrDefault(d => d.name.Equals(name, StringComparison.OrdinalIgnoreCase));
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
        if (!isStreamLive())
        {
            CPH.LogWarn("[DinoTame] Stream offline.");
            return false;
        }

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

        DinoEntry currentDinoValue = GetDinoByName(currentDino);

        var payload = new
        {
            type = "spawnDino",
            dinoName = currentDinoValue.name,
            iconUrl = dinoIconPath + "\\" + currentDinoValue.icon,
            spawnChance = currentDinoValue.spawn_chance,
            tameChance = currentDinoValue.tame_chance,
            baseHP = currentDinoValue.health,
            baseDMG = currentDinoValue.damage,
            duration = tameDuration
        };
        string json = JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);

        CPH.SetTimerInterval("ad7857a8-07fd-41ae-9b6c-cc36f9c5eab0", tameDuration); // Set timer for taming duration
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
    public bool AddWatchEggPaste()
    {
        List<Dictionary<string,object>> users = (List<Dictionary<string,object>>)args["users"]; 
        CPH.TryGetArg("isLive", out bool live);
        long? eggPaste;
        long? watchTime;
        string userId;
        int eggPasteAmountToAdd = args.ContainsKey("eggPasteAmountToAdd") ? int.Parse(args["eggPasteAmountToAdd"].ToString()) : 1;
        int eggPasteMinutes = 60;

        List<UserVariableValue<string>> userVarList = CPH.GetTwitchUsersVar<string>("dinoTame_watch_time", false);
        List<string> userVarNames = userVarList.Select(u => u.UserName).ToList();

        if (live)
        {
            for (int i = 0; i < users.Count; i++)
            {
                // Read in current points and add 1
                userId = users[i]["id"].ToString();
                string userName = users[i]["userName"].ToString();

                watchTime = CPH.GetTwitchUserVar<long?>(userName, "dinoTame_watch_time", false);
                watchTime ??= 0; // Wenn watchTime null ist, setze es auf 0
                watchTime += 1;
                if (userVarNames.Contains(userName))
                {
                    userVarNames.Remove(userName);
                }
                CPH.SetTwitchUserVar(userName, "dinoTame_watch_time", watchTime, false);

                // Wenn watchTime durch eggPasteMinutes ohne Rest teilbar ist (also ein Vielfaches), dann...
                if (watchTime % eggPasteMinutes == 0)
                {
                    // Hier kannst du beliebige Aktionen ausführen, z.B. eine Nachricht senden oder einen Bonus geben
                    CPH.LogInfo($"[DinoTame] {userName} hat {watchTime} Minuten Watchtime erreicht (Vielfaches von {eggPasteMinutes}).");
                    eggPaste = CPH.GetTwitchUserVar<long?>(userName, "dinoTame_egg_paste", true);
                    eggPaste += eggPasteAmountToAdd * (watchTime / eggPasteMinutes);
                    CPH.SetTwitchUserVar(userName, "dinoTame_egg_paste", eggPaste, true);
                }
            }
            foreach (var userName in userVarNames)
            {
                CPH.UnsetTwitchUserVar(userName, "dinoTame_watch_time", false);
            }
        }
        return true;
    }
    public bool AddEggPasteFirstWords()
    {
        string user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        int eggPasteAmountToAdd = args.ContainsKey("eggPasteAmountToAdd") ? int.Parse(args["eggPasteAmountToAdd"].ToString()) : 10;
        eggPaste += eggPasteAmountToAdd;
        CPH.SetTwitchUserVar(user, "dinoTame_egg_paste", eggPaste, true);
        return true;
    }
    public string ReplaceWithArgs(string input, Dictionary<string, object> args)
    {
        if (string.IsNullOrEmpty(input) || args == null || args.Count == 0)
        {
            string args2 = null;
            foreach (var arg in args)
            {
                if (string.IsNullOrEmpty(args2))
                {
                    args2 = arg.Key + ": " + arg.Value?.ToString();
                }
                else
                {
                    args2 = "; " + args2 + arg.Key + ": " + arg.Value?.ToString();
                }
            }
            CPH.LogWarn($"[DinoTame] ReplaceWithArgs called with empty input or args. Input: {input}, Args: {args2}");
            return input;
        }
        foreach (var arg in args)
        {
            input = input.Replace("{" + arg.Key + "}", arg.Value?.ToString() ?? "");
        }
        return input;
    }
    public bool UseKibbleForTaming()
    {
        string message;
        if (!tamingActive)
        {
            CPH.TryGetArg("useKibbleMessageNotActive", out message);
            CPH.SendMessage(message);
            return false;
        }

        string user = args["user"].ToString();
        string input = args["rawInput"].ToString().ToLowerInvariant();
        string defaultKibble = CPH.GetTwitchUserVar<string>(user, "dinoTame_kibble_type", true);
        if (string.IsNullOrEmpty(input))
        {
            input = defaultKibble;
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

        foreach (var entry in tameEntries)
        {
            double roll = rnd.NextDouble() * 100;
            string user = entry.user;
            string kibble = entry.kibbleType.ToLower();
            if (!kibbleBonus.TryGetValue(kibble, out double bonus))
            {
                CPH.LogWarn($"[DinoTame] Ungültiger Kibble-Typ von {user}: {entry.kibbleType}");
                continue;
            }

            double finalChance = baseTameChance * bonus;
            if (user == broadcaster.UserName)
            {
                finalChance *= 0.5; // -50% Bonus für den Broadcaster
            }
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
        Dictionary<string, TamedDinoEntry> tamedDinos = DeserializeUserTamedDinos(user);

        if (tamedDinos.ContainsKey(currentDino))
        {
            CPH.LogWarn($"[DinoTame] {user} hat den Dino {currentDino} bereits getamed.");
            return false;
        }

        // Dino hinzufügen
        DinoEntry currentDinoValue = GetDinoByName(currentDino);
        tamedDinos.Add(currentDino, new TamedDinoEntry
        {
            tame_date = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
            health = GetDinoStatValueMultiplier(currentDinoValue.health),
            damage = GetDinoStatValueMultiplier(currentDinoValue.damage),
            win = 0,
            lose = 0
        });

        SerializeUserTamedDinos(user, tamedDinos);
        CPH.LogInfo($"[DinoTame] {user} hat den Dino {currentDino} erfolgreich getamed.");
        return true;
    }
    public Dictionary<string, TamedDinoEntry> DeserializeUserTamedDinos(string user)
    {
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            CPH.LogInfo($"[DinoTame] {user} hat keine getamten Dinos.");
            return new Dictionary<string, TamedDinoEntry>();
        }

        Dictionary<string, TamedDinoEntry> tamedDinos = JsonConvert.DeserializeObject<Dictionary<string, TamedDinoEntry>>(json);
        return tamedDinos;
    }
    public bool SerializeUserTamedDinos(string user, Dictionary<string, TamedDinoEntry> tamedDinos)
    {
        string json = JsonConvert.SerializeObject(tamedDinos, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Include });
        CPH.SetTwitchUserVar(user, "dinoTame_TamedDinos", json, true);

        string filePath = Path.Combine(saveFolderPath, $"{user}.json");
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }
        // Speichern
        File.WriteAllText(filePath, json);

        return true;
    }
    public double GetDinoStatValueMultiplier(double baseValue)
    {
        double multiplier = 1.0;

        if (baseValue >= 10000)
        {
            multiplier = RandomStatMultiplier(0.1, 0.5);
        }
        else if (baseValue >= 1000 && baseValue < 10000)
        {
            multiplier = RandomStatMultiplier(0.5, 1.5);
        }
        else if (baseValue >= 100 && baseValue < 1000)
        {
            multiplier = RandomStatMultiplier(1.0, 3.0);
        }
        else if (baseValue >= 10 && baseValue < 100)
        {
            multiplier = RandomStatMultiplier(5.0, 15.0);
        }
        else if (baseValue >= 1 && baseValue < 10)
        {
            multiplier = RandomStatMultiplier(5.0, 20.0);
        }

        return Math.Round(baseValue * multiplier);
    }
    public double RandomStatMultiplier(double min = 0.5, double max = 1.5)
    {
        double multiplier = 1.0;

        multiplier = rnd.NextDouble() * (max - min) + min;

        return multiplier;
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
            message += $"{name} / {costs} / {bonus}";
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
        if (!CPH.TryGetArg("user", out string targetUser))
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

            Dictionary<string, TamedDinoEntry> requestUserTamedDinos = DeserializeUserTamedDinos(requestUser);
            Dictionary<string, TamedDinoEntry> targetUserTamedDinos = DeserializeUserTamedDinos(targetUser);

            string message;
            if (result == 0)
            {
                CPH.TryGetArg("fightMessageDraw", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoRequest", false), false);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoTarget", false), false);
            }
            else if (result == 1)
            {
                CPH.TryGetArg("fightMessageWin", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoRequest", false), true);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoTarget", false), false);
            }
            else
            {
                CPH.TryGetArg("fightMessageLose", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoRequest", false), false);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<string>("dinoTame_FightDinoTarget", false), true);
            }

            // Serialize updated tamed dinos back to user vars
            SerializeUserTamedDinos(requestUser, requestUserTamedDinos);
            SerializeUserTamedDinos(targetUser, targetUserTamedDinos);
            CPH.LogInfo($"[DinoTame] Fight result: {requestUser} vs {targetUser} - Result: {result}");

            CPH.UnsetGlobalVar("dinoTame_FightRequest", false);
            CPH.UnsetGlobalVar("dinoTame_FightTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoTarget", false);
            CPH.UnsetGlobalVar("dinoTame_FightDinoRequest", false);
            CPH.SendMessage(message);
            AutoCommitAndPush();
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
    public void UpdateWinLoseCounts(string user, string dino, bool isWin)
    {
        Dictionary<string, TamedDinoEntry> userTamedDinos = DeserializeUserTamedDinos(user);

        if (isWin)
        {
            userTamedDinos[dino].win++;
        }
        else
        {
            userTamedDinos[dino].lose++;
        }

        SerializeUserTamedDinos(user, userTamedDinos);
    }
    public string GetRandomTamedDino(string user)
    {
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            CPH.LogWarn($"[DinoTame] {user} has no tamed dinos.");
            return string.Empty;
        }

        Dictionary<string, TamedDinoEntry> tamedDinos = DeserializeUserTamedDinos(user);
        //Random rnd = new Random();
        int index = rnd.Next(tamedDinos.Count);
        return tamedDinos.ElementAt(index).Key;
    }
    public int FightResult(string requestUser, string targetUser)
    {
        string userDinoName = CPH.GetGlobalVar<string>("dinoTame_FightDinoRequest", false);
        string targetUserDinoName = CPH.GetGlobalVar<string>("dinoTame_FightDinoTarget", false);

        Dictionary<string, TamedDinoEntry> requestUserTamedDinos = DeserializeUserTamedDinos(requestUser);
        Dictionary<string, TamedDinoEntry> targetUserTamedDinos = DeserializeUserTamedDinos(targetUser);

        double requestUserDinoHealth = requestUserTamedDinos[userDinoName].health;
        double targetUserDinoHealth = targetUserTamedDinos[targetUserDinoName].health;
        double requestUserDinoDamage = requestUserTamedDinos[userDinoName].damage;
        double targetUserDinoDamage = targetUserTamedDinos[targetUserDinoName].damage;

        CPH.LogInfo($"[DinoTame] Fight: {requestUser} ({userDinoName} HP:{requestUserDinoHealth} DMG:{requestUserDinoDamage}) vs {targetUser} ({targetUserDinoName} HP:{targetUserDinoHealth} DMG:{targetUserDinoDamage})");

        CPH.TryGetArg("fightMessageAccept", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "requestUser", requestUser },
                { "targetUser", targetUser },
                { "requestUserDino", userDinoName },
                { "targetUserDino", targetUserDinoName },
                { "requestUserDinoHealth", requestUserDinoHealth },
                { "targetUserDinoHealth", targetUserDinoHealth },
                { "requestUserDinoDamage", requestUserDinoDamage },
                { "targetUserDinoDamage", targetUserDinoDamage }
            });
        CPH.SendMessage(message);

        while (requestUserDinoHealth > 0 && targetUserDinoHealth > 0)
        {
            double fightCritDmg = 3; // Crit damage multiplier
            bool userCrit = false;
            bool targetUserCrit = false;
            double requestUserDinoDamage2 = requestUserDinoDamage;
            double targetUserDinoDamage2 = targetUserDinoDamage;

            if (FightCritChance())
            {
                requestUserDinoDamage2 *= fightCritDmg; // Crit damage
                userCrit = true;
            }
            if (FightCritChance())
            {
                targetUserDinoDamage2 *= fightCritDmg; // Crit damage
                targetUserCrit = true;
            }
            targetUserDinoHealth -= requestUserDinoDamage2;
            requestUserDinoHealth -= targetUserDinoDamage2;
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
    public bool FightCritChance(int critChance = 25)
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
    public bool UpdateTamedDinos()
    {
        List<UserVariableValue<string>> userVarList = CPH.GetTwitchUsersVar<string>("dinoTame_TamedDinos", true);

        foreach (UserVariableValue<string> userVar in userVarList)
        {
            string user = userVar.UserName;
            Dictionary<string, TamedDinoEntry> tamedDinos = DeserializeUserTamedDinos(user);
            if (tamedDinos.Count == 0)
            {
                CPH.LogInfo($"[DinoTame] {user} has no tamed dinos to update.");
            }
            CPH.SetTwitchUserVar(user, "dinoTame_TamedDinos_backup", userVar.Value, true);

            bool updated = false;
            // Update each dino's stats
            foreach (var entry in tamedDinos)
            {
                var dinoName = entry.Key;
                var dinoEntry = GetDinoByName(dinoName);
                if (dinoEntry != null)
                {
                    if (entry.Value.health == 0)
                    {
                        entry.Value.health = GetDinoStatValueMultiplier(dinoEntry.health);
                        updated = true;
                    }

                    if (entry.Value.damage == 0)
                    {
                        entry.Value.damage = GetDinoStatValueMultiplier(dinoEntry.damage);
                        updated = true;
                    }
                }

                if (updated)
                {
                    SerializeUserTamedDinos(user, tamedDinos);
                    CPH.LogInfo($"[DinoTame] {user}'s tamed dinos have been updated (missing stats added).");
                }
                else
                {
                    CPH.LogInfo($"[DinoTame] {user}'s tamed dinos already have all stats set.");
                }
            }
        }
        return true;
    }
    public bool openArena()
    {
        if (arenaOpen)
        {
            CPH.LogWarn("[DinoTame] Arena is already open.");
            return false;
        }
        string json = JsonConvert.SerializeObject(new Dictionary<string, FighterEntry>());
        CPH.SetGlobalVar("DinoTame_Fighters", json, false);
        CPH.TryGetArg("fighterArenaOpenMessage", out string message);
        CPH.SendMessage(message);
        arenaOpen = true;
        return true;
    }
    public bool fightArena()
    {
        arenaOpen = false;
        CPH.DisableTimer("[DinoTame] Arena Entry Timer");
        string message;
        int pot = CPH.GetGlobalVar<int>("DinoTame_ArenaPot", false);
        string json = CPH.GetGlobalVar<string>("DinoTame_Fighters", false);
        Dictionary<string, FighterEntry> fighters = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        json = null;

        if (fighters == null || fighters.Count < 2)
        {
            CPH.LogWarn("[DinoTame] Not enough fighters to start the arena fight.");
            CPH.TryGetArg("fighterArenaNotEnoughFightersMessage", out message);
            CPH.SendMessage(message);
            CPH.SetTwitchUserVar(fighters.First().Value.user, "dinoTame_egg_paste", CPH.GetTwitchUserVar<int>(fighters.First().Value.user, "dinoTame_egg_paste", true) + pot, true);
            clearFighters(300);
            return false;
        }

        CPH.TryGetArg("fighterArenaFightMessage", out message);
        CPH.SendMessage(message);

        Dictionary<string, FighterEntry> aliveFighters = new Dictionary<string, FighterEntry>(fighters);

        while (aliveFighters.Count > 1)
        {
            var fighterKeys = aliveFighters
                .Where(x => x.Value.currentHealth > 0)
                .Select(x => x.Key)
                .ToList();

            foreach (var fighterKey in fighterKeys)
            {
                if (!aliveFighters.TryGetValue(fighterKey, out var fighter)) continue;
                fighter = aliveFighters[fighterKey];
                int randomIndex = rnd.Next(0, aliveFighters.Count);
                var randomFighter = aliveFighters.ElementAt(randomIndex);

                while (fighterKey == randomFighter.Key || string.IsNullOrEmpty(randomFighter.Key))
                {
                    CPH.LogInfo($"[DinoTame] Fighter {fighter.user} ({fighter.dino}) cannot attack themselves.");
                    randomIndex = rnd.Next(0, aliveFighters.Count);
                    randomFighter = aliveFighters.ElementAt(randomIndex);
                }

                double fightCritDmg = 3; // Crit damage multiplier
                bool userCrit = false;
                double dmg = 0;

                if (fighter.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {fighter.user} ({fighter.dino}) is already defeated.");
                    continue; // Skip if the fighter is already defeated
                }

                if (FightCritChance())
                {
                    dmg = fighter.damage * fightCritDmg; // Crit damage
                    userCrit = true;
                }
                else
                {
                    dmg = fighter.damage; // Normal damage
                }
                randomFighter.Value.currentHealth = Math.Max(0, randomFighter.Value.currentHealth - dmg);
                CPH.LogInfo($"[DinoTame] {fighter.user} ({fighter.dino}) attacks {randomFighter.Value.user} ({randomFighter.Value.dino}) for {dmg} damage. Crit: {userCrit}");
                updateFighter(fighterKey, randomFighter.Key, randomFighter.Value, dmg, userCrit);
                if (randomFighter.Value.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {randomFighter.Value.user} ({randomFighter.Value.dino}) was defeated.");
                    aliveFighters.Remove(randomFighter.Key);
                    UpdateWinLoseCounts(randomFighter.Value.user, randomFighter.Value.dino, false);
                    //continue;
                }
                CPH.LogInfo("[DinoTame] Waiting for next fighter action...");
                CPH.Wait(2000); // Wait for 2 seconds before the next action
            }
        }

        CPH.LogInfo("[DinoTame] Arena fight finished.");
        if (aliveFighters.Count == 1)
        {
            var winner = aliveFighters.First();
            CPH.TryGetArg("fighterArenaWinnerMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", winner.Value.user },
                { "dino", winner.Value.dino },
                { "fighterArenaPot", pot}
            });
            CPH.SendMessage(message);
            UpdateWinLoseCounts(winner.Value.user, winner.Value.dino, true);
            int winnerCurrentEggPaste = CPH.GetTwitchUserVar<int>(winner.Value.user, "dinoTame_egg_paste", true);
            int winnerNewEggPaste = winnerCurrentEggPaste + pot;
            CPH.SetTwitchUserVar(winner.Value.user, "dinoTame_egg_paste", winnerNewEggPaste, true);
            CPH.SetGlobalVar("DinoTame_ArenaPot", 0, false);

        }
        else
        {
            CPH.TryGetArg("fighterArenaNoWinnerMessage", out message);
            CPH.SendMessage(message);
        }
        CPH.Wait(10000); // Wait for 10 seconds before cleaning up
        clearFighters();

        return true;
    }
    public bool addFighter()
    {
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("fighterArenaEggPasteCost", out int eggPasteCost);

        string message;
        if (!arenaOpen)
        {
            CPH.LogWarn("[DinoTame] Arena is not open. Cannot add fighter.");
            CPH.TryGetArg("fighterArenaNotOpenMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }
        string json = CPH.GetGlobalVar<string>("DinoTame_Fighters", false);
        Dictionary<string, FighterEntry> fighters = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        json = null; // Clear the json variable to free memory
        if (fighters.ContainsKey(userId))
        {
            CPH.LogWarn($"[DinoTame] {user} is already a fighter in the arena.");
            CPH.TryGetArg("fighterArenaAlreadyFighterMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        string dino = GetRandomTamedDino(user);
        if (string.IsNullOrEmpty(dino))
        {
            CPH.LogWarn($"[DinoTame] {user} has no tamed dinos to fight with.");
            CPH.TryGetArg("fighterArenaNoDinosMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        if (eggPasteCost > 0)
        {
            int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
            if (eggPaste < eggPasteCost)
            {
                CPH.LogWarn($"[DinoTame] {user} does not have enough egg paste to enter the arena.");
                CPH.TryGetArg("fighterArenaNotEnoughEggPasteMessage", out message);
                message = ReplaceWithArgs(message, new Dictionary<string, object>
                {
                    { "user", user },
                    { "eggPasteCost", eggPasteCost },
                    { "eggPaste", eggPaste }
                });
                CPH.SendMessage(message);
                return false;
            }
            CPH.SetTwitchUserVar(user, "dinoTame_egg_paste", eggPaste - eggPasteCost, true);
            int arenaPot = Math.Max(CPH.GetGlobalVar<int>("DinoTame_ArenaPot", false), 0);
            CPH.SetGlobalVar("DinoTame_ArenaPot", arenaPot + eggPasteCost, false);
        }

        if (fighters == null || fighters.Count == 0)
        {
            CPH.EnableTimer("[DinoTame] Arena Entry Timer");
            CPH.LogInfo("[DinoTame] Fighters list is empty, enabling arena entry timer.");
            CPH.TryGetArg("fighterArenaFirstEntryMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "dino", dino }
            });
        }
        else
        {
            CPH.LogInfo("[DinoTame] Fighters list exists, no need to enable entry timer.");
            CPH.TryGetArg("fighterArenaEntryMessage", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "dino", dino }
            });
        }
        CPH.SendMessage(message);

        Dictionary<string, TamedDinoEntry> userTamedDinos = DeserializeUserTamedDinos(user);

        var payload = new
        {
            type = "addFighter",
            id = userId,
            playerName = user,
            dinoName = dino,
            iconUrl = $"{dinoIconPath}\\{dino}.png",
            currentHP = userTamedDinos[dino].health,
            maxHP = userTamedDinos[dino].health
        };
        json = JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);
        json = null;

        fighters.Add(userId, new FighterEntry
        {
            user = user,
            dino = dino,
            currentHealth = userTamedDinos[dino].health,
            maxHealth = userTamedDinos[dino].health,
            damage = userTamedDinos[dino].damage
        });
        json = JsonConvert.SerializeObject(fighters);
        CPH.SetGlobalVar("DinoTame_Fighters", json, false);
        return true;
    }
    public bool updateFighter(string attackerId, string defenderId, FighterEntry updatedFighter, double dmg, bool crit = false)
    {
        if (updatedFighter == null)
        {
            CPH.LogWarn("[DinoTame] No fighter data provided to update.");
            return false;
        }
        var payload = new
        {
            type = "attackFighter",
            attackerId = attackerId,
            id = defenderId,
            currentHP = updatedFighter.currentHealth,
            maxHP = updatedFighter.maxHealth,
            damage = dmg,
            critDmg = crit
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);
        return true;
    }
    public bool clearFighters(int duration = 0)
    {
        if (duration == 0)
        {
            duration = rnd.Next(1567, 3286); // Random duration between 1567 and 3286 seconds
        }
        CPH.SetTimerInterval("aa55a587-5eb1-42a2-9861-d10e983a749e", duration);
        CPH.DisableTimer("[DinoTame] Fight Request Timer");
        CPH.EnableTimer("[DinoTame] Arena CleanUp");
        CPH.LogInfo("[DinoTame] Clearing fighters from the arena.");
        CPH.UnsetGlobalVar("DinoTame_Fighters", false);
        CPH.WebsocketBroadcastJson("{\"type\":\"clearFighters\"}");
        return true;
    }
    public bool isStreamLive()
    {
        // Check if the stream is live
        return CPH.GetGlobalVar<bool>("isLive", false);
    }
    
}