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


public class CPHInline : CPHInlineBase
{
    private static readonly Random rnd = new Random();
    private List<DinoEntry> dinoList = new List<DinoEntry>();
    private Dictionary<string, List<DinoEntry>> dinoBossList = new Dictionary<string, List<DinoEntry>>();
    private Dictionary<string, WeaponStats> weaponStatsTorpor;
    private Dictionary<string, WeaponStats> weaponStatsNormal;
    private Dictionary<string, double> DinoStatsMultipliers = new Dictionary<string, double>();
    private DinoEntry currentDino;
    private int currentDinoLevel;
    private Dictionary<string, KibbleInfo> kibbleInfo;
    private Dictionary<string, int> negativeTameEvent;
    private int tameDuration;
    private int arenaJoinDuration;
    private bool tamingActive = false;
    private List<TameEntry> tameEntries = new List<TameEntry>();
    private bool arenaReady = false;
    private bool arenaOpen = false;
    private bool bossfightOpen = false;
    private string dinosFilePath;
    private string dinoIconPath;
    private string saveFolderPath;
    private DinoTameMessages messages = new DinoTameMessages();
    private bool debug;

    // Helper class for deserialization
    public class DinoEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Varianten { get; set; }
        public string Boss { get; set; }
        public List<string> Maps { get; set; }
        public DinoStats Stats { get; set; }
    }
    public class DinoStats
    {
        public StatValue Health { get; set; }
        public StatValue Stamina { get; set; }
        public StatValue Oxygen { get; set; }
        public StatValue Food { get; set; }
        public StatValue Weight { get; set; }
        public StatValue Damage { get; set; }
        public StatValue Speed { get; set; }
        public StatValue Torpor { get; set; }
    }
    public class DinoStatsMultiplier
    {
        public double Health { get; set; } = 0.2;
        public double Stamina { get; set; } = 1;
        public double Oxygen { get; set; } = 1;
        public double Food { get; set; } = 1;
        public double Weight { get; set; } = 1;
        public double Damage { get; set; } = 1;
        public double Speed { get; set; } = 1;
        public double Torpor { get; set; } = 1;
    }
    public class StatValue
    {
        public int Base { get; set; }
        public int Wild { get; set; }
        public int Tame { get; set; }
    }
    public class WeaponStats
    {
        public int Damage { get; set; }
        public int Torpor { get; set; }
        public int Durability { get; set; }
    }
    public class TamedDinoEntry
    {
        public string name { get; set; }
        public string tame_date { get; set; }
        public int levelWild { get; set; }
        public double effectiveness { get; set; }
        public int level { get; set; }
        public int health { get; set; }
        public int stamina { get; set; }
        public int oxygen { get; set; }
        public int food { get; set; }
        public int weight { get; set; }
        public int damage { get; set; }
        public int speed { get; set; }
        public int win { get; set; }
        public int lose { get; set; }
    }
    public class TameEntry
    {
        public string user { get; set; }
        public string kibbleType { get; set; }
        public KeyValuePair<string, WeaponStats> weapon { get; set; }
        public int weaponQuality { get; set; }
    }
    public class TameResult
    {
        public string user { get; set; }
        public string dino { get; set; }
        public double effectiveness { get; set; }
        public int level { get; set; }
        public int levelWild { get; set; }
    }
    public class KibbleInfo
    {
        public double Power { get; set; }
        public int Cost { get; set; }
        public double SafetyChance { get; set; } = 0.8;
    }    
    public class FighterEntry
    {
        public int userId { get; set; }
        public string user { get; set; }
        public int dinoId { get; set; }
        public string dino { get; set; }
        public int currentHealth { get; set; }
        public int maxHealth { get; set; }
        public int damage { get; set; }
    }
    
    public class DinoTameMessages
    {
        public string BuyEggPasteMessage { get; } 
            = "@{user}, du hast {eggPasteAmountToAdd} Eier-Paste gekauft und besitzt jetzt {eggPaste}.";

        public string CheckUserTamedMessageYes { get; } 
            = "@{user}, du hast {currentDino} bereits gezähmt! Deine Auflistung findest du hier: https://tinyurl.com/DinoTameViewer?username={user}";

        public string CheckUserTamedMessageNo { get; } 
            = "@{user}, du hast {currentDino} noch nicht gezähmt! Deine Auflistung findest du hier: https://tinyurl.com/DinoTameViewer?username={user}";

        public string CheckUserTamedMessageNotActive { get; } 
            = "@{user}, du hast {countTamed} von {countDinos} Dinos gezähmt! Deine Auflistung findest du hier: https://tinyurl.com/DinoTameViewer?username={user}";

        public string FightMessageNoDinos { get; } 
            = "@{user}, du hast keine Dinos.";

        public string FightMessageNoTarget { get; } 
            = "@{user}, bitte gib an, wen du herausfordern möchtest.";

        public string FightMessageNoUser { get; } 
            = "@{user}, {targetUser} scheint es nicht zu geben!";

        public string FightMessageSelf { get; } 
            = "@{user}, du kannst dich nicht selbst herausfordern!";

        public string FightMessageTargetNoDinos { get; } 
            = "@{user}, {targetUser} hat noch keine Dinos.";

        public string FightMessage { get; } 
            = "@{targetUser}, {user} fordert dich mit einem {requestUserDino} heraus. Möchtest du annehmen? !annehmen oder !ablehnen";

        public string FightMessageDraw { get; } 
            = "@{requestUser} @{targetUser}, Unentschieden! Ihr habt euch gegenseitig besiegt.";

        public string FightMessageWin { get; } 
            = "@{requestUser}, Glückwünsch! Du hast {targetUser} erfolgreich besiegt!";

        public string FightMessageLose { get; } 
            = "@{targetUser}, Glückwünsch! Du hast {requestUser} erfolgreich besiegt!";

        public string FightMessageReject { get; } 
            = "@{requestUser}, {targetUser} hat deine Herausforderung abgelehnt.";

        public string FightMessageAccept { get; } 
            = "Lasst den Kampf beginnen! {requestUser} mit {requestUserDino} vs {targetUser} mit {targetUserDino}";

        public string GetEggPasteMessage { get; } 
            = "@{user} du besitzt {eggPaste} Eier-Paste.";

        public string PickWinnerMessage { get; } 
            = "Ergebnis der Zähmung {currentDino}: {winners} | {losers}";

        public string PickWinnerMessageNoEntries { get; } 
            = "Keiner hat versucht einen {currentDino} zu zähmen.";

        public string PickWinnerMessageFail { get; } 
            = "😢 Der {currentDino} konnte von niemandem gezähmt werden.";

        public string PickWinnerMessageDinoNotKnockedOut { get; } 
            = "{currentDino} konnte nicht betäubt werden.";

        public string PickWinnerMessageDinoKilled { get; } 
            = "{currentDino} wurde beim Versuch ihn zu betäuben getötet.";

        public string SetDefaultKibbleMessageInvalidType { get; } 
            = "@{user}, bitte gib einen gültigen Kibble-Typ an: basic, simple, regular, superior, exceptional, extraordinary";

        public string SetDefaultKibbleMessageSuccess { get; } 
            = "@{user}, dein Standard Kibble wurde auf {input} geändert";

        public string SpawnDinoMessage { get; } 
            = "🐉 Macht das Kibble bereit! Wir machen uns auf den Weg einen {currentDino} Level {currentDinoLevel} zu zähmen!";

        public string UseKibbleMessageNotActive { get; } 
            = "Aktuell ist keine Zähmung aktiv.";

        public string UseKibbleMessageInvalidType { get; } 
            = "@{user}, bitte gib einen gültigen Kibble-Typ an.";

        public string UseKibbleMessageNotEnoughPaste { get; } 
            = "@{user}, du hast nicht genug Eier-Paste ({currentPaste}/{cost})!";

        public string UseKibbleMessageAlreadyTaming { get; } 
            = "@{user}, du bist bereits am Zähmen.";

        public string UseKibbleMessageSuccess { get; } 
            = "@{user} nutzt {weapon} mit {weaponQuality}% und {input} Kibble um {currentDino} zu zähmen! ({currentPaste} übrig)";

        public string UseKibbleMessageAlreadyTamed { get; } 
            = "@{user}, du hast {currentDino} bereits gezähmt!";

        public string FighterArenaNotOpenMessage { get; } 
            = "@{user}, die Arena wird aktuell gereinigt!";

        public string FighterArenaNoDinosMessage { get; } 
            = "@{user}, du hast leider keine gezähmten Dinos.";

        public string FighterArenaFirstEntryMessage { get; } 
            = "{user} startet die Arena mit einem {dino}! Mit > !arena < geht's los!";

        public string FighterArenaEntryMessage { get; } 
            = "{user} nimmt mit einem {dino} an der Arena teil!";

        public string FighterArenaAlreadyFighterMessage { get; } 
            = "@{user}, du nimmst bereits an der Arena teil.";

        public string FighterArenaNotEnoughEggPasteMessage { get; } 
            = "@{user}, du hast nicht genug Eier-Paste. {eggPasteCost}/{eggPaste}.";

        public string FighterArenaOpenMessage { get; } 
            = "Die Arena ist wieder einsatzbereit!";

        public string FighterArenaFightMessage { get; } 
            = "Lasst die Kämpfe beginnen!";

        public string FighterArenaNotEnoughFightersMessage { get; } 
            = "Es gab nicht genügend Teilnehmer. Der Kampf wurde abgesagt!";

        public string FighterArenaWinnerMessage { get; } 
            = "@{user} hat die Arena mit einem {dino} gewonnen und bekommt {fighterArenaPot} Eier-Paste!";

        public string FighterArenaNoWinnerMessage { get; } 
            = "Es gibt keinen Gewinner. #SCAM";

        public string FighterBossArenaOpenMessage { get; } 
            = "Es wird Zeit für den Boss Kampf! {eggPasteToWin} Eier-Paste wird auf alle Gewinner aufgeteilt! Meldet euch mit > !arena < an! (Mehrfach möglich!!!)";

        public string FighterBossArenaFightMessage { get; } 
            = "Lasst den Boss Kampf beginnen!";

        public string FighterBossArenaNotEnoughFightersMessage { get; } 
            = "Es gab nicht genügend Teilnehmer. Der Boss Kampf wurde abgesagt!";

        public string FighterBossArenaWinnerMessage { get; } 
            = "Der Boss ist tot! {fighterBossArenaPot} wird aufgeteilt an: {winners}";

        public string FighterBossArenaNoWinnerMessage { get; } 
            = "Ihr konntet den Boss nicht besiegen. Nächstes Mal besser vorbereiten!";
    }

    
    public bool Execute()
    {
        CPH.TryGetArg("debug", out debug);
        CPH.TryGetArg("tameDuration", out tameDuration);
        CPH.TryGetArg("arenaJoinDuration", out arenaJoinDuration);
        CPH.TryGetArg("saveFolderPath", out saveFolderPath);
        CPH.TryGetArg("dinoIconPath", out dinoIconPath);

        if (tameDuration <= 0 || string.IsNullOrEmpty(saveFolderPath) || string.IsNullOrEmpty(dinoIconPath))
        {
            CPH.LogWarn("[DinoTame] Arguments are not set correctly.");
            return false;
        }

        currentDino = null;

        dinosFilePath = args.ContainsKey("dinosFilePath") ? args["dinosFilePath"].ToString() : "Dinos.json";

        if (!File.Exists(dinosFilePath))
        {
            CPH.LogWarn($"[DinoTame] File '{dinosFilePath}' not found.");
            return false;
        }

        string json = File.ReadAllText(dinosFilePath);
        try
        {
            dinoList = JsonConvert.DeserializeObject<List<DinoEntry>>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[DinoTame] Failed to parse JSON: {ex.Message}");
            return false;
        }
        // Filter out boss dinos for boss list
        //dinoBossList = dinoList.Where(d => !string.IsNullOrWhiteSpace(d.Boss)).ToList();
        dinoBossList = dinoList
            .GroupBy(d => d.Boss)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Filter out entries that have no maps or empty maps
        dinoList = dinoList.Where(d => d.Maps != null && d.Maps.Any(m => !string.IsNullOrWhiteSpace(m))).ToList();
        if (dinoList == null || dinoList.Count == 0)
        {
            CPH.LogWarn("[DinoTame] No dinos with maps found in the JSON file.");
            return false;
        }
        // Filter out boss dinos for normal taming
        dinoList = dinoList.Where(d => string.IsNullOrWhiteSpace(d.Boss)).ToList();
        if (dinoList == null || dinoList.Count == 0)
        {
            CPH.LogWarn("[DinoTame] No non-boss dinos found in the JSON file.");
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

                dinoList = dinoList.Where(d => d.Maps != null && d.Maps.Any(dm => maps.Contains(dm, StringComparer.OrdinalIgnoreCase))).ToList();
                if (dinoList.Count == 0)
                {
                    CPH.LogWarn($"[DinoTame] No dinos found for maps: {string.Join(", ", maps)}.");
                    return false;
                }
                dinoBossList = dinoBossList
                    .ToDictionary(
                        boss => boss.Key,
                        boss => boss.Value
                            .Where(d => d.Maps != null &&
                                        d.Maps.Any(dm => maps.Contains(dm, StringComparer.OrdinalIgnoreCase)))
                            .ToList()
                    )
                    .Where(x => x.Value.Count > 0) // leere Boss-Einträge entfernen
                    .ToDictionary(x => x.Key, x => x.Value);
            }
        }

        kibbleInfo = new Dictionary<string, KibbleInfo>()
        {
            { "basic",          new KibbleInfo { Power = 1000, Cost = 1   } },
            { "simple",         new KibbleInfo { Power = 2000, Cost = 10   } },
            { "regular",        new KibbleInfo { Power = 3000, Cost = 25  } },
            { "superior",       new KibbleInfo { Power = 4000, Cost = 50  } },
            { "exceptional",    new KibbleInfo { Power = 5000, Cost = 100  } },
            { "extraordinary",  new KibbleInfo { Power = 6000, Cost = 220 } }
        };

        weaponStatsTorpor = new Dictionary<string, WeaponStats>()
        {
            {"Holzknüppel",     new WeaponStats { Damage = 5,  Torpor = 20,  Durability = 40  }},
            {"Steinschleuder",  new WeaponStats { Damage = 14, Torpor = 19,  Durability = 40  }},
            {"Bogen",           new WeaponStats { Damage = 20, Torpor = 90,  Durability = 50  }},
            {"Armbrust",        new WeaponStats { Damage = 35, Torpor = 160, Durability = 100 }},
            {"Compoundbogen",   new WeaponStats { Damage = 35, Torpor = 122, Durability = 55  }},
            {"Flinte",          new WeaponStats { Damage = 26, Torpor = 220, Durability = 70  }}
        };
        weaponStatsNormal = new Dictionary<string, WeaponStats>()
        {
            {"Bogen",           new WeaponStats { Damage = 55,  Torpor = 0,  Durability = 50   }},
            {"Armbrust",        new WeaponStats { Damage = 95,  Torpor = 0,  Durability = 100  }},
            {"Compoundbogen",   new WeaponStats { Damage = 175, Torpor = 0,  Durability = 55   }},
            {"Flinte",          new WeaponStats { Damage = 62,  Torpor = 0,  Durability = 70   }}
        };

        negativeTameEvent = new Dictionary<string, int>()
        {
            {"hittetByYou", 1},
            {"awaken", 1},
            {"eatenByPredator", 1}
        };

        CPH.LogInfo($"[DinoTame] Initialized");
        return true;
    }
    public DinoEntry GetDinoByName(string name)
    {
        // Suche den Dino in der Liste (Groß-/Kleinschreibung ignorieren)
        return dinoList.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    public DinoEntry SelectRandomDino()
    {
        var list = dinoList;
        int i = rnd.Next(0, list.Count);
        var selected = list[i];
        if (selected == null)
        {
            CPH.LogWarn("[DinoTame] Failed to select a dino.");
            return null;
        }
        else
        {
            CPH.LogInfo($"[DinoTame] Randomly selected dino: {selected.Name}");
            return selected;
        }
    }
    public List<DinoEntry> SelectRandomBossDinos()
    {
        int i = rnd.Next(0, dinoBossList.Count);
        var selectedKey = dinoBossList.Keys.ElementAt(i);
        var selected = dinoBossList[selectedKey];
        if (selected == null || selected.Count == 0)
        {
            CPH.LogWarn("[DinoTame] Failed to select boss dinos.");
            return null;
        }
        else
        {
            CPH.LogInfo($"[DinoTame] Randomly selected boss '{selectedKey}' with: {string.Join(", ", selected.Select(d => d.Boss))}");
            return selected;
        }
    }
    public bool SpawnDino()
    {
        if (!isStreamLive())
        {
            CPH.LogWarn("[DinoTame] Stream offline.");
            return false;
        }
        currentDino = SelectRandomDino();
        if (currentDino == null)
        {
            CPH.LogWarn("[DinoTame] Failed to select a random dino.");
            return false;
        }

        if (currentDino == null || string.IsNullOrEmpty(currentDino.Name))
        {
            CPH.LogWarn("No dino selected to spawn.");
            return false;
        }

        currentDinoLevel = rnd.Next(1, 31) * 5; // Level 1-150

        CPH.LogInfo($"[DinoTame] Spawning dino: {currentDino.Name} - Level {currentDinoLevel}");
        tamingActive = true;

        var payload = new
        {
            type = "spawnDino",
            dinoName = currentDino.Name,
            iconUrl = dinoIconPath + "\\" + currentDino.Name + ".png",
            level = currentDinoLevel,
            hp = currentDino.Stats.Health.Base + (currentDino.Stats.Health.Wild * currentDinoLevel),
            dmg = currentDino.Stats.Damage.Base + (currentDino.Stats.Damage.Wild * currentDinoLevel),
            torpor = currentDino.Stats.Torpor.Base + (currentDino.Stats.Torpor.Wild * currentDinoLevel),
            duration = tameDuration
        };
        string json = JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);

        CPH.SetTimerInterval("ad7857a8-07fd-41ae-9b6c-cc36f9c5eab0", tameDuration); // Set timer for taming duration
        CPH.EnableTimer("[DinoTame] Tame Duration");
        //CPH.TryGetArg("spawnDinoMessage", out string message);
        string message = ReplaceWithArgs(messages.SpawnDinoMessage, new Dictionary<string, object>
        {
            { "currentDino", currentDino.Name },
            { "currentDinoLevel", currentDinoLevel }
        });
        CPH.SendMessage(message);
        return true;
    }
    public bool BuyEggPaste()
    {
        var user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        int eggPasteAmountToAdd;

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
        //CPH.TryGetArg("buyEggPasteMessage", out string message);
        string message = ReplaceWithArgs(messages.BuyEggPasteMessage, new Dictionary<string, object>
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
            //CPH.TryGetArg("useKibbleMessageNotActive", out message);
            CPH.SendMessage(messages.UseKibbleMessageNotActive);
            return false;
        }

        string user = args["user"].ToString();
        string input = args["rawInput"].ToString().ToLowerInvariant();
        string defaultKibble = CPH.GetTwitchUserVar<string>(user, "dinoTame_kibble_type", true);
        if (string.IsNullOrEmpty(input))
        {
            if (string.IsNullOrEmpty(defaultKibble))
            {
                defaultKibble = "basic"; // Default Kibble-Typ
            }
            input = defaultKibble;
        }


        if (false && GetUserTamedDino())
        {
            //CPH.TryGetArg("useKibbleMessageAlreadyTamed", out message);
            message = ReplaceWithArgs(messages.UseKibbleMessageAlreadyTamed, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino.Name }
            });
            CPH.SendMessage(message);
            return false;
        }

        // Optional: prüfen ob User bereits mitgemacht hat
        if (tameEntries.Count > 0 && tameEntries.Any(e => e.user.Equals(user, StringComparison.OrdinalIgnoreCase)))
        {
            //CPH.TryGetArg("useKibbleMessageAlreadyTaming", out message);
            message = ReplaceWithArgs(messages.UseKibbleMessageAlreadyTaming, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        if (!kibbleInfo.TryGetValue(input, out KibbleInfo kibble) || string.IsNullOrEmpty(input))
        {
            //CPH.TryGetArg("useKibbleMessageInvalidType", out message);
            message = ReplaceWithArgs(messages.UseKibbleMessageInvalidType, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        int cost = kibble.Cost;
        int currentPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        if (currentPaste < cost)
        {
            //CPH.TryGetArg("useKibbleMessageNotEnoughPaste", out message);
            message = ReplaceWithArgs(messages.UseKibbleMessageNotEnoughPaste, new Dictionary<string, object>
            {
                { "user", user },
                { "currentPaste", currentPaste },
                { "cost", cost }
            });
            CPH.SendMessage(message);
            return false;
        }
        double weaponTypeChance = rnd.NextDouble();
        var weaponStats = weaponStatsTorpor;
        if (weaponTypeChance < 0.25)
        {
            weaponStats = weaponStatsNormal;
        }
        else
        {
            weaponStats = weaponStatsTorpor;
        }
        int weaponId = rnd.Next(0, weaponStats.Count);
        var weapon = weaponStats.ElementAt(weaponId);
        
        // double x = rnd.NextDouble();
        // double scaled = x * x;        // quadratisch -> viele kleine Werte
        // int quality = (int)(100 + scaled * (755 - 100));

        int maxQuality = 350;
        double x = rnd.NextDouble();
        double scaled = Math.Pow(x, 5); // kubisch -> noch mehr kleine Werte als x*x
        int quality = (int)Math.Round(100 + scaled * (maxQuality - 100));
        quality = Math.Max(100, Math.Min(maxQuality, quality));

        tameEntries.Add(new TameEntry { user = user, kibbleType = input, weapon = weapon, weaponQuality = quality });
        // Kibble abziehen
        CPH.SetTwitchUserVar(user, "dinoTame_egg_paste", currentPaste - cost, true);
        //CPH.TryGetArg("useKibbleMessageSuccess", out message);
        message = ReplaceWithArgs(messages.UseKibbleMessageSuccess, new Dictionary<string, object>
        {
            { "user", user },
            { "currentDino", currentDino.Name },
            { "input", input },
            { "weapon", weapon },
            { "weaponQuality", quality },
            { "currentPaste", currentPaste - cost }
        });
        CPH.SendMessage(message);
        return true;
    }
    public bool SetUserDefaultKibble()
    {
        string user = args["user"].ToString();
        string input = args["rawInput"].ToString().ToLowerInvariant();
        string message;
        if (string.IsNullOrEmpty(input))
        {
            input = "basic"; // Default Kibble-Typ
        }
        if (!kibbleInfo.ContainsKey(input))
        {
            //CPH.TryGetArg("setDefaultKibbleMessageInvalidType", out message);
            message = ReplaceWithArgs(messages.SetDefaultKibbleMessageInvalidType, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }
        //CPH.TryGetArg("setDefaultKibbleMessageSuccess", out message);
        message = ReplaceWithArgs(messages.SetDefaultKibbleMessageSuccess, new Dictionary<string, object>
        {
            { "user", user },
            { "input", input }
        });
        CPH.SendMessage(message);
        CPH.SetTwitchUserVar(user, "dinoTame_kibble_type", input, true);
        return true;
    }
    public string KnockOutDino()
    {
        double dinoHealth = currentDino.Stats.Health.Base + (currentDino.Stats.Health.Wild * currentDinoLevel);
        double dinoTorpor = currentDino.Stats.Torpor.Base + (currentDino.Stats.Torpor.Wild * currentDinoLevel);

        // Durability pro User speichern
        Dictionary<string, double> durabilityLeft = new Dictionary<string, double>();

        foreach (var entry in tameEntries)
        {
            durabilityLeft[entry.user] = entry.weapon.Value.Durability / tameEntries.Count;
        }

        // 🔁 Jetzt laufen wir Runden durch, bis KO oder tot
        while (dinoHealth > 0 && dinoTorpor > 0)
        {
            bool atLeastOneHit = false;

            foreach (var entry in tameEntries)
            {
                string user = entry.user;
                string weapon = entry.weapon.Key;
                int weaponQuality = entry.weaponQuality;

                // Wenn der User keine Haltbarkeit mehr hat → skip
                if (durabilityLeft[user] <= 0)
                    continue;

                // Schaden + Torpor pro Schlag
                double qualityMultiplier = weaponQuality / 100.0;
                double damageDealt = entry.weapon.Value.Damage * qualityMultiplier;
                double torporDealt = entry.weapon.Value.Torpor * qualityMultiplier;

                // Schlag ausführen
                dinoHealth -= damageDealt;
                dinoTorpor -= torporDealt;
                durabilityLeft[user] -= 1;
                atLeastOneHit = true;

                CPH.LogInfo($"[DinoTame] {user} trifft den Dino mit {weapon} (Qualität: {weaponQuality}) " +
                            $"für {damageDealt:F1} Schaden und {torporDealt:F1} Betäubung. " +
                            $"HP: {Math.Max(dinoHealth, 0):F1}, Torpor: {Math.Max(dinoTorpor, 0):F1}, " +
                            $"Haltbarkeit: {durabilityLeft[user]:F1}");

                // KO oder tot?
                if (dinoHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Der Dino wurde von {user} getötet.");
                    return "dead";
                }
                if (dinoTorpor <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Der Dino wurde von {user} betäubt.");
                    return "knockedOut";
                }
            }

            // Falls alle Waffen kaputt → Ende
            if (!atLeastOneHit)
            {
                CPH.LogInfo("[DinoTame] Niemand kann mehr zuschlagen, keine Haltbarkeit mehr.");
                break;
            }
        }

        CPH.LogInfo("[DinoTame] Der Dino konnte nicht betäubt werden.");
        return "none";
    }
    public bool EvaluateTaming()
    {
        string message = string.Empty;
        tamingActive = false;
        CPH.DisableTimer("[DinoTame] Tame Duration");

        if (string.IsNullOrEmpty(currentDino.Name))
        {
            CPH.LogError("[DinoTame] Fehler bei der Auswertung: Daten fehlen.");
            return false;
        }

        if (tameEntries == null || tameEntries.Count == 0)
        {
            CPH.LogInfo("[DinoTame] Niemand hat versucht, den Dino zu zähmen.");

            //CPH.TryGetArg("pickWinnerMessageNoEntries", out message);
            message = ReplaceWithArgs(messages.PickWinnerMessageNoEntries, new Dictionary<string, object>
            {
                { "currentDino", currentDino.Name }
            });
            CPH.SendMessage(message);

            return false;
        }

        List<TameResult> winners = new();
        Dictionary<string, List<TameResult>> winnersByEvent = new();
        Dictionary<string, List<string>> losersByEvent = new();
        bool success = true;
        string eventName = "";

        string knockOutResult = KnockOutDino();
        if (knockOutResult == "dead")
        {
            //CPH.TryGetArg("pickWinnerMessageDinoKilled", out message);
            message = ReplaceWithArgs(messages.PickWinnerMessageDinoKilled, new Dictionary<string, object>
            {
                { "currentDino", currentDino.Name }
            });
        }
        else if (knockOutResult == "none")
        {
            //CPH.TryGetArg("pickWinnerMessageDinoNotKnockedOut", out message);
            message = ReplaceWithArgs(messages.PickWinnerMessageDinoNotKnockedOut, new Dictionary<string, object>
            {
                { "currentDino", currentDino.Name }
            });
        }
        else if (knockOutResult == "knockedOut")
        {
            foreach (var entry in tameEntries)
            {
                string user = entry.user;
                string kibbleType = entry.kibbleType.ToLower();
                if (!kibbleInfo.TryGetValue(kibbleType, out KibbleInfo kibble))
                {
                    CPH.LogWarn($"[DinoTame] Ungültiger Kibble-Typ von {user}: {entry.kibbleType}");
                    continue;
                }
                int needed = (int)Math.Ceiling((currentDino.Stats.Food.Base + (currentDino.Stats.Food.Wild * currentDinoLevel)) / kibble.Power);
                double effectiveness = 100.0;
                double sc = kibble.SafetyChance;
                // 3) Münzwürfe
                for (int i = 0; i < needed; i++)
                {
                    double roll = rnd.NextDouble(); // 0.0–1.0

                    if (roll > sc)
                    {
                        // Effektivität sinkt bei diesem "Bad Coin Flip"
                        effectiveness -= rnd.Next(1, 16); // −1 bis −15 %
                        if (effectiveness < 1) { effectiveness = 1; break; }
                    }
                    CPH.LogInfo($"[DinoTame] Münzwurf für {user}, Kibble {kibbleType}, Versuch {i + 1}/{needed}, Roll: {roll:F4}/{sc:F4}, Effektivität: {effectiveness:F1}%");
                    
                    double k = 0.1; // Stärke des Abfalls
                    double chance = sc - sc * k;
                    sc = Math.Max(0.01, chance);
                }
                // 4) Negativen effekt auswürfeln
                if (rnd.NextDouble() < 0.05) //5% Chance
                {
                    List<string> entries = new List<string>();
                    foreach (var negEvent in negativeTameEvent)
                    {
                        for (int i = 0; i < negEvent.Value; i++)
                        {
                            entries.Add(negEvent.Key);
                        }
                    }
                    int eventId = rnd.Next(0, entries.Count);
                    eventName = entries[eventId];
                    
                    if (eventName == "hittetByYou")
                    {
                        effectiveness -= rnd.Next(5, 16); // −5 bis −15 %
                        success = true;
                    }
                    else if (eventName == "awaken")
                    {
                        success = false;
                    }
                    else if (eventName == "eatenByPredator")
                    {
                        success = false;
                    }
                    CPH.LogInfo($"[DinoTame] Negatives Ereignis für {user}: {eventName}, neue Effektivität: {effectiveness:F1}%");
                    if (effectiveness < 1) { effectiveness = 1; }

                }
                CPH.LogInfo($"[DinoTame] {user} | Kibble: {needed}x {kibbleType} | Effectiveness: {effectiveness:F1}% | Success: {success} | Event: {eventName}");
                if (success && eventName == "")
                {
                    int endLevel = currentDinoLevel + (int)(currentDinoLevel * effectiveness / 100 / 2); //50% Bonus auf Level basierend auf Effektivität
                    TameResult tr = new TameResult
                    {
                        user = user,
                        dino = currentDino.Name,
                        effectiveness = effectiveness,
                        levelWild = currentDinoLevel,
                        level = endLevel
                    };
                    winners.Add(tr);
                    AddUserTamedDino(tr);
                }
                else if (success && eventName == "hittetByYou")
                {
                    int endLevel = currentDinoLevel + (int)(currentDinoLevel * effectiveness / 100 / 2); //50% Bonus auf Level basierend auf Effektivität
                    TameResult tr = new TameResult
                    {
                        user = user,
                        dino = currentDino.Name,
                        effectiveness = effectiveness,
                        levelWild = currentDinoLevel,
                        level = endLevel
                    };
                    if (!winnersByEvent.ContainsKey(eventName))
                    {
                        winnersByEvent[eventName] = new List<TameResult>();
                    }
                    winnersByEvent[eventName].Add(tr);
                    AddUserTamedDino(tr);
                }
                else if (!success && eventName != "")
                {
                    if (!losersByEvent.ContainsKey(eventName))
                    {
                        losersByEvent[eventName] = new List<string>();
                    }
                    losersByEvent[eventName].Add(user);                
                }
            }
            if (winners.Count > 0 || losersByEvent.Count > 0)
            {
                List<string> winnersList = new List<string>();
                List<string> winnersByEventList = new List<string>();
                List<string> losersList = new();

                if (winners.Count > 0)
                {
                    foreach (var winner in winners)
                    {
                        CPH.LogInfo($"[DinoTame] Gewinner: {winner.user} hat den Dino {winner.dino} (Level {winner.levelWild}) mit Effektivität {winner.effectiveness:F1}% auf Level {winner.level} getamed.");
                        winnersList.Add($"{winner.user} (Eff. {winner.effectiveness:F1}% / Lvl {winner.level})");
                    }
                    //CPH.TryGetArg("pickWinnerMessage", out message);
                    message = ReplaceWithArgs(messages.PickWinnerMessage, new Dictionary<string, object>
                    {
                        { "currentDino", currentDino.Name + " (Level " + currentDinoLevel + ")" },
                        { "winners", string.Join(", ", winnersList) }
                    });
                    AutoCommitAndPush();
                }
                if (winnersByEvent.Count > 0)
                {
                    // Ergebnisliste für die finale Ausgabe

                    // Events alphabetisch sortieren
                    foreach (var entry in winnersByEvent.OrderBy(e => e.Key))
                    {
                        string eventName2 = entry.Key;
                        List<TameResult> users = entry.Value;

                        // Logging für Debug
                        foreach (TameResult winner in users)
                        {
                            CPH.LogDebug($"  -> {winner.user} hat den Dino {currentDino.Name} getamed. (Event: {eventName2})");
                        }

                        // Zeile bauen: EventName: user1, user2
                        string line = $"{eventName2}: {string.Join(", ", users)}";
                        winnersByEventList.Add(line);
                    }
                }
                if (losersByEvent.Count > 0)
                {
                    // Ergebnisliste für die finale Ausgabe

                    // Events alphabetisch sortieren
                    foreach (var entry in losersByEvent.OrderBy(e => e.Key))
                    {
                        string eventName2 = entry.Key;
                        List<string> users = entry.Value;

                        // Logging für Debug
                        foreach (string loser in users)
                        {
                            CPH.LogDebug($"  -> {loser} hat den Dino {currentDino.Name} nicht getamed. (Event: {eventName2})");
                        }

                        // Zeile bauen: EventName: user1, user2
                        string line = $"{eventName2}: {string.Join(", ", users)}";
                        losersList.Add(line);
                    }
                }
                //CPH.TryGetArg("pickWinnerMessage", out message);
                message = ReplaceWithArgs(messages.PickWinnerMessage, new Dictionary<string, object>
                {
                    { "currentDino", currentDino.Name + " (Level " + currentDinoLevel + ")" },
                    { "winners", winnersList.Count > 0 ? string.Join(", ", winnersList) : "Keine" },
                    { "winnersByEvent", string.Join(" | ", winnersByEventList) },
                    { "losers", string.Join(" | ", losersList) }
                });
            }
            else
            {
                //CPH.TryGetArg("pickWinnerMessageFail", out message);
                message = ReplaceWithArgs(messages.PickWinnerMessageFail, new Dictionary<string, object>
                {
                    { "currentDino", currentDino.Name }
                });
            }
        }
        CPH.SendMessage(message);

        // Aufräumen
        tameEntries.Clear();
        currentDino = null;
        return true;
    }
    public bool GetEggPasteCount()
    {
        string user = args["user"].ToString();
        int eggPaste = CPH.GetTwitchUserVar<int>(user, "dinoTame_egg_paste", true);
        //CPH.TryGetArg("getEggPasteMessage", out string message);
        string message = ReplaceWithArgs(messages.GetEggPasteMessage, new Dictionary<string, object>
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
        bool alreadyTamed = tamedDinos.ContainsKey(currentDino.Name);

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
    public int GetTimestamp()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return (int)now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
    public string ConvertTimestampToString(int timestamp, string format = "dd.MM.yyyy HH:mm:ss")
    {
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        return dateTimeOffset.ToLocalTime().ToString(format);
    }
    public bool AddUserTamedDino(TameResult tr)
    {
        if (string.IsNullOrEmpty(tr.dino))
        {
            CPH.LogWarn("[DinoTame] Kein aktueller Dino zum Hinzufügen.");
            return false;
        }

        // Aktuelle Tamed Dinos des Users abrufen
        Dictionary<int, TamedDinoEntry> tamedDinos = DeserializeUserTamedDinos(tr.user);

        // Prüfen, ob der Dino bereits getamed wurde
        if (tamedDinos.Values.Any(d => d.name == tr.dino))
        {
            CPH.LogWarn($"[DinoTame] {tr.user} hat den Dino {tr.dino} bereits getamed.");
            return false;
        }
        int id = GetTimestamp();
        // Dino hinzufügen
        Dictionary<string, int> tamedDinoStats = GetLeveldDinoStatsWithEffectiveness(tr.levelWild, tr.effectiveness);
        tamedDinos.Add(id, new TamedDinoEntry
        {
            name = tr.dino,
            tame_date = ConvertTimestampToString(id, "dd.MM.yyyy HH:mm:ss"),
            levelWild = tr.levelWild,
            effectiveness = tr.effectiveness,
            level = tr.level,
            health = tamedDinoStats["Health"],
            stamina = tamedDinoStats["Stamina"],
            oxygen = tamedDinoStats["Oxygen"],
            food = tamedDinoStats["Food"],
            weight = tamedDinoStats["Weight"],
            damage = tamedDinoStats["Damage"],
            speed = tamedDinoStats["Speed"],
            win = 0,
            lose = 0
        });

        SerializeUserTamedDinos(tr.user, tamedDinos);
        CPH.LogInfo($"[DinoTame] {tr.user} hat den Dino {tr.dino} erfolgreich getamed.");
        return true;
    }
    public Dictionary<int, TamedDinoEntry> DeserializeUserTamedDinos(string user)
    {
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            CPH.LogInfo($"[DinoTame] {user} hat keine getamten Dinos.");
            return new Dictionary<int, TamedDinoEntry>();
        }

        Dictionary<int, TamedDinoEntry> tamedDinos = JsonConvert.DeserializeObject<Dictionary<int, TamedDinoEntry>>(json);
        return tamedDinos;
    }
    public bool SerializeUserTamedDinos(string user, Dictionary<int, TamedDinoEntry> tamedDinos)
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
    public Dictionary<string, int> GetLeveldDinoStatsWithEffectiveness(int baseLevel, double effectiveness = 0.0)
    {
        Dictionary<string, int> stats = new Dictionary<string, int>();
        baseLevel -= 1; // Weil Level 1 = 0 Punkte
        // Alle StatValue-Properties von currentDino.Stats holen
        foreach (var prop in typeof(DinoStats).GetProperties())
        {
            StatValue stat = (StatValue)prop.GetValue(currentDino.Stats);
            ;
            if (DinoStatsMultipliers.TryGetValue(prop.Name, out double multiplier)) multiplier = 1.0;

            // Grundwert + Wild-Level-Skalierung
            int baseValue = stat.Base;
            int wildValue = stat.Wild * baseLevel;

            int totalValue = baseValue + wildValue;

            // Effektivitätsbonus (wenn vorhanden)
            if (effectiveness > 0)
            {
                // Effektivität = nur halbe Level auf jeden Stat
                // Beispiel: (150 * 0.50 * 0.50) * 12 = 450
                int effBonusLevels = (int)(baseLevel * (effectiveness / 100.0) / 2.0);
                double bonus = effBonusLevels * (totalValue * (stat.Tame / 100) * multiplier);
                totalValue += (int)bonus;
            }

            stats[prop.Name] = totalValue;
        }

        return stats;
    }    
    public bool CheckUserTamedDino()
    {
        string user = args["user"].ToString();
        bool alreadyTamed = GetUserTamedDino();
        int countTamed = GetUserTamedDinoCount();
        string message;

        if (!tamingActive)
        {
            //CPH.TryGetArg("checkUserTamedMessageNotActive", out message);
            message = ReplaceWithArgs(messages.CheckUserTamedMessageNotActive, new Dictionary<string, object>
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
            //CPH.TryGetArg("checkUserTamedMessageYes", out message);
            message = ReplaceWithArgs(messages.CheckUserTamedMessageYes, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino.Name }
            });
        }
        else
        {
            //CPH.TryGetArg("checkUserTamedMessageNo", out message);
            message = ReplaceWithArgs(messages.CheckUserTamedMessageNo, new Dictionary<string, object>
            {
                { "user", user },
                { "currentDino", currentDino.Name }
            });
        }

        CPH.SendMessage(message);
        return true;
    }
    public bool GetKibble()
    {
        string message = string.Empty;
        foreach (var kibble in kibbleInfo)
        {
            string name = kibble.Key;
            int costs = kibble.Value.Cost;
            double power = kibble.Value.Power / 1000;

            if (string.IsNullOrEmpty(message))
            {
                message = "Kibble-Liste (Kibble / Kosten / Power): ";
            }
            else
            {
                message += "; ";
            }
            message += $"{name} / {costs} / {power}";
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
            //CPH.TryGetArg("fightMessageNoDinos", out message);
            message = ReplaceWithArgs(messages.FightMessageNoDinos, new Dictionary<string, object>
            {
                { "user", requestUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {requestUser} has no tamed dinos to fight with.");
            return false;
        }

        if (string.IsNullOrEmpty(targetUser))
        {
            //CPH.TryGetArg("fightMessageNoTarget", out message);
            message = ReplaceWithArgs(messages.FightMessageNoTarget, new Dictionary<string, object>
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
            //CPH.TryGetArg("fightMessageNoUser", out message);
            message = ReplaceWithArgs(messages.FightMessageNoUser, new Dictionary<string, object>
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
            //CPH.TryGetArg("fightMessageSelf", out message);
            message = ReplaceWithArgs(messages.FightMessageSelf, new Dictionary<string, object>
            {
                { "user", requestUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {requestUser} cannot fight against themselves.");
            return false;
        }

        if (GetUserTamedDinoCount(targetUser) <= 0)
        {
            //CPH.TryGetArg("fightMessageTargetNoDinos", out message);
            message = ReplaceWithArgs(messages.FightMessageTargetNoDinos, new Dictionary<string, object>
            {
                { "user", requestUser },
                { "targetUser", targetUser }
            });
            CPH.SendMessage(message);
            CPH.LogWarn($"[DinoTame] {targetUser} has no tamed dinos to fight with.");
            return false;
        }

        var (requestUserDinoId, requestUserDino ) = GetRandomTamedDino(requestUser);
        var (targetUserDinoId, targetUserDino ) = GetRandomTamedDino(targetUser);

        //CPH.TryGetArg("fightMessage", out message);
        message = ReplaceWithArgs(messages.FightMessage, new Dictionary<string, object>
        {
            { "user", requestUser },
            { "targetUser", targetUser },
            { "requestUserDino", requestUserDino.name },
            { "targetUserDino", targetUserDino.name }
        });
        CPH.SendMessage(message);

        CPH.EnableTimer("[DinoTame] Fight Request Timer");
        CPH.SetGlobalVar("dinoTame_FightTarget", targetUser, false);
        CPH.SetGlobalVar("dinoTame_FightRequest", requestUser, false);
        CPH.SetGlobalVar("dinoTame_FightDinoTarget", targetUserDinoId, false);
        CPH.SetGlobalVar("dinoTame_FightDinoRequest", requestUserDinoId, false);
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

            Dictionary<int, TamedDinoEntry> requestUserTamedDinos = DeserializeUserTamedDinos(requestUser);
            Dictionary<int, TamedDinoEntry> targetUserTamedDinos = DeserializeUserTamedDinos(targetUser);

            string message;
            if (result == 0)
            {
                //CPH.TryGetArg("fightMessageDraw", out message);
                message = ReplaceWithArgs(messages.FightMessageDraw, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoRequest", false), false);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoTarget", false), false);
            }
            else if (result == 1)
            {
                //CPH.TryGetArg("fightMessageWin", out message);
                message = ReplaceWithArgs(messages.FightMessageWin, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoRequest", false), true);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoTarget", false), false);
            }
            else
            {
                //CPH.TryGetArg("fightMessageLose", out message);
                message = ReplaceWithArgs(messages.FightMessageLose, new Dictionary<string, object>
                {
                    { "targetUser", targetUser },
                    { "requestUser", requestUser }
                });

                // Update win/lose counts
                UpdateWinLoseCounts(requestUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoRequest", false), false);
                UpdateWinLoseCounts(targetUser, CPH.GetGlobalVar<int>("dinoTame_FightDinoTarget", false), true);
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
            //CPH.TryGetArg("fightMessageReject", out string message);
            string message = ReplaceWithArgs(messages.FightMessageReject, new Dictionary<string, object>
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
    public void UpdateWinLoseCounts(string user, int dino, bool isWin)
    {
        Dictionary<int, TamedDinoEntry> userTamedDinos = DeserializeUserTamedDinos(user);

        if (isWin)
        {
            userTamedDinos[dino].win++;
        }
        else
        {
            userTamedDinos.Remove(dino);
            //userTamedDinos[dino].lose++;
        }

        SerializeUserTamedDinos(user, userTamedDinos);
    }

    public (int, TamedDinoEntry) GetRandomTamedDino(string user)
    {
        string json = CPH.GetTwitchUserVar<string>(user, "dinoTame_TamedDinos", true);
        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            CPH.LogWarn($"[DinoTame] {user} has no tamed dinos.");
            return (-1, null);
        }

        Dictionary<int, TamedDinoEntry> tamedDinos = DeserializeUserTamedDinos(user);
        int index = rnd.Next(tamedDinos.Count);
        return (tamedDinos.ElementAt(index).Key, tamedDinos.ElementAt(index).Value);
    }
    public int FightResult(string requestUser, string targetUser)
    {
        int userDinoId = CPH.GetGlobalVar<int>("dinoTame_FightDinoRequest", false);
        int targetUserDinoId = CPH.GetGlobalVar<int>("dinoTame_FightDinoTarget", false);

        Dictionary<int, TamedDinoEntry> requestUserTamedDinos = DeserializeUserTamedDinos(requestUser);
        Dictionary<int, TamedDinoEntry> targetUserTamedDinos = DeserializeUserTamedDinos(targetUser);

        string userDinoName = requestUserTamedDinos[userDinoId].name;
        string targetUserDinoName = targetUserTamedDinos[targetUserDinoId].name;
        double requestUserDinoHealth = requestUserTamedDinos[userDinoId].health;
        double targetUserDinoHealth = targetUserTamedDinos[targetUserDinoId].health;
        double requestUserDinoDamage = requestUserTamedDinos[userDinoId].damage;
        double targetUserDinoDamage = targetUserTamedDinos[targetUserDinoId].damage;

        CPH.LogInfo($"[DinoTame] Fight: {requestUser} ({userDinoName} HP:{requestUserDinoHealth} DMG:{requestUserDinoDamage}) vs {targetUser} ({targetUserDinoName} HP:{targetUserDinoHealth} DMG:{targetUserDinoDamage})");

        //CPH.TryGetArg("fightMessageAccept", out string message);
        string message = ReplaceWithArgs(messages.FightMessageAccept, new Dictionary<string, object>
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
/*     public bool UpdateTamedDinos()
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
 */    
    public bool setArenaReady()
    {
        if (CPH.GetTimerState("aa55a587-5eb1-42a2-9861-d10e983a749e"))
        {
            CPH.LogWarn("[DinoTame] Arena is already in cleanup.");
            CPH.DisableTimer("[DinoTame] Boss Arena");
            return false;
        }
        CPH.DisableTimer("[DinoTame] Arena CleanUp");
        
        double dblrnd = rnd.NextDouble();
        if (dblrnd < 0.25)
        {
            arenaReady = true;
            CPH.DisableTimer("[DinoTame] Boss Arena");
            openBossfight();
        }
        else
        {
            if (!arenaReady) CPH.SendMessage(messages.FighterArenaOpenMessage);
            arenaReady = true;
            CPH.EnableTimer("[DinoTame] Boss Arena");
        }
        return true;
    }
    public bool openArena()
    {
        if (arenaOpen || bossfightOpen)
        {
            CPH.LogWarn("[DinoTame] Arena is already open.");
            return false;
        }
        else if (!arenaReady)
        {
            CPH.LogWarn("[DinoTame] Arena is not ready.");
            return false;
        }
        string json = JsonConvert.SerializeObject(new Dictionary<string, FighterEntry>());
        CPH.SetGlobalVar("DinoTame_Fighters", json, false);
        //CPH.TryGetArg("fighterArenaOpenMessage", out string message);
        CPH.EnableTimer("[DinoTame] Arena Entry Timer");        
        arenaOpen = true;
        arenaReady = false;
        return true;
    }
    public bool fightArena()
    {
        double animationSpeed = 1.0;
        int timestamp = GetTimestamp();
        if (!arenaOpen)
        {
            CPH.LogWarn("[DinoTame] Arena is not open.");
            return false;
        }
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
            //CPH.TryGetArg("fighterArenaNotEnoughFightersMessage", out message);
            CPH.SendMessage(messages.FighterArenaNotEnoughFightersMessage);
            CPH.SetTwitchUserVar(fighters.First().Value.user, "dinoTame_egg_paste", CPH.GetTwitchUserVar<int>(fighters.First().Value.user, "dinoTame_egg_paste", true) + pot, true);
            clearFighters(300);
            return false;
        }

        //CPH.TryGetArg("fighterArenaFightMessage", out message);
        CPH.SendMessage(messages.FighterArenaFightMessage);

        Dictionary<string, FighterEntry> aliveFighters = new Dictionary<string, FighterEntry>(fighters);

        while (aliveFighters.Count > 1)
        {
            if (GetTimestamp() - timestamp > 15)
            {
                animationSpeed += 0.25;
                timestamp = GetTimestamp();
            }
            var fighterKeys = aliveFighters
                .Where(x => x.Value.currentHealth > 0)
                .Select(x => x.Key)
                .ToList();

            foreach (var fighterKey in fighterKeys)
            {
                if (!aliveFighters.TryGetValue(fighterKey, out var fighter)) continue;
                //fighter = aliveFighters[fighterKey];
                int randomIndex = rnd.Next(0, aliveFighters.Count);
                var randomFighter = aliveFighters.ElementAt(randomIndex);

                while (fighterKey == randomFighter.Key || string.IsNullOrEmpty(randomFighter.Key))
                {
                    CPH.LogInfo($"[DinoTame] Fighter {fighter.user} ({fighter.dino}) cannot attack themselves.");
                    randomIndex = rnd.Next(0, aliveFighters.Count);
                    randomFighter = aliveFighters.ElementAt(randomIndex);
                }

                int fightCritDmg = 3; // Crit damage multiplier
                bool userCrit = false;
                int dmg = 0;

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
                updateFighter(fighterKey, randomFighter.Key, randomFighter.Value, dmg, userCrit, animationSpeed);
                if (randomFighter.Value.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {randomFighter.Value.user} ({randomFighter.Value.dino}) was defeated.");
                    UpdateWinLoseCounts(randomFighter.Value.user, randomFighter.Value.dinoId, false);
                    aliveFighters.Remove(randomFighter.Key);
                    //continue;
                }
                CPH.LogInfo("[DinoTame] Waiting for next fighter action...");
                CPH.Wait((int)(1500 / animationSpeed));
            }
        }

        CPH.LogInfo("[DinoTame] Arena fight finished.");
        if (aliveFighters.Count == 1)
        {
            var winner = aliveFighters.First();
            //CPH.TryGetArg("fighterArenaWinnerMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaWinnerMessage, new Dictionary<string, object>
            {
                { "user", winner.Value.user },
                { "dino", winner.Value.dino },
                { "fighterArenaPot", pot}
            });
            CPH.SendMessage(message);
            UpdateWinLoseCounts(winner.Value.user, winner.Value.dinoId, true);
            int winnerCurrentEggPaste = CPH.GetTwitchUserVar<int>(winner.Value.user, "dinoTame_egg_paste", true);
            int winnerNewEggPaste = winnerCurrentEggPaste + pot;
            CPH.SetTwitchUserVar(winner.Value.user, "dinoTame_egg_paste", winnerNewEggPaste, true);
            CPH.SetGlobalVar("DinoTame_ArenaPot", 0, false);

        }
        else
        {
            //CPH.TryGetArg("fighterArenaNoWinnerMessage", out message);
            CPH.SendMessage(messages.FighterArenaNoWinnerMessage);
        }
        CPH.Wait(10000); // Wait for 10 seconds before cleaning up
        clearFighters();

        return true;
    }
    public bool addFighter()
    {
        CPH.TryGetArg("userId", out int userId);
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("fighterArenaEggPasteCost", out int eggPasteCost);
        string globVarName = "DinoTame_Fighters";
        if (bossfightOpen) globVarName = "DinoTame_BossFighters";

        string message;
        if (!arenaReady && !arenaOpen && !bossfightOpen)
        {
            CPH.LogWarn("[DinoTame] Arena is not ready/open. Cannot add fighter.");
            //CPH.TryGetArg("fighterArenaNotOpenMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaNotOpenMessage, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }
        else if (arenaReady && !arenaOpen && !bossfightOpen)
        {
            openArena();
        }
        string json = CPH.GetGlobalVar<string>(globVarName, false);
        Dictionary<string, FighterEntry> fighters = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        json = null; // Clear the json variable to free memory
        if (arenaOpen && fighters != null && fighters.Values.Any(f => f != null && f.userId == userId))
        {
            CPH.LogWarn($"[DinoTame] {user} is already a fighter in the arena.");
            //CPH.TryGetArg("fighterArenaAlreadyFighterMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaAlreadyFighterMessage, new Dictionary<string, object>
            {
                { "user", user }
            });
            CPH.SendMessage(message);
            return false;
        }

        var (dinoId, dinoEntry) = GetRandomTamedDino(user);
        string id = userId + " - " + dinoId;

        if (bossfightOpen && fighters.ContainsKey(id))
        {
            int retryCountMax = Math.Min(5, GetUserTamedDinoCount(user));
            int retryCount = 1;
            while (fighters.ContainsKey(id) && retryCount <= retryCountMax)
            {
                CPH.LogInfo($"[DinoTame] {user} already has a fighter with dino ID {dinoId} in the boss fight. Try {retryCount} to selecting a new dino.");
                (dinoId, dinoEntry) = GetRandomTamedDino(user);
                id = userId + " - " + dinoId;
                retryCount++;
            }
            if (retryCount >= retryCountMax && fighters.ContainsKey(id))
            {
                CPH.LogWarn($"[DinoTame] {user} could not select a unique dino for the boss fight after {retryCountMax} attempts.");
                return false;
            }
        }

        if (dinoId == -1)
        {
            CPH.LogWarn($"[DinoTame] {user} has no tamed dinos to fight with.");
            //CPH.TryGetArg("fighterArenaNoDinosMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaNoDinosMessage, new Dictionary<string, object>
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
                //CPH.TryGetArg("fighterArenaNotEnoughEggPasteMessage", out message);
                message = ReplaceWithArgs(messages.FighterArenaNotEnoughEggPasteMessage, new Dictionary<string, object>
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
            if (arenaOpen) CPH.SetGlobalVar("DinoTame_ArenaPot", arenaPot + eggPasteCost, false);
        }

        if (arenaOpen && !bossfightOpen && (fighters == null || fighters.Count == 0))
        {
            CPH.EnableTimer("[DinoTame] Arena Entry Timer");
            CPH.LogInfo("[DinoTame] Fighters list is empty, enabling arena entry timer.");
            //CPH.TryGetArg("fighterArenaFirstEntryMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaFirstEntryMessage, new Dictionary<string, object>
            {
                { "user", user },
                { "dino", dinoEntry.name }
            });
            CPH.SendMessage(message);
        }
        else
        {
            CPH.LogInfo("[DinoTame] Fighters list exists, no need to enable entry timer.");
            //CPH.TryGetArg("fighterArenaEntryMessage", out message);
            message = ReplaceWithArgs(messages.FighterArenaEntryMessage, new Dictionary<string, object>
            {
                { "user", user },
                { "dino", dinoEntry.name }
            });
            CPH.SendMessage(message);
        }
        
        var payload = new
        {
            type = "addFighter",
            id = id,
            playerName = user,
            dinoName = dinoEntry.name + $" (Lvl {dinoEntry.level})",
            iconUrl = $"{dinoIconPath}\\{dinoEntry.name}.png",
            currentHP = dinoEntry.health,
            maxHP = dinoEntry.health,
            joinDurationSeconds = arenaJoinDuration
        };
        json = JsonConvert.SerializeObject(payload);
        CPH.WebsocketBroadcastJson(json);
        json = null;

        fighters.Add(id, new FighterEntry
        {
            userId = userId,
            user = user,
            dinoId = dinoId,
            dino = dinoEntry.name + $" (Lvl {dinoEntry.level})",
            currentHealth = dinoEntry.health,
            maxHealth = dinoEntry.health,
            damage = dinoEntry.damage
        });
        json = JsonConvert.SerializeObject(fighters);
        CPH.SetGlobalVar(globVarName, json, false);
        return true;
    }
    public bool addBossesToArena()
    {
        int bossDinoLevel = 1;
        int id = 1;
        var dinos = SelectRandomBossDinos();

        Dictionary<string, FighterEntry> bosses = new Dictionary<string, FighterEntry>();
        foreach (var bossDino in dinos)
        {
            if (string.IsNullOrEmpty(bossDino.Name))
            {
                CPH.LogError($"[DinoTame] Boss dino has no name defined.");
                return false;
            }
            int hp = bossDino.Stats.Health.Base + bossDino.Stats.Health.Wild * bossDinoLevel;
            int dmg = bossDino.Stats.Damage.Base + bossDino.Stats.Damage.Wild * bossDinoLevel;
            var payload = new
            {
                type = "addBoss",
                id = id.ToString(),
                playerName = $"{bossDino.Varianten} - Lvl {bossDinoLevel}",
                dinoName = $"{bossDino.Name}",
                iconUrl = $"{dinoIconPath}\\{bossDino.Name}.png",
                currentHP = hp,
                maxHP = hp,
                joinDurationSeconds = arenaJoinDuration
            };
            string json = JsonConvert.SerializeObject(payload);
            CPH.WebsocketBroadcastJson(json);

            bosses.Add(id.ToString(), new FighterEntry
            {
                user = "BossDino",
                dino = $"{bossDino.Varianten} {bossDino.Name} (Lvl {bossDinoLevel})",
                currentHealth = hp,
                maxHealth = hp,
                damage = dmg
            });
            json = JsonConvert.SerializeObject(bosses);
            CPH.SetGlobalVar("DinoTame_Bosses", json, false);
            id++;
        }

        CPH.SetTimerInterval("c2d0ce60-1c02-474d-bef9-1e504c87ca44", arenaJoinDuration);
        CPH.EnableTimer("[DinoTame] Arena Entry Timer");

        return true;
    }
    public bool updateFighter(string attackerId, string defenderId, FighterEntry updatedFighter, double dmg, bool crit = false, double animationSpeed = 1.0)
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
            critDmg = crit,
            animationSpeed = animationSpeed
        };
        string json = JsonConvert.SerializeObject(payload);
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
        CPH.SetGlobalVar("DinoTame_Fighters", "{}", false);
        CPH.SetGlobalVar("DinoTame_Bosses", "{}", false);
        CPH.SetGlobalVar("DinoTame_BossFighters", "{}", false);
        CPH.WebsocketBroadcastJson("{\"type\":\"clearFighters\"}");
        return true;
    }
    public bool isStreamLive()
    {
        
        // Check if the stream is live
        return debug || CPH.GetGlobalVar<bool>("isLive", false);
    }
    public bool openBossfight()
    {
        if (bossfightOpen || arenaOpen)
        {
            CPH.LogWarn("[DinoTame] Bossfight is already open.");
            return false;
        }
        else if (!arenaReady)
        {
            CPH.LogWarn("[DinoTame] Arena is not ready.");
            return false;
        }
        string json = JsonConvert.SerializeObject(new Dictionary<string, FighterEntry>());
        CPH.SetGlobalVar("DinoTame_BossFighters", json, false);
        addBossesToArena();
        json = CPH.GetGlobalVar<string>("DinoTame_Bosses", false);
        Dictionary<string, FighterEntry> bosses = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        int pot = 0;
        if (bosses != null)
        {
            foreach (var boss in bosses)
            {
                pot += (int)(boss.Value.maxHealth * 0.001); // Each boss contributes 1% of their max health to the pot
            }
            CPH.SetGlobalVar("DinoTame_BossArenaPot", pot, false);
        }
        else return false;
        //CPH.TryGetArg("fighterBossArenaOpenMessage", out string message);
        string message = ReplaceWithArgs(messages.FighterBossArenaOpenMessage, new Dictionary<string, object>
        {
            { "eggPasteToWin", pot }
        });
        CPH.SendMessage(message);
        bossfightOpen = true;
        arenaReady = false;
        CPH.DisableTimer("[DinoTame] start Boss Arena");
        CPH.EnableTimer("[DinoTame] Arena Entry Timer");
        return true;
    }
    public bool fightBossArena()
    {
        double animationSpeed = 1.5;
        int timestamp = GetTimestamp();
        if (!bossfightOpen)
        {
            CPH.LogWarn("[DinoTame] Bossfight is not open.");
            return false;
        }
        bossfightOpen = false;
        CPH.DisableTimer("[DinoTame] Arena Entry Timer");
        string message;
        
        string json = CPH.GetGlobalVar<string>("DinoTame_BossFighters", false);
        Dictionary<string, FighterEntry> fighters = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        json = null;
        int pot = CPH.GetGlobalVar<int>("DinoTame_ArenaPot", false);

        if (fighters == null || fighters.Count < 2)
        {
            CPH.LogWarn("[DinoTame] Not enough fighters to start the Boss Arena fight.");
           // CPH.TryGetArg("fighterBossArenaNotEnoughFightersMessage", out message);
            CPH.SendMessage(messages.FighterBossArenaNotEnoughFightersMessage);
            foreach (var fighter in fighters)
            {
                CPH.SetTwitchUserVar(fighter.Value.user, "dinoTame_egg_paste", CPH.GetTwitchUserVar<int>(fighter.Value.user, "dinoTame_egg_paste", true) + (pot / fighters.Count), true);
            }
            clearFighters(300);
            return false;
        }

        json = CPH.GetGlobalVar<string>("DinoTame_Bosses", false);
        Dictionary<string, FighterEntry> bosses = JsonConvert.DeserializeObject<Dictionary<string, FighterEntry>>(json);
        json = null;
        if (bosses != null)
        {
            pot = CPH.GetGlobalVar<int>("DinoTame_BossArenaPot", false);
        }
        else
        {
            CPH.LogWarn("[DinoTame] No boss fighter found in the arena.");
            clearFighters(300);
            return false;
        }



        //CPH.TryGetArg("fighterBossArenaFightMessage", out message);
        CPH.SendMessage(messages.FighterBossArenaFightMessage);

        Dictionary<string, FighterEntry> aliveFighters = new Dictionary<string, FighterEntry>(fighters);
        Dictionary<string, FighterEntry> aliveBosses = new Dictionary<string, FighterEntry>(bosses);

        while (aliveFighters.Count != 0 && aliveBosses.Count != 0)
        {
            if (GetTimestamp() - timestamp > 10)
            {
                animationSpeed += 0.25;
                timestamp = GetTimestamp();
            }
            // Fighters attack bosses
            var fighterKeys = aliveFighters
                .Where(x => x.Value.currentHealth > 0)
                .Select(x => x.Key)
                .ToList();
            foreach (var fighterKey in fighterKeys)
            {
                if (!aliveFighters.TryGetValue(fighterKey, out var fighter)) continue;
                fighter = aliveFighters[fighterKey];
                int randomIndex = rnd.Next(0, aliveBosses.Count);
                var randomTarget = aliveBosses.ElementAt(randomIndex);

                bool userCrit = false;
                int dmg = fighter.damage * 5;

                if (fighter.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {fighter.user} ({fighter.dino}) is already defeated.");
                    continue; // Skip if the fighter is already defeated
                }

                if (FightCritChance())
                {
                    dmg *= rnd.Next(2, 7);
                    userCrit = true;
                }
                randomTarget.Value.currentHealth = Math.Max(0, randomTarget.Value.currentHealth - dmg);
                CPH.LogInfo($"[DinoTame] {fighter.user} ({fighter.dino}) attacks {randomTarget.Value.user} ({randomTarget.Value.dino}) for {dmg} damage. Crit: {userCrit}");
                updateFighter(fighterKey, randomTarget.Key, randomTarget.Value, dmg, userCrit, animationSpeed);
                if (randomTarget.Value.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {randomTarget.Value.user} ({randomTarget.Value.dino}) was defeated.");
                    aliveBosses.Remove(randomTarget.Key);
                    if (aliveBosses.Count == 0) break;
                    //continue;
                }
                CPH.LogInfo("[DinoTame] Waiting for next fighter action...");
                CPH.Wait((int)(1500 / animationSpeed));
            }
            
            
            // Bosses' turn to attack
            var bossKeys = aliveBosses
                .Where(x => x.Value.currentHealth > 0)
                .Select(x => x.Key)
                .ToList();
            foreach (var bossKey in bossKeys)
            {
                if (!aliveBosses.TryGetValue(bossKey, out var fighter)) continue;
                fighter = aliveBosses[bossKey];
                int randomIndex = rnd.Next(0, aliveFighters.Count);
                var randomTarget = aliveFighters.ElementAt(randomIndex);

                bool userCrit = false;
                int dmg = fighter.damage * 5;
                if (fighter.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {fighter.user} ({fighter.dino}) is already defeated.");
                    continue; // Skip if the fighter is already defeated
                }
                if (FightCritChance(5))
                {
                    dmg *= rnd.Next(2, 7);
                    userCrit = true;
                }
                randomTarget.Value.currentHealth = Math.Max(0, randomTarget.Value.currentHealth - dmg);
                CPH.LogInfo($"[DinoTame] {fighter.user} ({fighter.dino}) attacks {randomTarget.Value.user} ({randomTarget.Value.dino}) for {dmg} damage. Crit: {userCrit}");
                updateFighter(bossKey, randomTarget.Key, randomTarget.Value, dmg, userCrit, animationSpeed);
                if (randomTarget.Value.currentHealth <= 0)
                {
                    CPH.LogInfo($"[DinoTame] Fighter {randomTarget.Value.user} ({randomTarget.Value.dino}) was defeated.");
                    aliveFighters.Remove(randomTarget.Key);
                    UpdateWinLoseCounts(randomTarget.Value.user, randomTarget.Value.dinoId, false);
                    if (aliveFighters.Count == 0) break;
                    //continue;
                }
                CPH.LogInfo("[DinoTame] Waiting for next fighter action...");
                CPH.Wait((int)(1500 / animationSpeed));
            }

        }

        CPH.LogInfo("[DinoTame] Boss Arena fight finished.");
        if (aliveBosses.Count == 0)
        {
            var winners = aliveFighters;
            //CPH.TryGetArg("fighterBossArenaWinnerMessage", out message);
            message = ReplaceWithArgs(messages.FighterBossArenaWinnerMessage, new Dictionary<string, object>
            {
                { "winners", string.Join(", ", winners.Select(w => $"{w.Value.user} ({w.Value.dino})")) },
                { "fighterBossArenaPot", pot}
            });
            CPH.SendMessage(message);
            foreach (var winner in winners)
            {
                UpdateWinLoseCounts(winner.Value.user, winner.Value.dinoId, true);
                int winnerCurrentEggPaste = CPH.GetTwitchUserVar<int>(winner.Value.user, "dinoTame_egg_paste", true);
                int winnerNewEggPaste = winnerCurrentEggPaste + pot / winners.Count;
                CPH.SetTwitchUserVar(winner.Value.user, "dinoTame_egg_paste", winnerNewEggPaste, true);
            }
        }
        else
        {
            //CPH.TryGetArg("fighterBossArenaNoWinnerMessage", out message);
            CPH.SendMessage(messages.FighterBossArenaNoWinnerMessage);
        }
        CPH.Wait(10000); // Wait for 10 seconds before cleaning up
        CPH.SetGlobalVar("DinoTame_ArenaPot", 0, false);
        
        int duration = rnd.Next(2345, 6543);
        clearFighters();

        return true;
    }
    public bool deleteAllUserDinos()
    {
        List<UserVariableValue<string>> userVarList = CPH.GetTwitchUsersVar<string>("dinoTame_TamedDinos", true);

        foreach (UserVariableValue<string> userVar in userVarList)
        {
            string user = userVar.UserName;
            CPH.UnsetTwitchUserVar(user, "dinoTame_TamedDinos", true);
            CPH.LogInfo($"[DinoTame] Deleted all Dinos for user: {user}");
        }
        return true;
    }
    public bool deleteUserDefaultKibble()
    {
        List<UserVariableValue<string>> userVarList = CPH.GetTwitchUsersVar<string>("dinoTame_kibble_type", true);

        foreach (UserVariableValue<string> userVar in userVarList)
        {
            string user = userVar.UserName;
            CPH.UnsetTwitchUserVar(user, "dinoTame_kibble_type", true);
            CPH.LogInfo($"[DinoTame] Deleted default Kibble type for user: {user}");
        }
        return true;
    }

}