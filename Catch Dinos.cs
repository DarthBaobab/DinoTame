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
    private List<DinoEntry> dinoList;
    private string dinosFilePath;
    private bool isInitialized = false;

    // Helper class for deserialization
    public class DinoEntry
    {
        public string name { get; set; }
        public double spawn_chance { get; set; }
        public double catch_chance { get; set; }
        public List<string> maps { get; set; }
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
            CPH.LogInfo($"[DinoCatch] Already Initialized");
            return false;
        }

        dinosFilePath = args.ContainsKey("DinosFilePath") ? args["DinosFilePath"].ToString() : "Dinos.json";

        if (!System.IO.File.Exists(dinosFilePath))
        {
            CPH.LogWarn($"[DinoCatch] File '{dinosFilePath}' not found.");
            return false;
        }

        string json = System.IO.File.ReadAllText(dinosFilePath);
        try
        {
            dinoList = JsonConvert.DeserializeObject<List<DinoEntry>>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[DinoCatch] Failed to parse JSON: {ex.Message}");
            return false;
        }

        // Filter out entries that have no maps or empty maps
        dinoList = dinoList.Where(d => d.maps != null && d.maps.Any(m => !string.IsNullOrWhiteSpace(m))).ToList();
        if (dinoList == null || dinoList.Count == 0)
        {
            CPH.LogWarn("[DinoCatch] No dinos with maps found in the JSON file.");
            return false;
        }

        CPH.SetGlobalVar("catchDinos_TamingEntries", "[]", true);
        CPH.SetGlobalVar("catchDinos_CurrentDino", "", true);
        CPH.SetGlobalVar("catchDinos_CurrentDinoCatchChance", 0.0, true);
        CPH.LogInfo($"[DinoCatch] Initialized");
        isInitialized = true;
        return true;
    }

    public bool SelectRandomDino()
    {
        var filteredList = dinoList;
        // Optional: filter by maps if enabled and argument provided (support multiple maps, comma or semicolon separated)
        bool enablemaps = args.ContainsKey("Enablemaps") && args["Enablemaps"] is bool b && b;
        if (enablemaps)
        {
            if (args.ContainsKey("Map"))
            {
                string mapArg = args["Map"].ToString();
                if (string.IsNullOrWhiteSpace(mapArg))
                {
                    CPH.LogWarn("[DinoCatch] Map argument is empty.");
                    return false;
                }

                var maps = mapArg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList();
                if (maps.Count == 0)
                {
                    CPH.LogWarn("[DinoCatch] No valid maps provided in Map argument.");
                    return false;
                }

                filteredList = filteredList.Where(d => d.maps != null && d.maps.Any(dm => maps.Contains(dm, StringComparer.OrdinalIgnoreCase))).ToList();
                if (filteredList.Count == 0)
                {
                    CPH.LogWarn($"[DinoCatch] No dinos found for maps: {string.Join(", ", maps)}.");
                    return false;
                }
            }
        }

        // Weighted random selection based on spawn_chance
        double totalWeight = filteredList.Sum(d => d.spawn_chance);
        if (totalWeight <= 0)
        {
            CPH.LogWarn("[DinoCatch] Total spawn chance is zero or negative.");
            return false;
        }

        Random rnd = new Random();
        double roll = rnd.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var dino in filteredList)
        {
            cumulative += dino.spawn_chance;
            if (roll <= cumulative)
            {
                CPH.LogInfo($"[DinoCatch] Randomly selected dino: {dino.name} (spawn_chance: {dino.spawn_chance}, catch_chance: {dino.catch_chance})");
                CPH.SetGlobalVar("catchDinos_CurrentDino", dino.name, true);
                CPH.SetGlobalVar("catchDinos_CurrentDinoCatchChance", dino.catch_chance, true);
                return true;
            }
        }

        CPH.LogWarn("[DinoCatch] Failed to select a dino.");
        return false;
    }

    public bool SpawnDino()
    {
        if (!SelectRandomDino())
        {
            CPH.LogWarn("[DinoCatch] Failed to select a random dino.");
            return false;
        }

        string currentDino = CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true);
        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogWarn("No dino selected to spawn.");
            return false;
        }

        CPH.LogInfo($"[DinoCatch] Spawning dino: {currentDino}");
        CPH.SetGlobalVar("catchDinos_TamingActive", true, false);
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
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "egg_paste", true);
        int eggPasteAmountToAdd = args.ContainsKey("eggPasteAmountToAdd") ? int.Parse(args["eggPasteAmountToAdd"].ToString()) : 10;
        eggPaste += eggPasteAmountToAdd;
        CPH.SetTwitchUserVar(user, "egg_paste", eggPaste, true);
        CPH.TryGetArg("buyEggPasteMessage", out string message);
        message = ReplaceWithArgs(message, new Dictionary<string, object>
        {
            { "user", user },
            { "eggPaste", eggPaste },
            { "eggPasteAmountToAdd", eggPasteAmountToAdd }
        });
        CPH.SendMessage(message);
        CPH.LogInfo($"[DinoCatch] Buy Egg Paste {user} | {eggPaste - eggPasteAmountToAdd} | +{eggPasteAmountToAdd} | {eggPaste}");
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
        string message = string.Empty;
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
        if (!CPH.GetGlobalVar<bool>("catchDinos_TamingActive", false))
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
        int currentPaste = CPH.GetTwitchUserVar<int>(user, "egg_paste", true);
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
                { "currentDino", CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true) }
            });
            CPH.SendMessage(message);
            return false;
        }

        // Optional: prüfen ob User bereits mitgemacht hat
        var json = CPH.GetGlobalVar<string>("catchDinos_TamingEntries", true);
        var list = string.IsNullOrEmpty(json) ? new List<TameEntry>() : JsonConvert.DeserializeObject<List<TameEntry>>(json);
        if (list.Any(e => e.user.Equals(user, StringComparison.OrdinalIgnoreCase)))
        {
            CPH.TryGetArg("useKibbleMessageAlreadyTaming", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        list.Add(new TameEntry { user = user, kibbleType = input });
        CPH.SetGlobalVar("catchDinos_TamingEntries", JsonConvert.SerializeObject(list), true);
        // Kibble abziehen
        CPH.SetTwitchUserVar(user, "egg_paste", currentPaste - cost, true);
        string currentDino = CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true);
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
        string currentDino = CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true);
        double baseCatchChance = CPH.GetGlobalVar<double>("catchDinos_CurrentDinoCatchChance", true);
        string json = CPH.GetGlobalVar<string>("catchDinos_TamingEntries", true);
        CPH.SetGlobalVar("catchDinos_TamingActive", false, false);

        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogError("[DinoCatch] Fehler bei der Auswertung: Daten fehlen.");
            return false;
        }
        else if (baseCatchChance < 0 || baseCatchChance > 100)
        {
            CPH.LogError("[DinoCatch] Ungültige Fangchance für den Dino.");
            return false;
        }
        else if (json == "[]" || string.IsNullOrEmpty(json))
        {
            CPH.LogError("[DinoCatch] Es wurden keine Kibble gespendet.");
            return false;
        }

        List<TameEntry> entries;
        try
        {
            entries = JsonConvert.DeserializeObject<List<TameEntry>>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn("[DinoCatch] Fehler beim Parsen von catchDinos_TamingEntries: " + ex.Message);
            return false;
        }

        if (entries == null || entries.Count == 0)
        {
            CPH.LogError("[DinoCatch] Niemand hat versucht, den Dino zu zähmen.");
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
        foreach (var entry in entries)
        {
            string user = entry.user;
            string kibble = entry.kibbleType.ToLower();
            if (!kibbleBonus.TryGetValue(kibble, out double bonus))
            {
                CPH.LogWarn($"[DinoCatch] Ungültiger Kibble-Typ von {user}: {entry.kibbleType}");
                continue;
            }

            double finalChance = baseCatchChance * bonus;
            finalChance = Math.Min(finalChance, 100); // Maximal 100%
            double roll = rnd.NextDouble() * 100;
            bool success = roll <= finalChance;
            CPH.LogInfo($"[DinoCatch] {user} | Kibble: {kibble} | Chance: {finalChance:F1}% | Roll: {roll:F1}% | Success: {success}");
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
        CPH.SetGlobalVar("catchDinos_TamingEntries", "[]", true);
        CPH.SetGlobalVar("catchDinos_CurrentDino", "", true);
        CPH.SetGlobalVar("catchDinos_CurrentDinoCatchChance", 0.0, true);
        return true;
    }

    public bool GetKibbleCount()
    {
        string user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "egg_paste", true);
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
        string dinoName = CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true);
        string json = CPH.GetTwitchUserVar<string>(user, "catchDinos_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return false;
        }

        // JSON in Liste umwandeln
        List<string> tamedDinos = JsonConvert.DeserializeObject<List<string>>(json);

        // Abfragen, ob der Dino drin ist (Groß-/Kleinschreibung beachten!)
        bool alreadyTamed = tamedDinos.Contains(dinoName);

        return alreadyTamed;
    }

    public bool AddUserTamedDino(string user)
    {
        string dinoName = CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true);
        if (string.IsNullOrEmpty(dinoName))
        {
            CPH.LogWarn("[DinoCatch] Kein aktueller Dino zum Hinzufügen.");
            return false;
        }

        // Aktuelle Tamed Dinos des Users abrufen
        string json = CPH.GetTwitchUserVar<string>(user, "catchDinos_TamedDinos", true);
        List<string> tamedDinos = string.IsNullOrEmpty(json) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(json);

        // Prüfen, ob der Dino bereits getamed wurde
        if (tamedDinos.Contains(dinoName))
        {
            CPH.LogWarn($"[DinoCatch] {user} hat den Dino {dinoName} bereits getamed.");
            return false;
        }

        // Dino hinzufügen
        tamedDinos.Add(dinoName);
        CPH.SetTwitchUserVar(user, "catchDinos_TamedDinos", JsonConvert.SerializeObject(tamedDinos), true);
        CPH.LogInfo($"[DinoCatch] {user} hat den Dino {dinoName} erfolgreich getamed.");
        return true;
    }

    public bool CheckUserTamedDino()
    {
        string user = args["user"].ToString();
        bool alreadyTamed = GetUserTamedDino();
        string message = string.Empty;

        if (!CPH.GetGlobalVar<bool>("catchDinos_TamingActive", false))
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
                { "currentDino", CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true) }
            });
        }
        else
        {
            CPH.TryGetArg("checkUserTamedMessageNo", out message);
            message = ReplaceWithArgs(message, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", CPH.GetGlobalVar<string>("catchDinos_CurrentDino", true) }
            });
        }

        CPH.SendMessage(message);
        return true;
    }
}