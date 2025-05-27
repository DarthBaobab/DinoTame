using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;
using Streamer.bot.Common.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using System.Globalization;

public class CPHInline : CPHInlineBase
{
    private List<DinoEntry> dinoList = new List<DinoEntry>();
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

    // Helper class for deserialization
    public class DinoEntry
    {
        public string name { get; set; }
        public double spawn_chance { get; set; }
        public double tame_chance { get; set; }
        public List<string> maps { get; set; }
        public string icon { get; set; }
    }

    class TameEntry
    {
        public string user { get; set; }
        public string kibbleType { get; set; }
    }

    public bool Execute()
    {
        if (isInitialized)
        {
            CPH.LogInfo($"[DinoTame] Already Initialized");
            return false;
        }

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



        if (string.IsNullOrEmpty(obsSceneName) || string.IsNullOrEmpty(obsSourceGroup) || 
            string.IsNullOrEmpty(obsSourceName) || string.IsNullOrEmpty(obsSourceIcon) || string.IsNullOrEmpty(obsSourceSpawnChanceTitle) ||
            string.IsNullOrEmpty(obsSourceSpawnChance) || string.IsNullOrEmpty(obsSourceTameChanceTitle) || string.IsNullOrEmpty(obsSourceTameChance) ||
            string.IsNullOrEmpty(obsSourceProgressBar) || string.IsNullOrEmpty(obsSourceProgressBarPath) || tameDuration <= 0  || string.IsNullOrEmpty(dinoIconPath))
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
        bool enablemaps = args.ContainsKey("enablemaps") && args["enablemaps"] is bool b && b;
        if (enablemaps)
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

        Random rnd = new Random();
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
        string message;
        Dictionary<string, int> kibbleCosts = new Dictionary<string, int>()
        {
            {
                "basic",
                1
            },
            {
                "simple",
                2
            },
            {
                "regular",
                4
            },
            {
                "superior",
                8
            },
            {
                "exceptional",
                16
            },
            {
                "extraordinary",
                32
            }
        };
        if (!tamingActive)
        {
            CPH.TryGetArg("useKibbleMessageNotActive", out message);
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

        // Kibble-Werte
        Dictionary<string, double> kibbleBonus = new()
        {
            {
                "basic",
                0.5
            },
            {
                "simple",
                0.75
            },
            {
                "regular",
                1.0
            },
            {
                "superior",
                1.25
            },
            {
                "exceptional",
                1.5
            },
            {
                "extraordinary",
                1.75
            }
        };
        Random rnd = new Random();
        List<string> winners = new();
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
            double roll = rnd.NextDouble() * 100;
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

    public bool GetKibbleCount()
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

    public bool GetUserTamedDino()
    {
        string user = args["user"].ToString();
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return false;
        }

        // JSON in Liste umwandeln
        List<string> tamedDinos = JsonConvert.DeserializeObject<List<string>>(json);

        // Abfragen, ob der Dino drin ist (Groß-/Kleinschreibung beachten!)
        bool alreadyTamed = tamedDinos.Contains(currentDino);

        return alreadyTamed;
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
        List<string> tamedDinos = string.IsNullOrEmpty(json) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(json);

        // Prüfen, ob der Dino bereits getamed wurde
        if (tamedDinos.Contains(currentDino))
        {
            CPH.LogWarn($"[DinoTame] {user} hat den Dino {currentDino} bereits getamed.");
            return false;
        }

        // Dino hinzufügen
        tamedDinos.Add(currentDino);
        CPH.SetTwitchUserVar(user, "dinoTame_TamedDinos", JsonConvert.SerializeObject(tamedDinos), true);
        CPH.LogInfo($"[DinoTame] {user} hat den Dino {currentDino} erfolgreich getamed.");
        return true;
    }

    public bool CheckUserTamedDino()
    {
        string user = args["user"].ToString();
        bool alreadyTamed = GetUserTamedDino();
        string message = string.Empty;

        if (!tamingActive)
        {
            CPH.TryGetArg("checkUserTamedMessageNotActive", out message);
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
        if (!isInitialized)
        {
            CPH.LogWarn("[DinoTame] Plugin not initialized. Cannot update OBS overlay.");
            return;
        }

        if (tamingActive)
        {
            CPH.ObsSetGdiText(obsSceneName, obsSourceName, currentDino);
            CPH.ObsSetGdiText(obsSceneName, obsSourceSpawnChanceTitle, "Spawn Chance:");
            CPH.ObsSetGdiText(obsSceneName, obsSourceSpawnChance, currentDinoSpawnChance.ToString("F2") + "%");
            CPH.ObsSetGdiText(obsSceneName, obsSourceTameChanceTitle, "Tame Chance:");
            CPH.ObsSetGdiText(obsSceneName, obsSourceTameChance, currentDinoTameChance.ToString("F2") + "%");
            CPH.ObsSetImageSourceFile(obsSceneName, obsSourceIcon, dinoIconPath + "\\" + currentDinoIcon);
            CPH.ObsSetBrowserSource(obsSceneName, obsSourceProgressBar, $"file:///{obsSourceProgressBarPath}?duration={tameDuration}");

            CPH.ObsShowSource(obsSceneName, obsSourceGroup);
        }
        else if (!tamingActive)
        {
            CPH.ObsHideSource(obsSceneName, obsSourceGroup);
        }
        else 
        {
            CPH.LogError("[DinoTame] OBS overlay cannot be updated.");
        }
    }


}