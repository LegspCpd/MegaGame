using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Save/load for single-player progress.
    ///
    /// Saves live under %APPDATA%/MegaGame Studios/MegaGame/saves (the Unity
    /// persistentDataPath), which survives an uninstall/reinstall cycle and
    /// needs no write access to the install directory.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileExtension = ".sav";
        private static DataContractJsonSerializer _serializer;

        public static string SaveDirectory
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, "saves");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string ScreenshotDirectory
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, "screenshots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static DataContractJsonSerializer Serializer =>
            _serializer ?? (_serializer = new DataContractJsonSerializer(typeof(SaveData)));

        // ---------------------------------------------------------------- save

        public static bool Save(SaveData data, out string error)
        {
            try
            {
                data.SavedAtUtc = DateTime.UtcNow;
                var path = Path.Combine(SaveDirectory, SanitizeName(data.SlotName) + FileExtension);

                using (var stream = File.Create(path))
                {
                    Serializer.WriteObject(stream, data);
                }

                error = null;
                Debug.Log($"[SaveSystem] Saved to {path}");
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[SaveSystem] Save failed: {e}");
                return false;
            }
        }

        public static SaveData Load(string slotName, out string error)
        {
            try
            {
                var path = Path.Combine(SaveDirectory, SanitizeName(slotName) + FileExtension);
                if (!File.Exists(path))
                {
                    error = $"No save named '{slotName}'";
                    return null;
                }

                using (var stream = File.OpenRead(path))
                {
                    var data = (SaveData)Serializer.ReadObject(stream);
                    error = null;
                    return data;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[SaveSystem] Load failed: {e}");
                return null;
            }
        }

        public static List<string> ListSaves()
        {
            var result = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(SaveDirectory, "*" + FileExtension))
                {
                    result.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not enumerate saves: {e.Message}");
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static bool Delete(string slotName)
        {
            try
            {
                var path = Path.Combine(SaveDirectory, SanitizeName(slotName) + FileExtension);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Delete failed: {e.Message}");
                return false;
            }
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "quicksave";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }
    }

    [Serializable]
    [DataContract]
    public class SaveData
    {
        [DataMember] public string SlotName;
        [DataMember] public int SlotIndex;
        [DataMember] public DateTime SavedAtUtc;

        [DataMember] public float PlayerX;
        [DataMember] public float PlayerY;
        [DataMember] public float PlayerZ;
        [DataMember] public float PlayerYaw;

        [DataMember] public float Health;
        [DataMember] public float Armor;
        [DataMember] public float Stamina;
        [DataMember] public float Money = 2500f;
        [DataMember] public int WantedLevel;

        [DataMember] public float PlaytimeSeconds;

        [DataMember] public List<SavedVehicle> Vehicles = new List<SavedVehicle>();
        [DataMember] public List<string> CompletedMissions = new List<string>();
        [DataMember] public List<string> UnlockedAreas = new List<string>();

        [DataMember] public GameWorldSaveState World = new GameWorldSaveState();
    }

    [Serializable]
    [DataContract]
    public class SavedVehicle
    {
        [DataMember] public string ModelId;
        [DataMember] public string CustomName;
        [DataMember] public float Health = 1000f;
        [DataMember] public float Mileage;
        [DataMember] public int PlateStyle;
    }

    /// <summary>
    /// Named GameWorldSaveState rather than WorldState: the generated protobuf
    /// already defines Megame.Network.WorldState, and an unqualified WorldState
    /// here would be an ambiguous reference in files that import both.
    /// </summary>
    [Serializable]
    [DataContract]
    public class GameWorldSaveState
    {
        [DataMember] public float TimeOfDayHours = 12f;
        [DataMember] public int DayNumber = 1;
        [DataMember] public bool Raining;
        [DataMember] public float TrafficDensity = 1f;
        [DataMember] public float PedestrianDensity = 1f;
    }
}