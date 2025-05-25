using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;
using Streamer.bot.Common.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;

public class CPHInline : CPHInlineBase
{
    // Store dino list and file path as class-level variables
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

    // Initialization method to load dinos from file
    private bool InitializeDinos(IDictionary<string, object> args)
    {
        if (isInitialized)
            return true;

        dinosFilePath = args.ContainsKey("DinosFilePath") ? args["DinosFilePath"].ToString() : "Dinos.json";
        if (!System.IO.File.Exists(dinosFilePath))
        {
            CPH.LogWarn($"File '{dinosFilePath}' not found.");
            return false;
        }

        string json = System.IO.File.ReadAllText(dinosFilePath);
        try
        {
            dinoList = JsonConvert.DeserializeObject<List<DinoEntry>>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"Failed to parse JSON: {ex.Message}");
            return false;
        }

        // Filter out entries that have no maps or empty maps
        dinoList = dinoList
            .Where(d => d.maps != null && d.maps.Any(m => !string.IsNullOrWhiteSpace(m)))
            .ToList();

        if (dinoList == null || dinoList.Count == 0)
        {
            CPH.LogWarn("No dinos with maps found in the JSON file.");
            return false;
        }

        isInitialized = true;
        return true;
    }

    

    public bool SelectRandomDino()
    {
        // Use args from context if available, otherwise empty
        var args = this.args ?? new Dictionary<string, object>();

        if (!InitializeDinos(args))
            return false;

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
                    CPH.LogWarn("Map argument is empty.");
                    return false;
                }

                var maps = mapArg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m))
                    .ToList();

                if (maps.Count == 0)
                {
                    CPH.LogWarn("No valid maps provided in Map argument.");
                    return false;
                }

                filteredList = filteredList.Where(d => d.maps != null && d.maps.Any(dm => maps.Contains(dm, StringComparer.OrdinalIgnoreCase))).ToList();
                if (filteredList.Count == 0)
                {
                    CPH.LogWarn($"No dinos found for maps: {string.Join(", ", maps)}.");
                    return false;
                }
            }
        }

        // Weighted random selection based on spawn_chance
        double totalWeight = filteredList.Sum(d => d.spawn_chance);
        if (totalWeight <= 0)
        {
            CPH.LogWarn("Total spawn chance is zero or negative.");
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
                CPH.LogInfo($"Randomly selected dino: {dino.name} (spawn_chance: {dino.spawn_chance}, catch_chance: {dino.catch_chance})");
                CPH.SetGlobalVar("CurrentDino", dino.name, true);
                CPH.SetGlobalVar("CurrentDinocatch_chance", dino.catch_chance, true);
                return true;
            }
        }

        CPH.LogWarn("Failed to select a dino.");
        return false;
    }

    public bool SpawnDino()
    {
        if (!SelectRandomDino())
        {
            CPH.LogWarn("Failed to select a random dino.");
            return false;
        }

        string currentDino = CPH.GetGlobalVar<string>("CurrentDino", true);

        if (string.IsNullOrEmpty(currentDino))
        {
            CPH.LogWarn("No dino selected to spawn.");
            return false;
        }

        CPH.LogInfo($"Spawning dino: {currentDino}");

        // Additional spawn logic can be added here

        return true;
    }

}