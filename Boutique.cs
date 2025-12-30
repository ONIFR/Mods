// GravitasMod - Printerceptor patches (FIXED)
// - Fix Harmony patch signature mismatch on PrintSelectedEntity
// - Tabs + filters (Seeds / Mutated Seeds / Eggs / Nuisibles / Resources / Artifacts)
// - Mutated seeds whitelist supports ColdWheat & BeanPlant (PlantableSeed)
// - Optional "mutated seed" badge PNG overlay (mutated_seed_badge.png)
// - Custom print consumption for Resources/Nuisibles/Gassy Moo (DataBank currency by default)

using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace GravitasMod
{
    internal enum PrinterceptorTab
    {
        All,
        Eggs,
        Nuisibles,
        Seeds,
        MutatedSeeds,
        Resources,
        Artifacts
    }

    internal static class PrinterceptorCatalog
    {
        // Currency used by the narrative building. Change later to "GravitasTicket" if needed.
        public const string CURRENCY_PREFAB_ID = "DataBank";

        // Likely ID for Gassy Moo (adjust if your prefab differs)
        public const string GASSY_MOO_ID = "Moo";

        // IDs to remove from Eggs tab
        public static readonly HashSet<string> EggsTabBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MoleEgg",
            "GnitEgg",
        };

        // ALL tab blacklist (remove duplicates like the Sleet Wheat grain)
        private static readonly HashSet<string> AllTabBlacklistIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ColdWheat"
        };

        internal static bool IsAllTabBlacklisted(Tag t)
        {
            var id = SafeTagToString(t);
            return !string.IsNullOrEmpty(id) && AllTabBlacklistIds.Contains(id);
        }

        // Mutated seeds whitelist.
        // Note: "ColdWheat" and "BeanPlant" are PLANT IDs (PlantableSeed), not "...Seed".
        public static readonly HashSet<string> MutatedSeedsWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BasicSingleHarvestPlantSeed",      // Mealwood
            "GardenFoodPlantSeed",              // Sweatcorn
            "MushroomSeed",                     // Dusk Cap
            "PrickleFlowerSeed",                // Bristle Blossom
            "ColdWheat",                        // Sleet Wheat grain (PlantableSeed)
            "SeaLettuceSeed",                   // Waterweed
            "BeanPlant",                        // Nosh bean (PlantableSeed)
            "DinofernSeed",                     // Megafrond
            "SpiceVineSeed",                    // Pincha Pepper
            "SwampHarvestPlantSeed",            // Bog Bucket
            "WormPlantSeed",                    // Grubfruit (spindly shares same seed)
            "CarrotPlantSeed",                  // Plume Squash (Carrot)
            "HardSkinBerryPlantSeed",           // Pikeapple
            "SwampLilySeed",                    // Balm Lily
            "BasicFabricMaterialPlantSeed",     // Thimble Reed
            "SaltPlantSeed",                    // Dasha Saltvine
            "GasGrassSeed",                     // Gas Grass
            "DewDripperPlantSeed",              // Dew Dripper
            "BlueGrassSeed",                    // Alveo Vera (BlueGrass)
            "OxyfernSeed",                      // Oxyfern
        };

        public static readonly HashSet<string> Nuisibles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mosquito",
            "BeeBaby",
            "Glom",
            "Mole"
        };

        public static readonly HashSet<string> Resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Resources (tab "RESSOURCES") – curated list + tuned pricing
            "IronOre",
            "Cuprite",
            "GoldAmalgam",
            "AluminumOre",
            "WoodLog",
            "UraniumOre",
            "Steel",
            "Diamond",
            "ViscoGel",
            "Iridium",
            "SuperCoolant",
            "Plastium",
            "Isoresin",
            "Insulation"
        };

        

        // =========================================================
        // Resources: pack size + multi-currency pricing
        // Note: UI currently displays Data Banks by default; plastic/tickets will be used later.
        // =========================================================
        internal struct ResourcePricing
        {
            public int PackKg;
            public int DataBankCost;
            public int PlasticKgCost;
            public int TicketCost;

            public ResourcePricing(int packKg, int dataBankCost, int plasticKgCost, int ticketCost)
            {
                PackKg = packKg;
                DataBankCost = dataBankCost;
                PlasticKgCost = plasticKgCost;
                TicketCost = ticketCost;
            }
        }

        internal static readonly Dictionary<string, ResourcePricing> ResourcePricingById =
            new Dictionary<string, ResourcePricing>(StringComparer.OrdinalIgnoreCase)
            {
                { "IronOre",       new ResourcePricing(400,  20,     600,      10) },
                { "Cuprite",       new ResourcePricing(400,  20,     600,      10) },
                { "GoldAmalgam",   new ResourcePricing(400,  25,     800,      10) },
                { "AluminumOre",   new ResourcePricing(400,  25,     800,      10) },
                { "WoodLog",       new ResourcePricing(500,  35,    3500,      20) },
                { "UraniumOre",    new ResourcePricing(400,  80,    3000,      50) },
                { "Steel",         new ResourcePricing(500, 150,    5000,     100) },
                { "Diamond",       new ResourcePricing(500, 600,   40000,     150) },
                { "ViscoGel",      new ResourcePricing(100, 400,   15000,     300) },
                { "Iridium",       new ResourcePricing(100, 400,   15000,     300) },
                { "SuperCoolant",  new ResourcePricing(100, 500,   50000,     400) },
                { "Plastium",      new ResourcePricing(100, 800,  100000,     400) },
                { "Isoresin",      new ResourcePricing(100,1500,  150000,     450) },
                { "Insulation",    new ResourcePricing(100,3000,  300000,     500) },
            };

        internal static bool TryGetResourcePackKg(Tag t, out int kg)
        {
            kg = 0;
            var id = SafeTagToString(t);
            if (string.IsNullOrEmpty(id)) return false;

            if (ResourcePricingById.TryGetValue(id, out var rp))
            {
                kg = rp.PackKg;
                return true;
            }
            return false;
        }

        internal static bool TryGetResourceDataBankCost(Tag t, out int cost)
        {
            cost = 0;
            var id = SafeTagToString(t);
            if (string.IsNullOrEmpty(id)) return false;

            if (ResourcePricingById.TryGetValue(id, out var rp))
            {
                cost = rp.DataBankCost;
                return true;
            }
            return false;
        }

        internal static bool TryGetResourcePlasticCostKg(Tag t, out int kgCost)
        {
            kgCost = 0;
            var id = SafeTagToString(t);
            if (string.IsNullOrEmpty(id)) return false;

            if (ResourcePricingById.TryGetValue(id, out var rp))
            {
                kgCost = rp.PlasticKgCost;
                return true;
            }
            return false;
        }

        internal static bool TryGetResourceTicketCost(Tag t, out int ticketCost)
        {
            ticketCost = 0;
            var id = SafeTagToString(t);
            if (string.IsNullOrEmpty(id)) return false;

            if (ResourcePricingById.TryGetValue(id, out var rp))
            {
                ticketCost = rp.TicketCost;
                return true;
            }
            return false;
        }

public static readonly string[] ArtifactPrefabIds =
        {
            "artifact_vhs",
            "artifact_bioluminescentrock",
            "artifact_blender",
            "artifact_dnamodel",
            "artifact_eggrock",
            "artifact_rainboweggrock",
            "artifact_teapot",
            "artifact_grubstatue",
            "artifact_honeyjar",
            "artifact_magmalamp",
            "artifact_saxophone",
            "artifact_reactormodel",
            "artifact_modernart",
            "artifact_moldavite",
            "artifact_moonmoonmoon",
            "artifact_officemug",
            "artifact_okxyray",
            "artifact_pacupercolator",
            "artifact_hatchfossil",
            "artifact_moodring",
            "artifact_robotarm",
            "artifact_rubikscube",
            "artifact_sandstone",
            "artifact_solarsystem",
            "artifact_shieldgenerator",
            "artifact_sink",
            "artifact_obelisk",
            "artifact_stethoscope",
            "artifact_brickphone",
            "artifact_rocktornado",
            "artifact_oracle",
            "artifact_ameliaswatch"
        };

        public static bool IsArtifact(Tag t)
        {
            var s = SafeTagToString(t);
            return !string.IsNullOrEmpty(s) && s.StartsWith("artifact_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNuisible(Tag t) => Nuisibles.Contains(SafeTagToString(t));
        public static bool IsResource(Tag t) => Resources.Contains(SafeTagToString(t));

        public static bool IsGassyMoo(Tag t)
        {
            var s = SafeTagToString(t);
            return !string.IsNullOrEmpty(s) && s.Equals(GASSY_MOO_ID, StringComparison.OrdinalIgnoreCase);
        }

        // Custom print: only those are handled by our currency-consumption patch
        public static bool IsCustomPrint(Tag t) => IsNuisible(t) || IsResource(t) || IsGassyMoo(t);

        public static bool IsWhitelistedMutatedSeed(Tag t)
        {
            var id = SafeTagToString(t);
            return !string.IsNullOrEmpty(id) && MutatedSeedsWhitelist.Contains(id);
        }

        public static bool IsWhitelistedMutatedSeedLike(Tag seedTag, GameObject seedPrefab)
        {
            if (IsWhitelistedMutatedSeed(seedTag))
                return true;

            var seedId = SafeTagToString(seedTag);
            var seedNoSuffix = StripSuffixIgnoreCase(seedId, "Seed");
            if (!string.IsNullOrEmpty(seedNoSuffix) && MutatedSeedsWhitelist.Contains(seedNoSuffix))
                return true;

            // PlantableSeed (ColdWheat / BeanPlant): whitelist based on plantID or plantTag
            var plantId = TryGetPlantIdFromPlantableSeed(seedPrefab);
            if (!string.IsNullOrEmpty(plantId))
            {
                if (MutatedSeedsWhitelist.Contains(plantId)) return true;
                var plantNoSuffix = StripSuffixIgnoreCase(plantId, "Seed");
                if (!string.IsNullOrEmpty(plantNoSuffix) && MutatedSeedsWhitelist.Contains(plantNoSuffix)) return true;
                if (MutatedSeedsWhitelist.Contains(plantId + "Seed")) return true;
            }

            var plantTag = TryGetPlantTagFromPlantableSeed(seedPrefab);
            if (!IsDefaultTag(plantTag))
            {
                var pt = SafeTagToString(plantTag);
                if (!string.IsNullOrEmpty(pt))
                {
                    if (MutatedSeedsWhitelist.Contains(pt)) return true;
                    var ptNoSuffix = StripSuffixIgnoreCase(pt, "Seed");
                    if (!string.IsNullOrEmpty(ptNoSuffix) && MutatedSeedsWhitelist.Contains(ptNoSuffix)) return true;
                    if (MutatedSeedsWhitelist.Contains(pt + "Seed")) return true;
                }
            }

            return false;
        }

        // ---- PlantableSeed reflection helpers ----
        private static string TryGetPlantIdFromPlantableSeed(GameObject seedPrefab)
        {
            try
            {
                if (seedPrefab == null) return null;

                var psType = AccessTools.TypeByName("PlantableSeed");
                if (psType == null) return null;

                var ps = seedPrefab.GetComponent(psType);
                if (ps == null) return null;

                return ReadFirstStringMember(ps, new[]
                {
                    "plantID","PlantID","plantId","PlantId",
                    "plantPrefabID","PlantPrefabID"
                });
            }
            catch { return null; }
        }

        private static Tag TryGetPlantTagFromPlantableSeed(GameObject seedPrefab)
        {
            try
            {
                if (seedPrefab == null) return default(Tag);

                var psType = AccessTools.TypeByName("PlantableSeed");
                if (psType == null) return default(Tag);

                var ps = seedPrefab.GetComponent(psType);
                if (ps == null) return default(Tag);

                return ReadFirstTagMember(ps, new[]
                {
                    "plantTag","PlantTag",
                    "plantPrefabTag","PlantPrefabTag"
                });
            }
            catch { return default(Tag); }
        }

        private static string ReadFirstStringMember(object obj, string[] preferredNames)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var n in preferredNames)
            {
                try
                {
                    var f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(string))
                        return f.GetValue(obj) as string;

                    var p = t.GetProperty(n, BF);
                    if (p != null && p.CanRead && p.PropertyType == typeof(string))
                        return p.GetValue(obj, null) as string;
                }
                catch { }
            }
            return null;
        }

        private static Tag ReadFirstTagMember(object obj, string[] preferredNames)
        {
            if (obj == null) return default(Tag);
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var n in preferredNames)
            {
                try
                {
                    var f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(Tag))
                        return (Tag)f.GetValue(obj);

                    var p = t.GetProperty(n, BF);
                    if (p != null && p.CanRead && p.PropertyType == typeof(Tag))
                        return (Tag)p.GetValue(obj, null);
                }
                catch { }
            }
            return default(Tag);
        }

        // ---- generic helpers ----
        public static string SafeTagToString(Tag t)
        {
            try { return t.ToString(); } catch { return null; }
        }

        public static bool IsDefaultTag(Tag t)
        {
            try { return t.Equals(default(Tag)) || string.IsNullOrEmpty(t.ToString()); }
            catch { return true; }
        }

        public static string StripSuffixIgnoreCase(string s, string suf)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(suf)) return s;
            if (!s.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) return s;
            return s.Substring(0, s.Length - suf.Length);
        }
    }

    internal sealed class PrinterceptorUILabelCache : MonoBehaviour
    {
        public string RequiredPrefix = "Data Banks Required:";
        public string AvailablePrefix = "Data Banks Available:";
    }

    internal static class SeedLikeUtil
    {
        private static Type _plantableSeedType;

        private static Type PlantableSeedType
        {
            get
            {
                if (_plantableSeedType == null)
                    _plantableSeedType = AccessTools.TypeByName("PlantableSeed");
                return _plantableSeedType;
            }
        }

        // Seed-like includes real seeds + PlantableSeed (ColdWheat / BeanPlant)
        public static bool IsSeedLikePrefab(GameObject prefab)
        {
            if (prefab == null) return false;

            var kpid = prefab.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                try { if (kpid.HasTag(GameTags.Seed)) return true; } catch { }
            }

            var t = PlantableSeedType;
            if (t != null)
            {
                try { if (prefab.GetComponent(t) != null) return true; } catch { }
            }

            return false;
        }

        public static bool IsSeedLikeInstance(GameObject go)
        {
            if (go == null) return false;

            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                try { if (kpid.HasTag(GameTags.Seed)) return true; } catch { }
            }

            var t = PlantableSeedType;
            if (t != null)
            {
                try { if (go.GetComponent(t) != null) return true; } catch { }
            }

            return false;
        }
    }

    internal sealed class PrinterceptorTabsState : MonoBehaviour
    {
        public PrinterceptorTab Current = PrinterceptorTab.All;
        public RectTransform OptionGrid;

        public void ApplyFilter(PrinterceptorScreen screen)
        {
            if (screen == null) return;

            Dictionary<Tag, MultiToggle> optionButtons = null;
            try { optionButtons = Traverse.Create(screen).Field<Dictionary<Tag, MultiToggle>>("optionButtons").Value; }
            catch { optionButtons = null; }

            if (optionButtons == null) return;

            foreach (var kv in optionButtons)
            {
                var tag = kv.Key;
                var toggle = kv.Value;
                if (toggle == null) continue;

                toggle.gameObject.SetActive(PassesFilter(tag));
            }

            if (OptionGrid != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(OptionGrid);
        }

        private bool PassesFilter(Tag tag)
        {
            if (Current == PrinterceptorTab.All)
            {
                if (PrinterceptorCatalog.IsAllTabBlacklisted(tag))
                    return false;
                return true;
            }

            if (Current == PrinterceptorTab.Artifacts)
                return PrinterceptorCatalog.IsArtifact(tag);
            if (PrinterceptorCatalog.IsArtifact(tag))
                return false;

            if (Current == PrinterceptorTab.Nuisibles)
                return PrinterceptorCatalog.IsNuisible(tag);
            if (PrinterceptorCatalog.IsNuisible(tag))
                return false;

            if (Current == PrinterceptorTab.Resources)
                return PrinterceptorCatalog.IsResource(tag);
            if (PrinterceptorCatalog.IsResource(tag))
                return false;

            GameObject prefab = null;
            try { prefab = Assets.GetPrefab(tag); } catch { prefab = null; }
            if (prefab == null) return false;

            var kpid = prefab.GetComponent<KPrefabID>();
            if (kpid == null) return false;

            
if (Current == PrinterceptorTab.Eggs)
{
    var id = PrinterceptorCatalog.SafeTagToString(tag);

    if (!string.IsNullOrEmpty(id) && PrinterceptorCatalog.EggsTabBlacklist.Contains(id))
        return false;

    // ✅ Sécurité: certains IDs de Gnit peuvent varier selon versions/DLC
    if (!string.IsNullOrEmpty(id) && id.IndexOf("Gnit", StringComparison.OrdinalIgnoreCase) >= 0)
        return false;

// ✅ Fallback: certains contenus ont des IDs imprévus, on filtre aussi par nom affiché
try
{
    var proper = kpid.GetProperName();
    if (!string.IsNullOrEmpty(proper) && proper.IndexOf("Gnit", StringComparison.OrdinalIgnoreCase) >= 0)
        return false;
}
catch { }

    // show Gassy Moo in Eggs tab even if not tagged as egg
    if (PrinterceptorCatalog.IsGassyMoo(tag))
        return true;

    return SafeHasTag(kpid, GameTags.Egg);
}

            if (Current == PrinterceptorTab.Seeds)
                return SeedLikeUtil.IsSeedLikePrefab(prefab);

            if (Current == PrinterceptorTab.MutatedSeeds)
                return SeedLikeUtil.IsSeedLikePrefab(prefab) && PrinterceptorCatalog.IsWhitelistedMutatedSeedLike(tag, prefab);

            return true;
        }

        private static bool SafeHasTag(KPrefabID kpid, Tag tag)
        {
            try { return kpid != null && kpid.HasTag(tag); }
            catch { return false; }
        }
    }

    // =========================================================
    // PNG badge overlay for seed buttons (optional)
    // Place "mutated_seed_badge.png" next to your DLL OR embed as resource.
    // =========================================================
    internal static class GravitasPngSpriteCache
    {
        public const string MUTATED_SEED_BADGE_PNG = "mutated_seed_badge.png";

        private static Sprite _cached;
        private static bool _tried;
        private static bool _warned;

        public static Sprite GetMutatedSeedBadgeSprite()
        {
            if (_cached != null) return _cached;
            if (_tried) return null;
            _tried = true;

            try
            {
                byte[] bytes = TryLoadEmbeddedBytes(MUTATED_SEED_BADGE_PNG);
                if (bytes == null || bytes.Length == 0)
                    bytes = TryLoadDiskBytes(MUTATED_SEED_BADGE_PNG);

                if (bytes == null || bytes.Length == 0)
                {
                    WarnOnce();
                    return null;
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;

                if (!tex.LoadImage(bytes))
                {
                    WarnOnce();
                    return null;
                }

                tex.Apply(false, false);

                _cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _cached.name = "Gravitas_MutatedSeedBadgeSprite";
                return _cached;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] PNG sprite load failed: {e}");
                return null;
            }
        }

        private static void WarnOnce()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[GravitasMod] Badge PNG missing/unreadable: {MUTATED_SEED_BADGE_PNG}");
        }

        private static byte[] TryLoadEmbeddedBytes(string fileName)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                if (names == null) return null;

                string found = null;
                for (int i = 0; i < names.Length; i++)
                {
                    var n = names[i];
                    if (!string.IsNullOrEmpty(n) && n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = n;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(found)) return null;

                using (var s = asm.GetManifestResourceStream(found))
                {
                    if (s == null) return null;
                    using (var ms = new System.IO.MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        private static byte[] TryLoadDiskBytes(string fileName)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string asmDir = "";
                try { asmDir = System.IO.Path.GetDirectoryName(asm.Location); } catch { asmDir = ""; }

                var candidates = new List<string>();
                AddCandidate(candidates, CombineSafe(asmDir, fileName));
                AddCandidate(candidates, CombineSafe(asmDir, "assets", fileName));
                AddCandidate(candidates, CombineSafe(asmDir, "sprites", fileName));
                AddCandidate(candidates, CombineSafe(asmDir, "anim", fileName));

                string parent = "";
                try { parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(asmDir, "..")); } catch { parent = ""; }
                AddCandidate(candidates, CombineSafe(parent, fileName));
                AddCandidate(candidates, CombineSafe(parent, "assets", fileName));
                AddCandidate(candidates, CombineSafe(parent, "sprites", fileName));
                AddCandidate(candidates, CombineSafe(parent, "anim", fileName));

                for (int i = 0; i < candidates.Count; i++)
                {
                    var p = candidates[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    try
                    {
                        if (System.IO.File.Exists(p))
                            return System.IO.File.ReadAllBytes(p);
                    }
                    catch { }
                }

                return null;
            }
            catch { return null; }
        }

        private static void AddCandidate(List<string> list, string path)
        {
            if (list == null) return;
            if (string.IsNullOrEmpty(path)) return;
            if (!list.Contains(path)) list.Add(path);
        }

        private static string CombineSafe(string a, string b)
        {
            try
            {
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return null;
                return System.IO.Path.Combine(a, b);
            }
            catch { return null; }
        }

        private static string CombineSafe(string a, string b, string c)
        {
            try
            {
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(c)) return null;
                return System.IO.Path.Combine(a, b, c);
            }
            catch { return null; }
        }
    }

    internal sealed class GravitasBadgeMarker : MonoBehaviour { }

    internal static class PrinterceptorMutatedSeedBadgeUtil
    {
        private const string BADGE_NAME = "Gravitas_MutatedSeedBadge";
        private static readonly Dictionary<string, bool> _seedCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static void UpdateBadges(PrinterceptorScreen screen, bool enable)
        {
            if (screen == null) return;

            if (!enable)
            {
                HideExistingBadges(screen);
                return;
            }

            var sprite = GravitasPngSpriteCache.GetMutatedSeedBadgeSprite();
            if (sprite == null) return;

            Dictionary<Tag, MultiToggle> optionButtons = null;
            try { optionButtons = Traverse.Create(screen).Field<Dictionary<Tag, MultiToggle>>("optionButtons").Value; }
            catch { optionButtons = null; }
            if (optionButtons == null) return;

            foreach (var kv in optionButtons)
            {
                var tag = kv.Key;
                var toggle = kv.Value;
                if (toggle == null) continue;

                bool isSeed = IsSeedTagCached(tag);

                var badgeRt = EnsureBadge(toggle.transform);
                if (badgeRt == null) continue;

                if (!isSeed)
                {
                    badgeRt.gameObject.SetActive(false);
                    continue;
                }

                var img = badgeRt.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = sprite;
                    img.enabled = true;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    var c = img.color; if (c.a < 1f) c.a = 1f; img.color = c;
                }

                badgeRt.gameObject.SetActive(true);
            }
        }

        public static void HideExistingBadges(PrinterceptorScreen screen)
        {
            if (screen == null) return;

            Dictionary<Tag, MultiToggle> optionButtons = null;
            try { optionButtons = Traverse.Create(screen).Field<Dictionary<Tag, MultiToggle>>("optionButtons").Value; }
            catch { optionButtons = null; }
            if (optionButtons == null) return;

            foreach (var kv in optionButtons)
            {
                var toggle = kv.Value;
                if (toggle == null) continue;

                var marker = toggle.GetComponentInChildren<GravitasBadgeMarker>(true);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                var existing = FindChildRecursive(toggle.transform, BADGE_NAME) as RectTransform;
                if (existing != null)
                    existing.gameObject.SetActive(false);
            }
        }

        private static bool IsSeedTagCached(Tag tag)
        {
            string key = PrinterceptorCatalog.SafeTagToString(tag);
            if (string.IsNullOrEmpty(key)) return false;

            if (_seedCache.TryGetValue(key, out var cached))
                return cached;

            bool isSeed = false;
            try
            {
                GameObject prefab = null;
                try { prefab = Assets.GetPrefab(tag); } catch { prefab = null; }
                isSeed = SeedLikeUtil.IsSeedLikePrefab(prefab);
            }
            catch { isSeed = false; }

            _seedCache[key] = isSeed;
            return isSeed;
        }

        private static RectTransform EnsureBadge(Transform toggleRoot)
        {
            if (toggleRoot == null) return null;

            var marker = toggleRoot.GetComponentInChildren<GravitasBadgeMarker>(true);
            if (marker != null)
            {
                var mr = marker.transform as RectTransform;
                if (mr != null) { mr.SetAsLastSibling(); return mr; }
            }

            var existing = FindChildRecursive(toggleRoot, BADGE_NAME) as RectTransform;

            var host = TryFindIconHost(toggleRoot);
            if (host == null) host = toggleRoot as RectTransform;

            if (existing != null)
            {
                if (host != null && existing.parent != host) existing.SetParent(host, false);
                existing.SetAsLastSibling();
                if (existing.GetComponent<GravitasBadgeMarker>() == null) existing.gameObject.AddComponent<GravitasBadgeMarker>();
                return existing;
            }

            try
            {
                var go = new GameObject(BADGE_NAME, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(GravitasBadgeMarker));
                var rt = (RectTransform)go.transform;
                rt.SetParent(host ?? toggleRoot, false);
                rt.SetAsLastSibling();

                var le = go.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = true;

                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(18f, 18f);
                rt.anchoredPosition = new Vector2(-6f, -6f);

                go.layer = (host != null ? host.gameObject.layer : toggleRoot.gameObject.layer);
                return rt;
            }
            catch { return null; }
        }

        private static RectTransform TryFindIconHost(Transform toggleRoot)
        {
            try
            {
                var imgs = toggleRoot.GetComponentsInChildren<Image>(true);
                if (imgs == null || imgs.Length == 0) return null;

                Image best = null;
                float bestArea = -1f;

                for (int i = 0; i < imgs.Length; i++)
                {
                    var img = imgs[i];
                    if (img == null || img.sprite == null) continue;
                    var rt = img.rectTransform;
                    if (rt == null) continue;
                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = img;
                    }
                }

                return best != null ? best.rectTransform : null;
            }
            catch { return null; }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            try
            {
                if (root.name == name) return root;

                for (int i = 0; i < root.childCount; i++)
                {
                    var c = root.GetChild(i);
                    if (c == null) continue;
                    if (c.name == name) return c;

                    var r = FindChildRecursive(c, name);
                    if (r != null) return r;
                }
            }
            catch { }

            return null;
        }
    }

    // After filtering, show/hide badges depending on tab.
    [HarmonyPatch(typeof(PrinterceptorTabsState), nameof(PrinterceptorTabsState.ApplyFilter))]
    internal static class PrinterceptorTabsState_ApplyFilter_Badges_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(PrinterceptorTabsState __instance, PrinterceptorScreen screen)
        {
            try
            {
                if (__instance == null || screen == null) return;

                bool enable = (__instance.Current == PrinterceptorTab.MutatedSeeds);

                if (enable)
                    PrinterceptorMutatedSeedBadgeUtil.UpdateBadges(screen, true);
                else
                    PrinterceptorMutatedSeedBadgeUtil.HideExistingBadges(screen);
            }
            catch { }
        }
    }

    // =========================================================
    // UI: Inline wallet counter under "Required:"
    // =========================================================
    internal sealed class GravitasWalletMirror : MonoBehaviour
{
    public PrinterceptorScreen Screen;

    // Destination (inline, à droite de "Required")
    public LocText DestLabel;
    public Image DestIcon;

    // (Optional) reference to the (hidden) header wallet, used only for icon + localized suffix
    public LocText SourceHeaderLabel;
    public Image[] SourceHeaderIcons;

    private float _t;
    private int _last = int.MinValue;

    private string _suffix;       // e.g. "Stored Data Banks"
    private bool _suffixInit;

    private HijackedHeadquarters.Instance _hq;
    private Storage _storage;

    private void LateUpdate()
    {
        _t += Time.unscaledDeltaTime;
        if (_t < 0.2f) return;
        _t = 0f;

        if (DestLabel == null) return;

        EnsureSources();

        int available = GetLiveAvailableCount();
        if (available < 0) available = 0;

        // Only update UI when the value actually changes
        if (available != _last || !_suffixInit)
        {
            _last = available;

            var suffix = GetSuffix();
            if (string.IsNullOrEmpty(suffix))
                suffix = "Stored Data Banks";

            GravitasUIUtil.SetLocText(DestLabel, $"{available} {suffix}");
        }

        // Icon: mirror header icon when available
        try
        {
            if (DestIcon != null)
            {
                Sprite s = null;

                if (SourceHeaderIcons != null && SourceHeaderIcons.Length > 0 && SourceHeaderIcons[0] != null)
                    s = SourceHeaderIcons[0].sprite;

                if (s != null)
                {
                    DestIcon.sprite = s;
                    DestIcon.enabled = true;
                    DestIcon.preserveAspect = true;
                    var c = DestIcon.color; if (c.a < 1f) c.a = 1f; DestIcon.color = c;
                }
            }
        }
        catch { }
    }

    private void EnsureSources()
    {
        // Resolve HQ instance once (should be stable while the screen is open)
        try
        {
            if (_hq == null || _hq.gameObject == null)
                _hq = ResolveHijackedHQInstance(Screen);
        }
        catch { _hq = null; }

        // Resolve storage from HQ once
        try
        {
            if (_storage == null && _hq != null)
                _storage = ResolveStorage(_hq);
        }
        catch { _storage = null; }
    }

private int GetLiveAvailableCount()
    {
        int best = -1;

        // 1) Prefer IUserControlledCapacity.AmountStored (handles explicit interface implementations via TryReadFloatLike)
        try
        {
            if (_hq != null && TryReadFloatLike(_hq, "AmountStored", out float amtStored))
            {
                bool wholeValues = true;
                TryReadBoolLike(_hq, "WholeValues", out wholeValues);

                int n = wholeValues ? Mathf.FloorToInt(amtStored + 0.0001f) : Mathf.RoundToInt(amtStored);
                best = Mathf.Max(best, n);
            }
        }
        catch { }

        // 2) Count items in storage directly
        try
        {
            if (_storage != null)
            {
	                // NOTE: CURRENCY_PREFAB_ID is declared on PrinterceptorCatalog.
	                // CountStoredByPrefabId expects a Tag, not a string.
	                int st = PrinterceptorStorageUtil.CountStoredByPrefabId(_storage, new Tag(PrinterceptorCatalog.CURRENCY_PREFAB_ID));
                best = Mathf.Max(best, st);
            }
        }
        catch { }

        // 3) Parse the rendered header label (most reliable match to vanilla UI)
        try
        {
            string header = PrinterceptorUIHelpers.ReadRenderedText(SourceHeaderLabel);
            if (!string.IsNullOrEmpty(header) && PrinterceptorUIHelpers.TryExtractLeadingInt(header, out int parsed))
            {
                best = Mathf.Max(best, parsed);
            }
        }
        catch { }

        return best < 0 ? 0 : best;
    }


    private string GetSuffix()
    {
        if (_suffixInit) return _suffix;

        _suffixInit = true;
        _suffix = null;

        try
        {
            // Prefer the localized suffix from the header
            if (SourceHeaderLabel != null)
            {
                var txt = PrinterceptorUIHelpers.ReadRenderedText(SourceHeaderLabel) ?? SourceHeaderLabel.text;
                var s = PrinterceptorUIHelpers.ExtractWalletSuffix(txt);
                if (!string.IsNullOrEmpty(s))
                    _suffix = CleanSuffix(s);
            }
        }
        catch { }

        if (string.IsNullOrEmpty(_suffix))
            _suffix = "Stored Data Banks";

        return _suffix;
    }

    private static string CleanSuffix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Trim();
        if (s.EndsWith(":")) s = s.Substring(0, s.Length - 1).Trim();
        return s;
    }

    private static HijackedHeadquarters.Instance ResolveHijackedHQInstance(PrinterceptorScreen screen)
    {
        if (screen == null) return null;

        // Prefer screen.target: reliable and avoids UnityEngine.Object generic constraint issues when assemblies mismatch.
        object target = null;
        try { target = Traverse.Create(screen).Field<object>("target").Value; } catch { }

        if (target is HijackedHeadquarters.Instance hi)
            return hi;

        // target may be a Component or a wrapper around the real instance
        if (target is Component c && c != null)
        {
            var hiFromComp = c.GetComponent<HijackedHeadquarters.Instance>();
            if (hiFromComp != null) return hiFromComp;
        }

        return TryFindHijackedHQOnObject(target);
    }

private static HijackedHeadquarters.Instance TryFindHijackedHQOnObject(object obj)
    {
        if (obj == null) return null;

        try
        {
            if (obj is HijackedHeadquarters.Instance hi) return hi;

            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var f in t.GetFields(BF))
            {
                if (f == null) continue;
                if (!typeof(HijackedHeadquarters.Instance).IsAssignableFrom(f.FieldType)) continue;

                var v = f.GetValue(obj) as HijackedHeadquarters.Instance;
                if (v != null) return v;
            }

            foreach (var p in t.GetProperties(BF))
            {
                if (p == null || !p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!typeof(HijackedHeadquarters.Instance).IsAssignableFrom(p.PropertyType)) continue;

                var v = p.GetValue(obj, null) as HijackedHeadquarters.Instance;
                if (v != null) return v;
            }
        }
        catch { }

        return null;
    }

    private static Storage ResolveStorage(object instance)
    {
        if (instance == null) return null;

        try
        {
            // Most likely field name for the Printerceptor
            var f = AccessTools.Field(instance.GetType(), "m_storage");
            if (f != null && typeof(Storage).IsAssignableFrom(f.FieldType))
            {
                var s = f.GetValue(instance) as Storage;
                if (s != null) return s;
            }

            // Fallback: any Storage field
            var fields = instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var fi in fields)
            {
                if (fi == null) continue;
                if (!typeof(Storage).IsAssignableFrom(fi.FieldType)) continue;

                var s = fi.GetValue(instance) as Storage;
                if (s != null) return s;
            }
        }
        catch { }

        // Very last chance: component lookup (if instance is also a Component)
        try
        {
            if (instance is Component c && c != null)
            {
                var st = c.GetComponent<Storage>();
                if (st != null) return st;

                st = c.GetComponentInChildren<Storage>(true);
                if (st != null) return st;

                st = c.GetComponentInParent<Storage>();
                if (st != null) return st;
            }
        }
        catch { }

        return null;
    }

private static bool TryReadFloatLike(object instance, string propName, out float value)
    {
        value = 0f;
        if (instance == null || string.IsNullOrEmpty(propName))
            return false;

        var t = instance.GetType();

        // 1) Direct property / field on the concrete type
        try
        {
            var p = AccessTools.Property(t, propName);
            if (p != null && p.CanRead)
            {
                var o = p.GetValue(instance, null);
                if (TryCoerceFloat(o, out value))
                    return true;
            }
        }
        catch { }

        try
        {
            var f = AccessTools.Field(t, propName);
            if (f != null)
            {
                var o = f.GetValue(instance);
                if (TryCoerceFloat(o, out value))
                    return true;
            }
        }
        catch { }

        // 2) Interface property (covers explicit interface implementations)
        try
        {
            foreach (var iface in t.GetInterfaces())
            {
                var ip = iface.GetProperty(propName);
                if (ip == null || !ip.CanRead)
                    continue;

                var o = ip.GetValue(instance, null);
                if (TryCoerceFloat(o, out value))
                    return true;
            }
        }
        catch { }

        return false;
    }


    private static bool TryCoerceFloat(object o, out float value)
    {
        value = 0f;
        if (o == null) return false;

        if (o is float f) { value = f; return true; }
        if (o is double d) { value = (float)d; return true; }
        if (o is decimal dec) { value = (float)dec; return true; }
        if (o is int i) { value = i; return true; }
        if (o is long l) { value = l; return true; }
        if (o is short s) { value = s; return true; }
        if (o is uint ui) { value = ui; return true; }
        if (o is ulong ul) { value = ul; return true; }

        try { value = Convert.ToSingle(o); return true; } catch { value = 0f; return false; }
    }

    private static bool TryCoerceBool(object o, out bool value)
    {
        value = false;
        if (o == null) return false;

        if (o is bool b) { value = b; return true; }
        if (o is int i) { value = i != 0; return true; }
        if (o is float f) { value = Math.Abs(f) > 0.0001f; return true; }

        try { value = Convert.ToBoolean(o); return true; } catch { value = false; return false; }
    }


    private static bool TryReadBoolLike(object instance, string propName, out bool value)
    {
        value = false;
        if (instance == null || string.IsNullOrEmpty(propName)) return false;

        try
        {
            var t = instance.GetType();
            var p = AccessTools.Property(t, propName);
            if (p != null && p.CanRead && p.PropertyType == typeof(bool))
            {
                value = (bool)p.GetValue(instance, null);
                return true;
            }
        }
        catch { }

        try
        {
            var t = instance.GetType();
            var m = AccessTools.Method(t, "get_" + propName);
            if (m != null && m.ReturnType == typeof(bool))
            {
                value = (bool)m.Invoke(instance, null);
                return true;
            }
        }
        catch { }

        return false;
    }
}

internal static class PrinterceptorUIHelpers

    {
                
        // Avoid direct reference to TextAnchor (prevents missing UnityEngine.TextRenderingModule ref in mod projects)
        public static void TrySetChildAlignment(HorizontalLayoutGroup hlg, string alignmentName)
        {
            if (hlg == null || string.IsNullOrEmpty(alignmentName)) return;

            try
            {
                var prop = AccessTools.Property(hlg.GetType(), "childAlignment");
                if (prop == null || !prop.CanWrite) return;

                var enumType = prop.PropertyType;
                object val = null;

                try { val = Enum.Parse(enumType, alignmentName); } catch { val = null; }

                if (val != null)
                    prop.SetValue(hlg, val, null);
            }
            catch { }
        }
public static Storage TryGetPrinterStorage(PrinterceptorScreen screen)
                {
                    if (screen == null) return null;

                    object target = null;
                    try { target = Traverse.Create(screen).Field<object>("target").Value; } catch { }

                    // 1) Storage held by a field on the target (ex: m_storage)
                    var st = TryFindStorageOnObject(target);
                    if (st != null) return st;

                    // 2) Target is a component / gameobject in the world
                    if (target is Component c && c != null)
                    {
                        st = c.GetComponent<Storage>();
                        if (st != null) return st;

                        st = c.GetComponentInChildren<Storage>(true);
                        if (st != null) return st;

                        st = c.GetComponentInParent<Storage>();
                        if (st != null) return st;

                        // Sometimes the instance holds a storage field rather than a component
                        st = TryFindStorageOnObject(c);
                        if (st != null) return st;
                    }

                    if (target is GameObject go && go != null)
                    {
                        st = go.GetComponent<Storage>();
                        if (st != null) return st;

                        st = go.GetComponentInChildren<Storage>(true);
                        if (st != null) return st;

                        st = go.GetComponentInParent<Storage>();
                        if (st != null) return st;
                    }

                    return null;
                }

                        public static bool TryGetStoredCurrencyCount(PrinterceptorScreen screen, Tag currencyTag, out int count)
        {
            count = 0;
            if (screen == null)
                return false;

            try
            {
                // Prefer the value shown on the building SideScreen ("X Stored Data Banks").
                // This comes from the Printerceptor instance (IUserControlledCapacity.AmountStored).
                if (IsDataBankTag(currencyTag))
                {
                    int stored;
                    if (TryGetAmountStoredFromTarget(screen, out stored))
                    {
                        count = stored;
                        return true;
                    }
                }

                var storage = TryGetPrinterStorage(screen);
                if (storage == null)
                    return false;

                count = PrinterceptorStorageUtil.CountStoredByPrefabId(storage, currencyTag);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDataBankTag(Tag t)
        {
            try
            {
                var s = t.ToString();
                return !string.IsNullOrEmpty(s) && s.Equals(PrinterceptorCatalog.CURRENCY_PREFAB_ID, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool TryGetAmountStoredFromTarget(PrinterceptorScreen screen, out int count)
        {
            count = 0;
            if (screen == null) return false;

            object target = null;
            try { target = Traverse.Create(screen).Field<object>("target").Value; } catch { target = null; }
            if (target == null) return false;

            float amt;
            if (!TryReadFloatLike(target, "AmountStored", out amt))
                return false;

            bool whole = false;
            bool tmp;
            if (TryReadBoolLike(target, "WholeValues", out tmp)) whole = tmp;
            if (TryReadBoolLike(target, "wholeValues", out tmp)) whole = tmp;

            int v = whole ? Mathf.RoundToInt(amt) : Mathf.FloorToInt(amt + 0.0001f);
            if (v < 0) v = 0;
            count = v;
            return true;
        }

        private static bool TryReadFloatLike(object instance, string propName, out float value)
        {
            value = 0f;
            if (instance == null || string.IsNullOrEmpty(propName)) return false;

            try
            {
                var t = instance.GetType();
                var p = AccessTools.Property(t, propName);
                if (p != null && p.CanRead)
                {
                    var o = p.GetValue(instance, null);
                    if (o is float f) { value = f; return true; }
                    if (o is int i) { value = i; return true; }
                    if (o is double d) { value = (float)d; return true; }
                }
            }
            catch { }

            try
            {
                var t = instance.GetType();
                var m = AccessTools.Method(t, "get_" + propName);
                if (m != null)
                {
                    var o = m.Invoke(instance, null);
                    if (o is float f) { value = f; return true; }
                    if (o is int i) { value = i; return true; }
                    if (o is double d) { value = (float)d; return true; }
                }
            }
            catch { }

            return false;
        }

        private static bool TryReadBoolLike(object instance, string propName, out bool value)
        {
            value = false;
            if (instance == null || string.IsNullOrEmpty(propName)) return false;

            try
            {
                var t = instance.GetType();
                var p = AccessTools.Property(t, propName);
                if (p != null && p.CanRead && p.PropertyType == typeof(bool))
                {
                    value = (bool)p.GetValue(instance, null);
                    return true;
                }
            }
            catch { }

            try
            {
                var t = instance.GetType();
                var m = AccessTools.Method(t, "get_" + propName);
                if (m != null && m.ReturnType == typeof(bool))
                {
                    value = (bool)m.Invoke(instance, null);
                    return true;
                }
            }
            catch { }

            return false;
        }

                public static int TryParseFirstInt(string s, int fallback)
                {
                    if (string.IsNullOrEmpty(s)) return fallback;

                    try
                    {
                        int i = 0;
                        while (i < s.Length && !char.IsDigit(s[i])) i++;
                        if (i >= s.Length) return fallback;

                        int v = 0;
                        while (i < s.Length && char.IsDigit(s[i]))
                        {
                            v = (v * 10) + (s[i] - '0');
                            i++;
                        }
                        return v;
                    }
                    catch
                    {
                        return fallback;
                    }
                }

                public static string ExtractWalletSuffix(string text)
                {
                    if (string.IsNullOrEmpty(text)) return null;

                    try
                    {
                        text = text.Trim();
                        if (text.Length == 0) return null;

                        // "27 Stored Data Banks" -> "Stored Data Banks"
                        int i = 0;
                        while (i < text.Length && char.IsDigit(text[i])) i++;
                        if (i > 0)
                        {
                            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                            var suffix = (i < text.Length) ? text.Substring(i).Trim() : null;
                            if (!string.IsNullOrEmpty(suffix)) return suffix;
                        }

                        // "Data Banks Available: 27" -> "Data Banks Available"
                        int idx = text.IndexOf(':');
                        if (idx >= 0)
                        {
                            var prefix = text.Substring(0, idx).Trim();
                            if (prefix.EndsWith(":")) prefix = prefix.Substring(0, prefix.Length - 1).Trim();
                            if (!string.IsNullOrEmpty(prefix)) return prefix;
                        }

                        return text;
                    }
                    catch
                    {
                        return null;
                    }
                }

                private static Storage TryFindStorageOnObject(object target)
                {
                    if (target == null) return null;

                    try
                    {
                        if (target is Storage s) return s;

                        var t = target.GetType();
                        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                        // Prefer common field names used by ONI (and the Printerceptor)
                        string[] preferred = { "m_storage", "storage", "Storage", "dataStorage", "dataBankStorage" };

                        for (int i = 0; i < preferred.Length; i++)
                        {
                            var name = preferred[i];
                            try
                            {
                                var f = t.GetField(name, BF);
                                if (f != null && typeof(Storage).IsAssignableFrom(f.FieldType))
                                {
                                    var v = f.GetValue(target) as Storage;
                                    if (v != null) return v;
                                }
                            }
                            catch { }
                        }

                        foreach (var f in t.GetFields(BF))
                        {
                            try
                            {
                                if (f == null) continue;
                                if (!typeof(Storage).IsAssignableFrom(f.FieldType)) continue;

                                var v = f.GetValue(target) as Storage;
                                if (v != null) return v;
                            }
                            catch { }
                        }
                    }
                    catch { }

                    return null;
                }

        public static string ExtractStablePrefix(string text, string fallback)
{
    if (string.IsNullOrEmpty(text)) return fallback;

    string core;

    int idx = text.IndexOf(':');
    if (idx >= 0)
    {
        core = text.Substring(0, idx + 1).Trim();
    }
    else
    {
        int end = text.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        while (end >= 0 && char.IsDigit(text[end])) end--;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;

        core = end >= 0 ? text.Substring(0, end + 1).Trim() : fallback;
        if (string.IsNullOrEmpty(core)) core = fallback;
        if (!core.EndsWith(":")) core += ":";
    }

    // Some Printerceptor labels start with a "bundle count" like "100 Databanks: 25".
    // We strip that leading number token to keep the UI clean.
    core = StripLeadingCountToken(core);

    if (string.IsNullOrEmpty(core)) core = fallback;
    return core;
}

public static string StripLeadingCountToken(string s)
{
    if (string.IsNullOrEmpty(s)) return s;

    int i = 0;
    while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

    // optional 'x' prefix (x100)
    if (i < s.Length && (s[i] == 'x' || s[i] == 'X')) i++;

    int digitsStart = i;
    while (i < s.Length && char.IsDigit(s[i])) i++;

    // no leading digits => unchanged
    if (i == digitsStart) return s.Trim();

    int after = i;
    while (after < s.Length && char.IsWhiteSpace(s[after])) after++;

    // don't strip if nothing remains, or if next token is also a number
    if (after >= s.Length) return s.Trim();
    if (char.IsDigit(s[after])) return s.Trim();

    return s.Substring(after).TrimStart();
}

        public static void ConfigureCounterLabel(LocText lt)
        {
            if (lt == null) return;

            var le = lt.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = lt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.flexibleWidth = 1f;

            try
            {
                var t = lt.GetType();

                var pLines = t.GetProperty("maxVisibleLines", BindingFlags.Instance | BindingFlags.Public);
                if (pLines != null && pLines.CanWrite)
                    pLines.SetValue(lt, 1, null);

                var pWrap = t.GetProperty("enableWordWrapping", BindingFlags.Instance | BindingFlags.Public);
                if (pWrap != null && pWrap.CanWrite)
                    pWrap.SetValue(lt, false, null);

                var pOverflow = t.GetProperty("overflowMode", BindingFlags.Instance | BindingFlags.Public);
                if (pOverflow != null && pOverflow.CanWrite)
                {
                    var enumType = pOverflow.PropertyType;
                    object ell = Enum.Parse(enumType, "Ellipsis");
                    pOverflow.SetValue(lt, ell, null);
                }
            }
            catch { }
        }

        public static void ConfigureMultiLineLabel(LocText lt, int maxLines)
        {
            if (lt == null) return;
            if (maxLines < 1) maxLines = 1;

            try
            {
                var le = lt.gameObject.GetComponent<LayoutElement>();
                if (le == null) le = lt.gameObject.AddComponent<LayoutElement>();
                le.minHeight = Mathf.Max(le.minHeight, 38f);
                le.preferredHeight = Mathf.Max(le.preferredHeight, 38f);
                le.flexibleWidth = 1f;

                var t = lt.GetType();

                var pLines = t.GetProperty("maxVisibleLines", BindingFlags.Instance | BindingFlags.Public);
                if (pLines != null && pLines.CanWrite)
                    pLines.SetValue(lt, maxLines, null);

                var pWrap = t.GetProperty("enableWordWrapping", BindingFlags.Instance | BindingFlags.Public);
                if (pWrap != null && pWrap.CanWrite)
                    pWrap.SetValue(lt, true, null);

                var pOverflow = t.GetProperty("overflowMode", BindingFlags.Instance | BindingFlags.Public);
                if (pOverflow != null && pOverflow.CanWrite)
                {
                    var enumType = pOverflow.PropertyType;
                    object ovf = null;
                    try { ovf = Enum.Parse(enumType, "Overflow"); } catch { }
                    if (ovf != null) pOverflow.SetValue(lt, ovf, null);
                }
            }
            catch { }
        }



        public static void HideHeaderWallet(LocText walletLabel, Image[] walletIcons)
        {
            try
            {
                if (walletLabel != null)
                {
                    var c = walletLabel.color; c.a = 0f; walletLabel.color = c;
                }

                if (walletIcons != null)
                {
                    foreach (var img in walletIcons)
                    {
                        if (img == null) continue;
                        var c = img.color; c.a = 0f; img.color = c;
                    }
                }
            }
            catch { }
        }

        public static RectTransform FindDirectChildUnder(RectTransform host, RectTransform target)
        {
            if (host == null || target == null) return null;

            Transform t = target;
            while (t != null && t.parent != null)
            {
                if (t.parent == host)
                    return t as RectTransform;
                t = t.parent;
            }
            return null;
        }
    

        /// <summary>
        /// Reads the *rendered* text for a label (LocText / TMP_Text / UI.Text).
        /// This is more robust than relying on LocText.text, which can be stale depending on how the game updates UI.
        /// </summary>
        public static string ReadRenderedText(Component label)
        {
            if (label == null) return null;

            // 1) If the component itself exposes a string "text" property
            try
            {
                var p = AccessTools.Property(label.GetType(), "text");
                if (p != null && p.CanRead && p.PropertyType == typeof(string))
                {
                    var s = p.GetValue(label, null) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }

            // 2) TMPro text on the same GameObject
            try
            {
                var go = label.gameObject;
                if (go != null)
                {
                    var tmpType = AccessTools.TypeByName("TMPro.TMP_Text") ?? AccessTools.TypeByName("TMPro.TextMeshProUGUI");
                    if (tmpType != null)
                    {
                        var tmp = go.GetComponent(tmpType);
                        if (tmp != null)
                        {
                            var p = AccessTools.Property(tmpType, "text");
                            if (p != null && p.CanRead)
                            {
                                var s = p.GetValue(tmp, null) as string;
                                if (!string.IsNullOrEmpty(s)) return s;
                            }
                        }
                    }

                    var uiText = go.GetComponent<UnityEngine.UI.Text>();
                    if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                        return uiText.text;
                }
            }
            catch { }

            return null;
        }

        public static bool TryExtractLeadingInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                    continue;

                int j = i;
                while (j < text.Length && char.IsDigit(text[j]))
                    j++;

                return int.TryParse(text.Substring(i, j - i), out value);
            }

            return false;
        }
}

    // =========================================================
    // Storage helper: count/consume items by prefabTag (DataBank)
    // =========================================================
    internal static class PrinterceptorStorageUtil
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<Type, FieldInfo> _itemsFieldCache = new Dictionary<Type, FieldInfo>();
        private static readonly Dictionary<Type, MethodInfo> _removeCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> _dropCache = new Dictionary<Type, MethodInfo>();

        public static int CountStoredByPrefabId(Storage storage, Tag prefabTag)
        {
            if (storage == null) return 0;

            if (TryGetItemsList(storage, out var items))
            {
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    var go = items[i];
                    if (go == null) continue;

                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid == null) continue;

                    if (kpid.PrefabTag == prefabTag)
                        count++;
                }
                return count;
            }

            // fallback snapshot
            int fallbackCount = 0;
            var snap = GetStoredItemsSnapshot(storage);
            for (int i = 0; i < snap.Count; i++)
            {
                var go = snap[i];
                if (go == null) continue;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null) continue;

                if (kpid.PrefabTag == prefabTag)
                    fallbackCount++;
            }
            return fallbackCount;
        }

        public static bool ConsumeStoredByPrefabId(Storage storage, Tag prefabTag, int amount)
        {
            if (storage == null) return false;
            if (amount <= 0) return true;

            int remaining = amount;

            if (TryGetItemsList(storage, out var items))
            {
                for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    var go = items[i];
                    if (go == null) continue;

                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid == null) continue;

                    if (kpid.PrefabTag != prefabTag)
                        continue;

                    TryRemoveFromStorage(storage, go);
                    try { Util.KDestroyGameObject(go); } catch { }
                    remaining--;
                }

                return remaining <= 0;
            }

            var snap = GetStoredItemsSnapshot(storage);
            for (int i = 0; i < snap.Count && remaining > 0; i++)
            {
                var go = snap[i];
                if (go == null) continue;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null) continue;

                if (kpid.PrefabTag != prefabTag)
                    continue;

                TryRemoveFromStorage(storage, go);
                try { Util.KDestroyGameObject(go); } catch { }
                remaining--;
            }

            return remaining <= 0;
        }

        private static void TryRemoveFromStorage(Storage storage, GameObject go)
        {
            if (storage == null || go == null) return;

            var t = storage.GetType();

            MethodInfo remove = null;
            MethodInfo drop = null;
            lock (_lock)
            {
                _removeCache.TryGetValue(t, out remove);
                _dropCache.TryGetValue(t, out drop);
            }

            if (remove == null && drop == null)
            {
                try { remove = AccessTools.Method(t, "Remove", new[] { typeof(GameObject) }); } catch { remove = null; }
                try { drop = AccessTools.Method(t, "Drop", new[] { typeof(GameObject) }); } catch { drop = null; }

                lock (_lock)
                {
                    if (!_removeCache.ContainsKey(t)) _removeCache[t] = remove;
                    if (!_dropCache.ContainsKey(t)) _dropCache[t] = drop;
                }
            }

            try
            {
                if (remove != null)
                {
                    remove.Invoke(storage, new object[] { go });
                    return;
                }
            }
            catch { }

            try
            {
                if (drop != null)
                    drop.Invoke(storage, new object[] { go });
            }
            catch { }
        }

        private static bool TryGetItemsList(Storage storage, out IList<GameObject> items)
        {
            items = null;
            if (storage == null) return false;

            FieldInfo f = null;
            var t = storage.GetType();

            lock (_lock)
            {
                _itemsFieldCache.TryGetValue(t, out f);
            }

            if (f == null && !_itemsFieldCache.ContainsKey(t))
            {
                string[] candidateFields = { "items", "storedItems", "itemList" };
                foreach (var fname in candidateFields)
                {
                    try
                    {
                        var fi = AccessTools.Field(t, fname);
                        if (fi == null) continue;

                        if (typeof(IList<GameObject>).IsAssignableFrom(fi.FieldType))
                        {
                            f = fi;
                            break;
                        }
                    }
                    catch { }
                }

                lock (_lock)
                {
                    if (!_itemsFieldCache.ContainsKey(t)) _itemsFieldCache[t] = f; // may be null
                }
            }

            if (f == null) return false;

            object v = null;
            try { v = f.GetValue(storage); } catch { v = null; }

            if (v is IList<GameObject> gl)
            {
                items = gl;
                return true;
            }

            return false;
        }

        private static List<GameObject> GetStoredItemsSnapshot(Storage storage)
        {
            var list = new List<GameObject>();
            if (storage == null) return list;

            var t = storage.GetType();

            // try known fields
            string[] candidateFields = { "items", "storedItems", "itemList" };
            FieldInfo f = null;
            foreach (var fname in candidateFields)
            {
                try
                {
                    var fi = AccessTools.Field(t, fname);
                    if (fi == null) continue;
                    if (typeof(IEnumerable).IsAssignableFrom(fi.FieldType))
                    {
                        f = fi;
                        break;
                    }
                }
                catch { }
            }

            if (f == null) return list;

            try
            {
                var v = f.GetValue(storage);
                if (v is IEnumerable enumerable)
                {
                    foreach (var o in enumerable)
                        if (o is GameObject go) list.Add(go);
                }
            }
            catch { }

            return list;
        }
    }

    // =========================================================
    // Tabs injection on spawn
    // =========================================================
    [HarmonyPatch(typeof(PrinterceptorScreen), "OnSpawn")]
    public static class PrinterceptorTabs_OnSpawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PrinterceptorScreen __instance)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.gameObject.GetComponent<PrinterceptorTabsState>() != null) return;

                var tr = Traverse.Create(__instance);

                var grid = tr.Field<RectTransform>("optionGridContainer").Value;
                var printBtn = tr.Field<KButton>("printButton").Value;

                LocText walletLabel = null;
                Image[] walletIcons = null;

                try { walletLabel = tr.Field<LocText>("dataWalletLabel").Value; } catch { }
                try { walletIcons = tr.Field<Image[]>("dataWalletIcon").Value; } catch { }

                if (walletIcons == null)
                {
                    try
                    {
                        var single = tr.Field<Image>("dataWalletIcon").Value;
                        if (single != null) walletIcons = new[] { single };
                    }
                    catch { }
                }

                LocText costLabel = null;
                Image costIcon = null;
                try { costLabel = tr.Field<LocText>("selectedCostLabel").Value; } catch { }
                try { costIcon = tr.Field<Image>("selectedCostIcon").Value; } catch { }

                if (grid == null || printBtn == null) return;

                // cache vanilla prefixes
                var cache = __instance.gameObject.GetComponent<PrinterceptorUILabelCache>();
                if (cache == null) cache = __instance.gameObject.AddComponent<PrinterceptorUILabelCache>();

                if (costLabel != null)
                    cache.RequiredPrefix = PrinterceptorUIHelpers.ExtractStablePrefix(costLabel.text, cache.RequiredPrefix);
                if (walletLabel != null)
                    cache.AvailablePrefix = PrinterceptorUIHelpers.ExtractStablePrefix(walletLabel.text, cache.AvailablePrefix);

                RectTransform host = null;
                RectTransform headerRow = walletLabel != null ? (walletLabel.transform.parent as RectTransform) : null;

                if (headerRow != null && headerRow.parent is RectTransform hrp) host = hrp;
                if (host == null) host = grid.parent as RectTransform;
                if (host == null) return;

                RectTransform listRoot = PrinterceptorUIHelpers.FindDirectChildUnder(host, grid);
                if (listRoot == null) listRoot = grid;

                var barGO = new GameObject("Gravitas_PrinterceptorTabsBar", typeof(RectTransform));
                var barRT = (RectTransform)barGO.transform;
                barRT.SetParent(host, false);
                barRT.SetSiblingIndex(listRoot.GetSiblingIndex());

                var hlg = barGO.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 6f;
                hlg.padding = new RectOffset(6, 6, 4, 4);

                var le = barGO.AddComponent<LayoutElement>();
                le.preferredHeight = 38f;

                var state = __instance.gameObject.AddComponent<PrinterceptorTabsState>();
                state.OptionGrid = grid;

                const float TAB_W = 92f;
                const float TAB_H = 34f;

                MakeTabButton(printBtn, barRT, "TOUT", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.All; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "ŒUFS", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.Eggs; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "NUISIBLES", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.Nuisibles; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "GRAINES", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.Seeds; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "GRAINES\nMUTÉES", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.MutatedSeeds; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "RESSOURCES", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.Resources; state.ApplyFilter(__instance); });
                MakeTabButton(printBtn, barRT, "ARTEFACTS", TAB_W, TAB_H, () => { state.Current = PrinterceptorTab.Artifacts; state.ApplyFilter(__instance); });

                state.ApplyFilter(__instance);

                EnsureWalletRowUnderRequired(__instance, walletLabel, walletIcons, costLabel, costIcon);

                LayoutRebuilder.ForceRebuildLayoutImmediate(host);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GravitasTabs] OnSpawn error: {e}");
            }
        }

        private static void EnsureWalletRowUnderRequired(PrinterceptorScreen screen, LocText walletLabel, Image[] walletIcons, LocText costLabel, Image costIcon)
        {
            try
            {
                if (screen == null || costLabel == null || costIcon == null)
                    return;

                var costRow = costLabel.transform.parent as RectTransform;
                if (costRow == null)
                    return;

                // Already created?
                if (costRow.Find("Gravitas_InlineWalletOverlay") != null ||
                    costRow.Find("Gravitas_InlineWalletLabel") != null ||
                    costRow.Find("Gravitas_InlineWalletIcon") != null)
                    return;

                // Keep the vanilla "Required" label readable (single line, flexible)
                PrinterceptorUIHelpers.ConfigureCounterLabel(costLabel);

                // Floating overlay anchored at the RIGHT of the cost row
                var overlayGO = new GameObject("Gravitas_InlineWalletOverlay", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                var overlayRT = (RectTransform)overlayGO.transform;
                overlayRT.SetParent(costRow, false);
                overlayRT.SetAsLastSibling();

                var overlayLE = overlayGO.GetComponent<LayoutElement>();
                if (overlayLE != null) overlayLE.ignoreLayout = true;

                overlayRT.anchorMin = new Vector2(1f, 0.5f);
                overlayRT.anchorMax = new Vector2(1f, 0.5f);
                overlayRT.pivot = new Vector2(1f, 0.5f);

                float h = 24f;
                try { if (costRow.rect.height > 1f) h = costRow.rect.height; } catch { }
                overlayRT.sizeDelta = new Vector2(260f, h);
                overlayRT.anchoredPosition = new Vector2(-6f, 0f);

                var hlg = overlayGO.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 4f;
                hlg.padding = new RectOffset(0, 0, 0, 0);
                PrinterceptorUIHelpers.TrySetChildAlignment(hlg, "MiddleRight");

                var iconGO = UnityEngine.Object.Instantiate(costIcon.gameObject, overlayRT);
                iconGO.name = "Gravitas_InlineWalletIcon";
                var iconImg = iconGO.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    var c = iconImg.color; if (c.a < 1f) c.a = 1f; iconImg.color = c;
                }

                var txtGO = UnityEngine.Object.Instantiate(costLabel.gameObject, overlayRT);
                txtGO.name = "Gravitas_InlineWalletLabel";
                var txt = txtGO.GetComponent<LocText>();
                PrinterceptorUIHelpers.ConfigureCounterLabel(txt);

                // placeholder (updated by GravitasWalletMirror)
                GravitasUIUtil.SetLocText(txt, "0 Stored Data Banks");

                var mirror = txtGO.AddComponent<GravitasWalletMirror>();
                mirror.Screen = screen;
                mirror.DestLabel = txt;
                mirror.DestIcon = iconImg;
                mirror.SourceHeaderLabel = walletLabel;
                mirror.SourceHeaderIcons = walletIcons;

                // Hide vanilla wallet (if present) to avoid duplicates
                PrinterceptorUIHelpers.HideHeaderWallet(walletLabel, walletIcons);

                LayoutRebuilder.ForceRebuildLayoutImmediate(costRow);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasTabs] EnsureWalletRowUnderRequired(inline) error: {e}");
            }
        }


        private static KButton MakeTabButton(KButton template, RectTransform parent, string label, float width, float height, global::System.Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = "Tab_" + label.Replace("\n", "_");

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(width, height);

            var loc = go.GetComponentInChildren<LocText>(true);
            if (loc != null) GravitasUIUtil.SetLocText(loc, label);

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;

            var btn = go.GetComponent<KButton>();
            if (btn != null)
            {
                ClearKButtonOnClick(btn);
                AddKButtonOnClick(btn, onClick);
            }

            return btn;
        }

        private static void ClearKButtonOnClick(KButton btn)
        {
            try
            {
                var f = AccessTools.Field(btn.GetType(), "onClick");
                if (f != null && typeof(global::System.Delegate).IsAssignableFrom(f.FieldType))
                    f.SetValue(btn, null);
            }
            catch { }
        }

        private static void AddKButtonOnClick(KButton btn, global::System.Action onClick)
        {
            if (btn == null || onClick == null) return;

            try
            {
                var f = AccessTools.Field(btn.GetType(), "onClick");
                if (f != null && typeof(global::System.Delegate).IsAssignableFrom(f.FieldType))
                {
                    var del = global::System.Delegate.CreateDelegate(f.FieldType, onClick.Target, onClick.Method);
                    f.SetValue(btn, del);
                    return;
                }
            }
            catch { }

            // fallback: try event
            try
            {
                var ev = btn.GetType().GetEvent("onClick");
                if (ev != null)
                {
                    var del = global::System.Delegate.CreateDelegate(ev.EventHandlerType, onClick.Target, onClick.Method);
                    ev.AddEventHandler(btn, del);
                }
            }
            catch { }
        }
    }

    // =========================================================
    // Add custom items into the option grid
    // =========================================================
    [HarmonyPatch(typeof(PrinterceptorScreen), "SpawnOptionButtons")]
    public static class PrinterceptorAddCustomItemsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PrinterceptorScreen __instance)
        {
            if (__instance == null) return;

            var tr = Traverse.Create(__instance);

            Dictionary<Tag, MultiToggle> optionButtons = null;
            try { optionButtons = tr.Field<Dictionary<Tag, MultiToggle>>("optionButtons").Value; } catch { optionButtons = null; }
            if (optionButtons == null) return;

            var spawnMethod = AccessTools.Method(typeof(PrinterceptorScreen), "SpawnOptionButton", new[] { typeof(Tag) });
            if (spawnMethod == null)
            {
                Debug.LogError("[GravitasMod] PrinterceptorScreen.SpawnOptionButton(Tag) introuvable.");
                return;
            }

            Dictionary<Tag, int> overrides = null;
            try
            {
                overrides = Traverse.Create(typeof(HijackedHeadquartersConfig))
                    .Field<Dictionary<Tag, int>>("PrintableCostOverrides").Value;
            }
            catch { overrides = null; }

            AddIds(__instance, spawnMethod, optionButtons, overrides, PrinterceptorCatalog.ArtifactPrefabIds, 50);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Mosquito", "BeeBaby", "Glom", "Mole" }, 250);
            // Resources (curated + tuned pricing; default UI shows Data Banks)
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "IronOre", "Cuprite" }, 20);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "GoldAmalgam", "AluminumOre" }, 25);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "WoodLog" }, 35);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "UraniumOre" }, 80);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Steel" }, 150);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Diamond" }, 600);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "ViscoGel", "Iridium" }, 400);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "SuperCoolant" }, 500);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Plastium" }, 800);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Isoresin" }, 1500);
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { "Insulation" }, 3000);

// Gassy Moo
                        // Gassy Moo (cost tuned)
            AddIds(__instance, spawnMethod, optionButtons, overrides, new[] { PrinterceptorCatalog.GASSY_MOO_ID }, 25);

            RectTransform grid = null;
            try { grid = tr.Field<RectTransform>("optionGridContainer").Value; } catch { grid = null; }
            if (grid != null) LayoutRebuilder.ForceRebuildLayoutImmediate(grid);

            var state = __instance.gameObject.GetComponent<PrinterceptorTabsState>();
            if (state != null) state.ApplyFilter(__instance);
        }

        private static void AddIds(PrinterceptorScreen screen, MethodInfo spawnButtonMethod,
            Dictionary<Tag, MultiToggle> optionButtons,
            Dictionary<Tag, int> overrides,
            string[] ids,
            int cost)
        {
            if (screen == null || spawnButtonMethod == null || optionButtons == null || ids == null) return;

            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;

                Tag t;
                try { t = TagManager.Create(id); }
                catch { t = new Tag(id); }
                if (optionButtons.ContainsKey(t))
                    continue;

                GameObject prefab = null;
                try { prefab = Assets.GetPrefab(t); } catch { prefab = null; }
                if (prefab == null)
                {
                    Debug.LogWarning($"[GravitasMod] Prefab introuvable, ignoré: {id}");
                    continue;
                }

                try
                {
                    spawnButtonMethod.Invoke(screen, new object[] { t });

                    if (overrides != null)
                    {
                        if (overrides.ContainsKey(t))
                            overrides[t] = cost;
                        else
                            overrides.Add(t, cost);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GravitasMod] Impossible d’ajouter {id}: {e}");
                }
            }
        }
    }

    // =========================================================
    // Icons: fix alt icon for artifacts (right = left)
    // =========================================================
    [HarmonyPatch(typeof(PrinterceptorScreen), "SelectEntity")]
    public static class PrinterceptorArtifactAltIconFixPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PrinterceptorScreen __instance, Tag __0)
        {
            if (__instance == null) return;
            if (!PrinterceptorCatalog.IsArtifact(__0)) return;

            var tr = Traverse.Create(__instance);

            Image left = null;
            Image right = null;
            try { left = tr.Field<Image>("selectedIcon").Value; } catch { }
            try { right = tr.Field<Image>("selectedIconAlt").Value; } catch { }

            if (left == null || right == null) return;
            if (left.sprite == null) return;

            right.sprite = left.sprite;
            right.enabled = true;
            right.preserveAspect = true;

            var c = right.color; if (c.a < 1f) c.a = 1f; right.color = c;
        }
    }


// =========================================================
// UI: remove leading bundle count ("100 ") from cost label
// Example: "100 Databanks: 25" -> "Databanks: 25"
// =========================================================
[HarmonyPatch(typeof(PrinterceptorScreen), "SelectEntity")]
internal static class Printerceptor_SelectEntity_CleanCostLabelPrefix_Patch
{
    [HarmonyPostfix]
    private static void Postfix(PrinterceptorScreen __instance)
    {
        try
        {
            if (__instance == null) return;

            LocText costLabel = null;
            try { costLabel = Traverse.Create(__instance).Field<LocText>("selectedCostLabel").Value; } catch { costLabel = null; }
            if (costLabel == null) return;

            var original = costLabel.text;
            if (string.IsNullOrEmpty(original)) return;

            // Only strip if there is a ':' somewhere (we don't want to touch pure numeric labels)
            if (original.IndexOf(':') < 0) return;

            var cleaned = PrinterceptorUIHelpers.StripLeadingCountToken(original);
            if (!string.IsNullOrEmpty(cleaned) && cleaned != original)
                GravitasUIUtil.SetLocText(costLabel, cleaned);
        }
        catch { }
    }
}
    // Icons: fix alt icon for seed-like (avoid white square)
    [HarmonyPatch(typeof(PrinterceptorScreen), "SelectEntity")]
    public static class PrinterceptorSeedLikeAltIconFixPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PrinterceptorScreen __instance, Tag __0)
        {
            if (__instance == null) return;
            if (PrinterceptorCatalog.IsArtifact(__0)) return;

            GameObject prefab = null;
            try { prefab = Assets.GetPrefab(__0); } catch { prefab = null; }
            if (!SeedLikeUtil.IsSeedLikePrefab(prefab)) return;

            var tr = Traverse.Create(__instance);

            Image left = null;
            Image right = null;
            try { left = tr.Field<Image>("selectedIcon").Value; } catch { }
            try { right = tr.Field<Image>("selectedIconAlt").Value; } catch { }

            if (left == null || right == null) return;

            if (right.sprite == null && left.sprite != null) right.sprite = left.sprite;
            else if (left.sprite == null && right.sprite != null) left.sprite = right.sprite;

            if (left.sprite != null)
            {
                left.enabled = true;
                left.preserveAspect = true;
                var c = left.color; if (c.a < 1f) c.a = 1f; left.color = c;
            }
            if (right.sprite != null)
            {
                right.enabled = true;
                right.preserveAspect = true;
                var c = right.color; if (c.a < 1f) c.a = 1f; right.color = c;
            }
        }
    }

    // =========================================================
// =========================================================
// ✅ Sync visuel des boutons (MultiToggle) avec la sélection réelle
// - Corrige le cas Moo: le bouton ne s'allumait pas car on bypassait SelectEntity vanilla.
// - S'applique aussi si un autre bouton est cliqué après: on force l'état UI cohérent.
// =========================================================
// =========================================================
    

// =========================================================
// Safety: ensure PrintCounts dictionary has a key for newly injected options
// This avoids vanilla SelectEntity/GetPrintCount crashing with KeyNotFoundException
// =========================================================
[HarmonyPatch(typeof(PrinterceptorScreen), "SelectEntity")]
internal static class Printerceptor_SelectEntity_EnsurePrintCountKey_Patch
{
    [HarmonyPrefix]
    private static void Prefix(PrinterceptorScreen __instance, Tag __0)
    {
        try
        {
            if (__instance == null) return;

            // Only for injected custom printables (resources/nuisibles/moo) to keep risk low
            if (!PrinterceptorCatalog.IsCustomPrint(__0))
                return;

            // Try screen-side dictionary first (if present in this version)
            TryEnsureKeyOnObject(__instance, __0);

            // Try on the building instance referenced by the screen target (common implementation)
            object target = null;
            try { target = Traverse.Create(__instance).Field<object>("target").Value; } catch { target = null; }

            Component comp = target as Component;
            if (comp == null && target is GameObject go) comp = go != null ? go.GetComponent<Component>() : null;

            if (comp != null)
            {
                var hq = comp.GetComponent<HijackedHeadquarters.Instance>();
                if (hq != null)
                    TryEnsureKeyOnObject(hq, __0);
            }
        }
        catch { }
    }

    private static void TryEnsureKeyOnObject(object obj, Tag key)
    {
        if (obj == null) return;

        try
        {
            // Some Printerceptor code paths assume printCounts is non-null.
            // For injected "custom" items (like our Resources tab), this dictionary can be null depending on init order.
            // Make sure it exists before the vanilla method touches it.
            var tr = Traverse.Create(obj);

            Traverse<Dictionary<Tag, int>> field = null;
            Dictionary<Tag, int> dict = null;

            try
            {
                field = tr.Field<Dictionary<Tag, int>>("printCounts");
                dict = field.Value;
            }
            catch { dict = null; }

            // Fallback for potential internal renames across ONI builds/modded variants
            if (dict == null)
            {
                try
                {
                    var altField = tr.Field<Dictionary<Tag, int>>("m_printCounts");
                    var altDict = altField.Value;
                    if (altDict != null)
                    {
                        field = altField;
                        dict = altDict;
                    }
                }
                catch { }
            }

            if (dict == null)
            {
                dict = new Dictionary<Tag, int>();

                // Try to assign back to whichever field we have (or the common name).
                try
                {
                    if (field == null)
                        field = tr.Field<Dictionary<Tag, int>>("printCounts");

                    field.Value = dict;
                }
                catch
                {
                    try
                    {
                        tr.Field<Dictionary<Tag, int>>("m_printCounts").Value = dict;
                    }
                    catch { return; }
                }
            }

            if (!dict.ContainsKey(key))
                dict[key] = 0;
        }
        catch { }
    }

}


    // =========================================================
    // ✅ FIX: Prevent NRE when selecting injected custom options (Resources / Nuisibles / Moo)
    // Vanilla PrinterceptorScreen.SelectEntity can assume internal data exists for its own catalog,
    // but injected buttons may not have all backing data, leading to a NullReferenceException.
    // We safely handle selection for our injected tags and skip the vanilla method for those tags.
    // =========================================================
    [HarmonyPatch(typeof(PrinterceptorScreen), "SelectEntity")]
    internal static class Printerceptor_SelectEntity_CustomInjected_SafeBypass_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(PrinterceptorScreen __instance, Tag __0)
        {
            try
            {
                if (__instance == null)
                    return true;

                // Only bypass for our injected custom prints; everything else remains vanilla.
                if (!PrinterceptorCatalog.IsCustomPrint(__0))
                    return true;

                TrySetSelectedEntityTag(__instance, __0);

                UpdateSelectedTexts(__instance, __0);
                UpdateSelectedIconsFromOptionButton(__instance, __0);
                UpdateSelectedCostLabel(__instance, __0);
                UpdateSelectedPrintAmountLabel(__instance, __0);
                TryEnablePrintButton(__instance);

                return false; // Skip vanilla SelectEntity (prevents NRE)
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] Safe SelectEntity fallback failed for '{__0}': {e}");
                return true; // Fail-open to vanilla
            }
        }

        private static void TrySetSelectedEntityTag(PrinterceptorScreen screen, Tag tag)
        {
            try
            {
                // Field is private in vanilla, so use reflection (Field/Property both supported).
                var f = AccessTools.Field(screen.GetType(), "selectedEntityTag");
                if (f != null)
                {
                    f.SetValue(screen, tag);
                    return;
                }

                var p = AccessTools.Property(screen.GetType(), "selectedEntityTag");
                if (p != null && p.CanWrite)
                {
                    p.SetValue(screen, tag, null);
                }
            }
            catch { }
        }

        private static void UpdateSelectedTexts(PrinterceptorScreen screen, Tag tag)
        {
            try
            {
                var tr = Traverse.Create(screen);

                LocText titleText = null;
                LocText flavourText = null;
                LocText effectsText = null;

                try { titleText = tr.Field<LocText>("selectedTitleText").Value; } catch { }
                try { flavourText = tr.Field<LocText>("selectedFlavourText").Value; } catch { }
                if (flavourText == null)
                {
                    try { flavourText = tr.Field<LocText>("selectedFlavorText").Value; } catch { }
                }
                try { effectsText = tr.Field<LocText>("selectedEffectsText").Value; } catch { }

                string title = tag.ToString();

                try
                {
                    var prefab = Assets.GetPrefab(tag);
                    if (prefab != null)
                    {
                        var kpid = prefab.GetComponent<KPrefabID>();
                        if (kpid != null)
                            title = kpid.GetProperName();
                    }
                }
                catch { }

                string flavour = string.Empty;
                if (PrinterceptorCatalog.IsResource(tag))
                {
                    int kg;
                    if (PrinterceptorCatalog.TryGetResourcePackKg(tag, out kg))
                        flavour = $"Quantité : {kg} kg";
                        // Arm a short-lived resource pack override so the printed debris spawns with the correct mass.
                        // (Applied in Util/GameUtil.KInstantiate postfix before the object becomes active)
                        GravitasResourcePackPrintArm.Arm(screen, tag, kg);

                }

                if (titleText != null) GravitasUIUtil.SetLocText(titleText, title);
                bool wroteQtyToEffects = false;
                if (flavourText != null)
                {
                    GravitasUIUtil.SetLocText(flavourText, flavour);
                }
                else if (!string.IsNullOrEmpty(flavour) && effectsText != null)
                {
                    GravitasUIUtil.SetLocText(effectsText, flavour);
                    wroteQtyToEffects = true;
                }
                if (effectsText != null && !wroteQtyToEffects)
                    GravitasUIUtil.SetLocText(effectsText, string.Empty);
            }
            catch { }
        }

                
private static void UpdateSelectedIconsFromOptionButton(PrinterceptorScreen screen, Tag tag)
{
    try
    {
        // selectedIcon / selectedIconAlt can be Image OR KImage depending on ONI version/UI.
        object selectedIconObj = AccessTools.Field(screen.GetType(), "selectedIcon")?.GetValue(screen);
        object selectedIconAltObj = AccessTools.Field(screen.GetType(), "selectedIconAlt")?.GetValue(screen);

        if (selectedIconObj == null && selectedIconAltObj == null)
            return;

        Sprite sprite = null;

        // 1) Prefer: copy the exact item icon used by the option button.
        // (This avoids accidentally picking the currency icon used in the cost row.)
        try
        {
            var dict = Traverse.Create(screen).Field<Dictionary<Tag, MultiToggle>>("optionButtons").Value;
            if (dict != null && dict.TryGetValue(tag, out var mt) && mt != null)
                sprite = FindBestSprite(mt.gameObject);
        }
        catch { }

        // 2) Fallback: Def.GetUISprite (handles many prefabs/elements)
        if (sprite == null)
            sprite = TryGetSpriteFromDef(tag);

        if (sprite == null)
            return;

        // Apply to both without assuming which one is currently shown.
        ApplySpriteToIconObject(selectedIconObj, sprite, forceActive: true, forceEnabled: true);
        ApplySpriteToIconObject(selectedIconAltObj, sprite, forceActive: true, forceEnabled: true);
    }
    catch { }
}

private static Sprite TryGetSpriteFromDef(Tag tag)
        {
            try
            {
                var prefab = Assets.GetPrefab(tag);
                var defType = AccessTools.TypeByName("Def");
                if (defType == null) return null;

                var methods = defType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "GetUISprite") continue;

                    object result = null;
                    try
                    {
                        var p = m.GetParameters();

                        if (p.Length == 1 && p[0].ParameterType == typeof(Tag))
                            result = m.Invoke(null, new object[] { tag });
                        else if (p.Length == 1 && p[0].ParameterType == typeof(GameObject) && prefab != null)
                            result = m.Invoke(null, new object[] { prefab });
                        else if (p.Length == 2 && p[0].ParameterType == typeof(Tag) && p[1].ParameterType == typeof(bool))
                            result = m.Invoke(null, new object[] { tag, false });
                        else if (p.Length == 2 && p[0].ParameterType == typeof(GameObject) && p[1].ParameterType == typeof(bool) && prefab != null)
                            result = m.Invoke(null, new object[] { prefab, false });
                    }
                    catch { result = null; }

                    var sprite = ExtractSprite(result);
                    if (sprite != null) return sprite;
                }
            }
            catch { }

            return null;
        }

        private static Sprite ExtractSprite(object uiSprite)
        {
            if (uiSprite == null) return null;
            if (uiSprite is Sprite s) return s;

            try
            {
                var t = uiSprite.GetType();

                // Klei sometimes returns a pair/tuple-like object with field/property "first" as Sprite
                var fFirst = AccessTools.Field(t, "first");
                if (fFirst != null && fFirst.FieldType == typeof(Sprite))
                    return (Sprite)fFirst.GetValue(uiSprite);

                var pFirst = AccessTools.Property(t, "first");
                if (pFirst != null && pFirst.PropertyType == typeof(Sprite))
                    return (Sprite)pFirst.GetValue(uiSprite, null);

                var fSprite = AccessTools.Field(t, "sprite");
                if (fSprite != null && fSprite.FieldType == typeof(Sprite))
                    return (Sprite)fSprite.GetValue(uiSprite);

                var pSprite = AccessTools.Property(t, "sprite");
                if (pSprite != null && pSprite.PropertyType == typeof(Sprite))
                    return (Sprite)pSprite.GetValue(uiSprite, null);
            }
            catch { }

            return null;
        }

        private static void ApplySpriteToIconObject(object iconObj, Sprite sprite, bool forceActive, bool forceEnabled)
        {
            if (iconObj == null || sprite == null) return;

            // Unity Image
            if (iconObj is Image img)
            {
                img.sprite = sprite;
                if (forceEnabled) img.enabled = true;
                img.preserveAspect = true;

                var c = img.color;
                if (c.a < 1f) { c.a = 1f; img.color = c; }

                if (forceActive && img.gameObject != null) img.gameObject.SetActive(true);
                return;
            }

            // KImage (or similar) via reflection
            try
            {
                var t = iconObj.GetType();

                // sprite
                var pSprite = AccessTools.Property(t, "sprite") ?? AccessTools.Property(t, "Sprite");
                if (pSprite != null && pSprite.CanWrite && pSprite.PropertyType == typeof(Sprite))
                    pSprite.SetValue(iconObj, sprite, null);
                else
                {
                    var fSprite = AccessTools.Field(t, "sprite") ?? AccessTools.Field(t, "Sprite");
                    if (fSprite != null && fSprite.FieldType == typeof(Sprite))
                        fSprite.SetValue(iconObj, sprite);
                }

                // enabled
                if (forceEnabled)
                {
                    var pEnabled = AccessTools.Property(t, "enabled");
                    if (pEnabled != null && pEnabled.CanWrite && pEnabled.PropertyType == typeof(bool))
                        pEnabled.SetValue(iconObj, true, null);
                }

                // preserveAspect (best-effort)
                var pPA = AccessTools.Property(t, "preserveAspect");
                if (pPA != null && pPA.CanWrite && pPA.PropertyType == typeof(bool))
                    pPA.SetValue(iconObj, true, null);

                // alpha = 1 (best-effort)
                var pColor = AccessTools.Property(t, "color");
                if (pColor != null && pColor.CanRead && pColor.PropertyType == typeof(Color) && pColor.CanWrite)
                {
                    var c = (Color)pColor.GetValue(iconObj, null);
                    if (c.a < 1f) { c.a = 1f; pColor.SetValue(iconObj, c, null); }
                }

                if (forceActive && iconObj is Component comp && comp.gameObject != null)
                    comp.gameObject.SetActive(true);
            }
            catch { }
        }

                
private static Sprite FindBestSprite(GameObject root)
{
    if (root == null) return null;

    Sprite bestSprite = null;
    float bestScore = float.MinValue;

    // Local helpers (avoid extra methods / keep changes isolated)
    string BuildPath(Component c)
    {
        try
        {
            if (c == null || c.transform == null) return string.Empty;
            var t = c.transform;
            string path = t.name;
            int guard = 0;
            while (t.parent != null && guard++ < 5)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    bool LooksLikeCost(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.ToLowerInvariant();
        return s.Contains("cost") || s.Contains("price") || s.Contains("wallet") ||
               s.Contains("currency") || s.Contains("required") || s.Contains("available") ||
               s.Contains("databank") || s.Contains("data bank") || s.Contains("bank") ||
               s.Contains("count") || s.Contains("qty");
    }

    bool LooksLikeIcon(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.ToLowerInvariant();
        return s.Contains("icon") || s.Contains("preview") || s.Contains("portrait") || s.Contains("item");
    }

    float RectArea(Component c)
    {
        try
        {
            if (c is RectTransform rt)
                return Mathf.Abs(rt.rect.width) * Mathf.Abs(rt.rect.height);
            if (c != null && c.transform is RectTransform rt2)
                return Mathf.Abs(rt2.rect.width) * Mathf.Abs(rt2.rect.height);
        }
        catch { }
        return 0f;
    }

    void ConsiderCandidate(Sprite sprite, Component comp, bool enabled, bool active)
    {
        if (sprite == null) return;

        string name = comp != null && comp.gameObject != null ? comp.gameObject.name : string.Empty;
        string path = BuildPath(comp);

        bool costLike = LooksLikeCost(name) || LooksLikeCost(path);
        bool iconLike = LooksLikeIcon(name) || LooksLikeIcon(path);

        float area = RectArea(comp);

        float score = 0f;
        if (enabled) score += 1000f;
        if (active) score += 1000f;

        // Area is important (main item icon is usually the biggest on the option button)
        score += Mathf.Clamp(area, 0f, 20000f);

        if (iconLike) score += 8000f;
        if (costLike) score -= 15000f; // strong penalty: avoid picking the currency icon

        // Prefer "Simple" images when possible (backgrounds often use Sliced/Tiled)
        if (comp is Image uiImg)
        {
            if (uiImg.type == Image.Type.Simple) score += 1500f;
            else score -= 500f;
        }

        if (score > bestScore)
        {
            bestScore = score;
            bestSprite = sprite;
        }
    }

    // 1) KImage candidates first (often the real item icon for elements/resources)
    try
    {
        var kImageType = AccessTools.TypeByName("KImage") ?? AccessTools.TypeByName("Klei.UI.KImage");
        if (kImageType != null)
        {
            var comps = root.GetComponentsInChildren(kImageType, true);
            for (int i = 0; i < comps.Length; i++)
            {
                var obj = comps[i];
                if (obj == null) continue;

                var comp = obj as Component;
                var sprite = ExtractSpriteFromComponent(obj);
                if (sprite == null) continue;

                bool active = comp != null && comp.gameObject != null && comp.gameObject.activeInHierarchy;

                // enabled best-effort via reflection
                bool enabled = true;
                try
                {
                    var pEnabled = AccessTools.Property(obj.GetType(), "enabled");
                    if (pEnabled != null && pEnabled.PropertyType == typeof(bool))
                        enabled = (bool)pEnabled.GetValue(obj, null);
                }
                catch { }

                ConsiderCandidate(sprite, comp, enabled, active);
            }
        }
    }
    catch { }

    // 2) Unity Image candidates (fallback)
    try
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var img = images[i];
            if (img == null || img.sprite == null) continue;

            bool active = img.gameObject != null && img.gameObject.activeInHierarchy;
            bool enabled = img.enabled;

            ConsiderCandidate(img.sprite, img, enabled, active);
        }
    }
    catch { }

    return bestSprite;
}

        private static Sprite ExtractSpriteFromComponent(object comp)
        {
            if (comp == null) return null;

            try
            {
                var t = comp.GetType();

                var p = AccessTools.Property(t, "sprite") ?? AccessTools.Property(t, "Sprite");
                if (p != null && p.PropertyType == typeof(Sprite))
                    return (Sprite)p.GetValue(comp, null);

                var f = AccessTools.Field(t, "sprite") ?? AccessTools.Field(t, "Sprite");
                if (f != null && f.FieldType == typeof(Sprite))
                    return (Sprite)f.GetValue(comp);
            }
            catch { }

            return null;
        }

        
        private static int TryGetPrintCountSafe(PrinterceptorScreen screen, Tag tag)
        {
            if (screen == null) return 0;

            // 1) Screen-side dictionary (some versions)
            try
            {
                var dict = Traverse.Create(screen).Field<Dictionary<Tag, int>>("printCounts").Value;
                if (dict != null && dict.TryGetValue(tag, out var v))
                    return v;
            }
            catch { }

            // 2) Target object (most common)
            object target = null;
            try { target = Traverse.Create(screen).Field<object>("target").Value; } catch { target = null; }

            int count;
            if (TryReadPrintCountOnObject(target, tag, out count))
                return count;

            // 3) If target is a Unity object, scan components for a printCounts dict
            try
            {
                if (target is GameObject go && go != null)
                {
                    var comps = go.GetComponents<Component>();
                    for (int i = 0; i < comps.Length; i++)
                        if (TryReadPrintCountOnObject(comps[i], tag, out count))
                            return count;
                }
                else if (target is Component c && c != null)
                {
                    var comps = c.GetComponents<Component>();
                    for (int i = 0; i < comps.Length; i++)
                        if (TryReadPrintCountOnObject(comps[i], tag, out count))
                            return count;
                }
            }
            catch { }

            return 0;
        }

        private static bool TryReadPrintCountOnObject(object obj, Tag tag, out int count)
        {
            count = 0;
            if (obj == null) return false;

            try
            {
                var dict = Traverse.Create(obj).Field<Dictionary<Tag, int>>("printCounts").Value;
                if (dict != null && dict.TryGetValue(tag, out var v))
                {
                    count = v;
                    return true;
                }
            }
            catch { }

            return false;
        }

private static void UpdateSelectedCostLabel(PrinterceptorScreen screen, Tag tag)
        {
            try
            {
                var tr = Traverse.Create(screen);
                LocText costLabel = null;
                try { costLabel = tr.Field<LocText>("selectedCostLabel").Value; } catch { }

                if (costLabel == null)
                    return;

                // Reuse the captured localized prefix if available (best for translations).
                string prefix = null;
                try
                {
                    var cache = screen.gameObject != null ? screen.gameObject.GetComponent<PrinterceptorUILabelCache>() : null;
                    if (cache != null && !string.IsNullOrEmpty(cache.RequiredPrefix))
                        prefix = cache.RequiredPrefix;
                }
                catch { }

                if (string.IsNullOrEmpty(prefix))
                    prefix = PrinterceptorUIHelpers.ExtractStablePrefix(costLabel.text, costLabel.text);

                // Determine print count (safe) and compute cost.
                int printCount = TryGetPrintCountSafe(screen, tag);

                int cost = 0;
                try { cost = HijackedHeadquartersConfig.GetDataBankCost(tag, printCount); } catch { }

                if (string.IsNullOrEmpty(prefix))
                    prefix = "Data Banks Required:";

                // For Resources tab, also show the pack quantity under the cost line (kg).
                int packKg = 0;
                bool hasPackKg = PrinterceptorCatalog.IsResource(tag) && PrinterceptorCatalog.TryGetResourcePackKg(tag, out packKg);

                string costText = $"{prefix} {cost}";
                if (hasPackKg && packKg > 0)
                    costText = $"{prefix} {cost}\n<size=80%>▣ Pack : {packKg} kg</size>";


GravitasUIUtil.SetLocText(costLabel, costText);
PrinterceptorUIHelpers.ConfigureCounterLabel(costLabel);

// Counter labels are forced to 1 line by default; allow 2 lines for the resources quantity.
if (hasPackKg && packKg > 0)
    PrinterceptorUIHelpers.ConfigureMultiLineLabel(costLabel, 2);
}
            catch { }
        }


        private static void UpdateSelectedPrintAmountLabel(PrinterceptorScreen screen, Tag tag)
        {
            try
            {
                if (screen == null || screen.gameObject == null)
                    return;

                if (!PrinterceptorCatalog.IsResource(tag))
                    return;

                int packKg;
                if (!PrinterceptorCatalog.TryGetResourcePackKg(tag, out packKg) || packKg <= 0)
                    return;

                // Display as kg (packs are large enough that kg is the clearest unit).
                string massText = $"{packKg} kg";

                // Avoid touching our cost label (we already inject the pack line there).
                LocText costLabel = null;
                try { costLabel = Traverse.Create(screen).Field<LocText>("selectedCostLabel").Value; } catch { }

                var locTexts = screen.gameObject.GetComponentsInChildren<LocText>(true);
                if (locTexts == null || locTexts.Length == 0)
                    return;

                for (int i = 0; i < locTexts.Length; i++)
                {
                    var lt = locTexts[i];
                    if (lt == null || lt == costLabel || lt.gameObject == null)
                        continue;

                    string current = lt.text ?? string.Empty;
                    if (string.IsNullOrEmpty(current))
                        continue;

                    if (!LooksLikeMassText(current))
                        continue;

                    string goName = lt.gameObject.name ?? string.Empty;
                    string parentName = string.Empty;
                    try { parentName = lt.transform != null && lt.transform.parent != null ? lt.transform.parent.name : string.Empty; } catch { }

                    bool looksRelatedToPrint =
                        ContainsPrintWord(current) ||
                        NameLooksPrint(goName) ||
                        NameLooksPrint(parentName);

                    if (!looksRelatedToPrint)
                        continue;

                    string prefix = PrefixBeforeFirstNumber(current);
                    string newText = string.IsNullOrEmpty(prefix) ? massText : $"{prefix} {massText}";

                    GravitasUIUtil.SetLocText(lt, newText);
                    PrinterceptorUIHelpers.ConfigureCounterLabel(lt);
                }
            }
            catch { }
        }

        private static bool ContainsPrintWord(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            try
            {
                return s.IndexOf("print", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.IndexOf("imprimer", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return false;
        }

        private static bool NameLooksPrint(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                name = name.ToLowerInvariant();
                return name.Contains("print") ||
                       name.Contains("amount") ||
                       name.Contains("qty") ||
                       name.Contains("quantity") ||
                       name.Contains("mass") ||
                       name.Contains("weight");
            }
            catch { }
            return false;
        }

        private static bool LooksLikeMassText(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            // Must contain at least one digit
            int firstDigit = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { firstDigit = i; break; }
            }
            if (firstDigit < 0) return false;

            try
            {
                // Prefer explicit kg
                if (s.IndexOf("kg", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                // Or a grams suffix like "1000g" / "1 000 g"
                var trimmed = s.Trim();
                if (trimmed.EndsWith("g", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Common pattern: digit + optional spaces + 'g'
                return s.IndexOf(" g", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }

            return false;
        }

        private static string PrefixBeforeFirstNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            try
            {
                int idx = -1;
                for (int i = 0; i < s.Length; i++)
                {
                    if (char.IsDigit(s[i])) { idx = i; break; }
                }

                if (idx <= 0)
                    return string.Empty;

                var prefix = s.Substring(0, idx).TrimEnd();
                // Trim common separators at end.
                prefix = prefix.TrimEnd(':', '-', '–', '—');

                return prefix.TrimEnd();
            }
            catch { }

            return string.Empty;
        }

        private static void TryEnablePrintButton(PrinterceptorScreen screen)
        {
            try
            {
                var tr = Traverse.Create(screen);
                KButton btn = null;
                try { btn = tr.Field<KButton>("printButton").Value; } catch { }

                if (btn == null)
                    return;

                // Try the most common API patterns without assuming a specific ONI version.
                try
                {
                    var m = AccessTools.Method(btn.GetType(), "SetInteractable", new[] { typeof(bool) });
                    if (m != null)
                    {
                        m.Invoke(btn, new object[] { true });
                        return;
                    }
                }
                catch { }

                try
                {
                    var p = AccessTools.Property(btn.GetType(), "isInteractable");
                    if (p != null && p.CanWrite)
                    {
                        p.SetValue(btn, true, null);
                        return;
                    }
                }
                catch { }

                try
                {
                    var p = AccessTools.Property(btn.GetType(), "interactable");
                    if (p != null && p.CanWrite)
                    {
                        p.SetValue(btn, true, null);
                        return;
                    }
                }
                catch { }
            }
            catch { }
        }
    }

// Custom print consumption for resources/nuisibles/moo
    // Fixes: HarmonyException due to wrong prefix signature (no extra spawnPos param!)
    // =========================================================
    [HarmonyPatch(typeof(HijackedHeadquarters.Instance), "PrintSelectedEntity")]
    public static class Printerceptor_CustomPrint_Patch
    {
        // UTIL ONLY (not a Harmony patch!)
        private static void SpawnGassyMooAsComet(Vector3 spawnPos)
        {
            // Just spawn the Moo (no reliance on hypothetical comet prefabs)
            try
            {
                var mooPrefab = Assets.GetPrefab(new Tag(PrinterceptorCatalog.GASSY_MOO_ID));
                if (mooPrefab == null)
                {
                    Debug.LogWarning($"[GravitasMod] Moo prefab introuvable: {PrinterceptorCatalog.GASSY_MOO_ID}");
                    return;
                }

                // small delay feels nicer visually
                GameScheduler.Instance.Schedule("Gravitas_SpawnMoo", 0.25f, _ =>
                {
                    try
                    {
                        var moo = Util.KInstantiate(mooPrefab, spawnPos + new Vector3(0f, 1f, 0f));
                        moo.SetActive(true);
                    }
                    catch { }
                });
            }
            catch { }
        }

        [HarmonyPrefix]
        public static bool Prefix(HijackedHeadquarters.Instance __instance)
        {
            try
            {
                var screen = PrinterceptorScreenInstance.TryGet();
                if (screen == null) return true;

                Tag selected = PrinterceptorScreenInstance.GetSelectedTag(screen);
                if (!PrinterceptorCatalog.IsCustomPrint(selected))
                    return true; // vanilla for everything else

                Storage storage = FindStorage(__instance);
                if (storage == null)
                {
                    Debug.LogWarning("[GravitasMod] Storage introuvable: fallback vanilla.");
                    return true;
                }

                // print count (optional)
                Dictionary<Tag, int> printCounts = null;
                try { printCounts = Traverse.Create(__instance).Field<Dictionary<Tag, int>>("printCounts").Value; } catch { printCounts = null; }

                int printCount = 0;
                if (printCounts != null) printCounts.TryGetValue(selected, out printCount);

                int cost = HijackedHeadquartersConfig.GetDataBankCost(selected, printCount);

                Tag currencyTag = new Tag(PrinterceptorCatalog.CURRENCY_PREFAB_ID);
                int available = PrinterceptorStorageUtil.CountStoredByPrefabId(storage, currencyTag);

                if (available < cost)
                {
                    Debug.Log($"[GravitasMod] Pas assez de {PrinterceptorCatalog.CURRENCY_PREFAB_ID} ({available}/{cost})");
                    return false; // cancel print
                }

                if (!PrinterceptorStorageUtil.ConsumeStoredByPrefabId(storage, currencyTag, cost))
                {
                    Debug.LogWarning("[GravitasMod] Consommation devise impossible, print annulé.");
                    return false;
                }

                Vector3 spawnPos = GetPrinterSpawnPos(screen);

                if (PrinterceptorCatalog.IsGassyMoo(selected))
                {
                    SpawnGassyMooAsComet(spawnPos);
                }
                else
                {
                    GameObject spawned = null;

                    // Resources: spawn element debris with the correct pack mass (kg).
                    // This is more robust than instantiating the prefab then trying to overwrite its default mass.
                    if (PrinterceptorCatalog.IsResource(selected))
                    {
                        int packKg = 0;
                        if (PrinterceptorCatalog.TryGetResourcePackKg(selected, out packKg) && packKg > 0)
                        {
                            float tempK = TryGetInstanceTemperatureK(__instance);
                            // First try SimUtil.SpawnElement(...) (sim-level) for truly correct mass.
                            spawned = TrySpawnSimElementFromTag(selected, spawnPos, (float)packKg, tempK);

                            // Fallback: ElementChunk.Create(...) (robust across builds)
                            if (spawned == null)
                                spawned = TrySpawnElementChunkCreateFromTag(selected, spawnPos, (float)packKg, tempK);

                            // Fallback: Substance.SpawnResource(...) (may vary by version)
                            if (spawned == null)
                                spawned = TrySpawnElementDebrisFromTag(selected, spawnPos, (float)packKg, tempK);

                            // Best-effort: ensure active.
                            try { if (spawned != null) spawned.SetActive(true); } catch { }

                            // Robust fix: the Printerceptor can re-spawn/normalize element debris after the initial spawn.
                            // Re-apply the intended pack mass on the nearest freshly printed debris (usually created at 1kg / 1000g by default).
                            try { ScheduleFixPrintedResourceMass(selected, spawnPos, packKg); } catch { }
                        }
                    }

                    // Fallback: vanilla-style instantiate
                    if (spawned == null)
                    {
                        GameObject prefab = null;
                        try { prefab = Assets.GetPrefab(selected); } catch { prefab = null; }

                        if (prefab == null)
                        {
                            Debug.LogWarning($"[GravitasMod] Prefab null au print: {selected}");
                            return false;
                        }

                        spawned = Util.KInstantiate(prefab, spawnPos);
                        spawned.SetActive(true);
                        ApplyResourcePackMassIfNeeded(selected, spawned);
                    }
                }
if (printCounts != null)
                {
                    int pc = 0;
                    printCounts.TryGetValue(selected, out pc);
                    printCounts[selected] = pc + 1;
                }

                try { Traverse.Create(__instance).Method("UpdateMeter").GetValue(); } catch { }
                try { Traverse.Create(__instance).Method("UpdateStatusItems").GetValue(); } catch { }

                return false; // we handled it
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] CustomPrint error: {e}");
                return true; // fail open
            }
        }

        

        private static void ApplyResourcePackMassIfNeeded(Tag selected, GameObject spawned)
        {
            try
            {
                if (spawned == null)
                    return;

                if (!PrinterceptorCatalog.IsResource(selected))
                    return;

                int kg;
                if (!PrinterceptorCatalog.TryGetResourcePackKg(selected, out kg) || kg <= 0)
                    return;

                ApplyResourcePackMassNow(spawned, kg);

                // Some prefabs re-apply a default mass on spawn/enable; re-apply next tick (one-shot).
                try
                {
                    GameScheduler.Instance.Schedule("Gravitas_ReapplyPackMass", 0f, _ =>
                    {
                        try
                        {
                            if (spawned != null)
                                ApplyResourcePackMassNow(spawned, kg);
                        }
                        catch { }
                    });
                    // Reapply again a bit later (ElementChunk can overwrite mass after initial spawn)
                    GameScheduler.Instance.Schedule("Gravitas_ReapplyPackMass2", 0.10f, _ =>
                    {
                        try
                        {
                            if (spawned != null)
                                ApplyResourcePackMassNow(spawned, kg);
                        }
                        catch { }
                    });
                    GameScheduler.Instance.Schedule("Gravitas_ReapplyPackMass3", 0.30f, _ =>
                    {
                        try
                        {
                            if (spawned != null)
                                ApplyResourcePackMassNow(spawned, kg);
                        }
                        catch { }
                    });

                }
                catch { }
            }
            catch { }
        }

        // =========================================================
        // Robust post-spawn mass fix for Resources
        // Some Printerceptor spawn paths normalize element debris to 1kg (1000g) after creation.
        // We re-locate the freshly printed debris near the spawn position and re-apply the intended pack mass.
        // =========================================================
        private static void ScheduleFixPrintedResourceMass(Tag selected, Vector3 around, int packKg)
        {
            if (packKg <= 0)
                return;

            try
            {
                // Multiple passes: different spawn paths finalize the printed object at different times.
                ScheduleFixPrintedResourceMassAttempt(selected, around, packKg, 0.2f);
                ScheduleFixPrintedResourceMassAttempt(selected, around, packKg, 0.6f);
                ScheduleFixPrintedResourceMassAttempt(selected, around, packKg, 1.2f);
                ScheduleFixPrintedResourceMassAttempt(selected, around, packKg, 2.0f);
            }
            catch { }
        }

        private static void ScheduleFixPrintedResourceMassAttempt(Tag selected, Vector3 around, int packKg, float delay)
        {
            try
            {
                GameScheduler.Instance.Schedule("Gravitas_FixPrintedResourceMass", delay, _ =>
                {
                    try
                    {
                        var go = FindNearestPrintedPickupable(selected, around, 8f);
                        if (go == null) return;

                        // Only override objects that still look like the default 1kg debris.
                        var pe = go.GetComponent<PrimaryElement>() ?? go.GetComponentInChildren<PrimaryElement>(true);
                        if (pe != null)
                        {
                            float curMass = 0f;
                            try { curMass = pe.Mass; } catch { curMass = 0f; }

                            // Debris created by the Printerceptor defaults to ~1kg; we only force the pack mass in that case.
                            if (curMass > 0f && curMass <= 2.5f)
                            {
                                ApplyResourcePackMassNow(go, packKg);
                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static GameObject FindNearestPrintedPickupable(Tag selected, Vector3 around, float maxDist)
        {
            try
            {
                float bestDist = float.MaxValue;
                GameObject best = null;

                // NOTE: FindObjectsOfType is heavier than Components lists, but this runs only on print (rare),
                // and we keep the search radius tight.
                var pickupables = UnityEngine.Object.FindObjectsOfType<Pickupable>();
                if (pickupables == null || pickupables.Length == 0)
                    return null;

                for (int i = 0; i < pickupables.Length; i++)
                {
                    var p = pickupables[i];
                    if (p == null || p.gameObject == null)
                        continue;

                    var go = p.gameObject;

                    // Must match the selected Tag.
                    try
                    {
                        var kpid = go.GetComponent<KPrefabID>();
                        if (kpid == null) continue;
                        if (!kpid.HasTag(selected)) continue;
                    }
                    catch
                    {
                        continue;
                    }

                    float d = 0f;
                    try { d = Vector3.Distance(go.transform.position, around); }
                    catch { d = float.MaxValue; }

                    if (d > maxDist)
                        continue;

                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = go;
                    }
                }

                return best;
            }
            catch { }

            return null;
        }



        // =========================================================
        // Robust resource spawning (element debris) with correct mass
        // =========================================================
        private static float TryGetInstanceTemperatureK(HijackedHeadquarters.Instance inst)
        {
            try
            {
                if (inst != null)
                {
                    var pe = inst.GetComponent<PrimaryElement>();
                    if (pe != null)
                        return pe.Temperature;
                }
            }
            catch { }
            return 293.15f; // ~20°C
        }

        // =========================================================
        // Element resolution helpers (robust across ONI builds)
        // =========================================================
        private static bool TryParseSimHashes(string id, out Type simHashesType, out object simHashObj, out int simHashInt)
        {
            simHashesType = null;
            simHashObj = null;
            simHashInt = 0;

            try
            {
                simHashesType = AccessTools.TypeByName("SimHashes");
                if (simHashesType == null || !simHashesType.IsEnum)
                    return false;

                simHashObj = Enum.Parse(simHashesType, id, true);
                simHashInt = Convert.ToInt32(simHashObj);
                return true;
            }
            catch
            {
                simHashesType = null;
                simHashObj = null;
                simHashInt = 0;
                return false;
            }
        }

        private static object FindElementByNameFlexible(string id)
        {
            try
            {
                var t = AccessTools.TypeByName("ElementLoader");
                if (t == null) return null;

                var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null) continue;

                    if (m.Name != "FindElementByName" && m.Name != "FindElementByID" && m.Name != "FindElementById")
                        continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 1) continue;
                    if (ps[0].ParameterType != typeof(string)) continue;

                    try { return m.Invoke(null, new object[] { id }); }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private static object FindElementByHashFlexibleAny(int simHashInt, object simHashObj, Type simHashesType)
        {
            try
            {
                var t = AccessTools.TypeByName("ElementLoader");
                if (t == null) return null;

                var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "FindElementByHash") continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 1) continue;

                    var pt = ps[0].ParameterType;
                    object arg = null;

                    if (pt == typeof(int))
                    {
                        arg = simHashInt;
                    }
                    else if (pt.IsEnum)
                    {
                        if (simHashesType != null && pt == simHashesType && simHashObj != null)
                            arg = simHashObj;
                        else
                            arg = Enum.ToObject(pt, simHashInt);
                    }
                    else
                    {
                        continue;
                    }

                    try { return m.Invoke(null, new object[] { arg }); }
                    catch { }
                }
            }
            catch { }

            return null;
        }


        
                private static GameObject TrySpawnSimElementFromTag(Tag selected, Vector3 spawnPos, float massKg, float temperatureK)
        {
            try
            {
                if (massKg <= 0f)
                    return null;

                string id = PrinterceptorCatalog.SafeTagToString(selected);
                if (string.IsNullOrEmpty(id))
                    return null;

                // IMPORTANT:
                // SimUtil.SpawnElement expects a SimHashes (enum) OR its underlying int value,
                // NOT Hash.SDBMLower(...) hashes. We must parse the element id into SimHashes.
                if (!TryParseSimHashes(id, out var simHashesType, out var simHashObj, out var simHashInt))
                    return null;

                var simUtilType = AccessTools.TypeByName("SimUtil");
                if (simUtilType == null)
                    return null;

                var methods = simUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "SpawnElement")
                        continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length < 3)
                        continue;

                    // First param: either Vector3 position OR int cell
                    bool firstIsPos = ps[0].ParameterType == typeof(Vector3);
                    bool firstIsCell = ps[0].ParameterType == typeof(int);
                    if (!firstIsPos && !firstIsCell)
                        continue;

                    // Second param: element identifier (SimHashes enum OR int)
                    var p1t = ps[1].ParameterType;
                    object elemArg = null;
                    if (p1t == typeof(int))
                        elemArg = simHashInt;
                    else if (p1t.IsEnum)
                        elemArg = Enum.ToObject(p1t, simHashInt);
                    else
                        continue;

                    var args = new object[ps.Length];

                    // Position/cell
                    args[0] = firstIsPos ? (object)spawnPos : Grid.PosToCell(spawnPos);
                    args[1] = elemArg;

                    // Fill remaining args using parameter names where possible (more stable than ordering).
                    bool setMass = false;
                    bool setTemp = false;
                    bool setDiseaseIdx = false;
                    bool setDiseaseCount = false;

                    for (int a = 2; a < ps.Length; a++)
                    {
                        var pt = ps[a].ParameterType;
                        string pn = ps[a].Name ?? string.Empty;

                        if (pt == typeof(float))
                        {
                            if (!setMass && (pn.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             pn.IndexOf("units", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = massKg;
                                setMass = true;
                                continue;
                            }
                            if (!setTemp && (pn.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             pn.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = temperatureK;
                                setTemp = true;
                                continue;
                            }

                            // Fallback by order: mass first, then temperature.
                            if (!setMass)
                            {
                                args[a] = massKg;
                                setMass = true;
                            }
                            else if (!setTemp)
                            {
                                args[a] = temperatureK;
                                setTemp = true;
                            }
                            else
                            {
                                args[a] = 0f;
                            }

                            continue;
                        }

                        if (pt == typeof(byte))
                        {
                            if (!setDiseaseIdx && pn.IndexOf("disease", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // byte.MaxValue is commonly used for "no disease"
                                args[a] = byte.MaxValue;
                                setDiseaseIdx = true;
                            }
                            else
                            {
                                args[a] = (byte)0;
                            }
                            continue;
                        }

                        if (pt == typeof(int))
                        {
                            if (!setDiseaseCount && pn.IndexOf("disease", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                args[a] = 0;
                                setDiseaseCount = true;
                            }
                            else
                            {
                                args[a] = 0;
                            }
                            continue;
                        }

                        if (pt == typeof(bool))
                        {
                            args[a] = true;
                            continue;
                        }

                        if (pt.IsEnum)
                        {
                            if (pn.IndexOf("element", StringComparison.OrdinalIgnoreCase) >= 0)
                                args[a] = Enum.ToObject(pt, simHashInt);
                            else
                                args[a] = Enum.ToObject(pt, 0);
                            continue;
                        }

                        if (pt.IsValueType)
                        {
                            try { args[a] = Activator.CreateInstance(pt); }
                            catch { args[a] = null; }
                            continue;
                        }

                        // reference types
                        args[a] = null;
                    }

                    object result = null;
                    try
                    {
                        result = m.Invoke(null, args);
                    }
                    catch
                    {
                        // try next overload
                        continue;
                    }

                    var go = CoerceToGameObject(result);
                    if (go != null)
                    {
                        // Ensure correct mass sticks
                        try { ApplyResourcePackMassNow(go, (int)Mathf.Round(massKg)); } catch { }
                        return go;
                    }

                    // Some overloads return void (spawn happens internally). Best-effort locate the new debris.
                    if (m.ReturnType == typeof(void))
                    {
                        var found = FindNearestPrintedDebrisByElementId(simHashInt, spawnPos, 6f, massKg);
                        if (found != null)
                        {
                            try { ApplyResourcePackMassNow(found, (int)Mathf.Round(massKg)); } catch { }
                            return found;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        // =========================================================
        // Even more robust: call ElementChunk.Create(...) directly (different builds expose different overloads)
        // =========================================================
        private static GameObject TrySpawnElementChunkCreateFromTag(Tag selected, Vector3 spawnPos, float massKg, float temperatureK)
        {
            try
            {
                if (massKg <= 0f)
                    return null;

                string id = PrinterceptorCatalog.SafeTagToString(selected);
                if (string.IsNullOrEmpty(id))
                    return null;

                if (!TryParseSimHashes(id, out var simHashesType, out var simHashObj, out var simHashInt))
                    return null;

                var chunkType = AccessTools.TypeByName("ElementChunk");
                if (chunkType == null)
                    return null;

                var methods = chunkType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null) continue;

                    // We accept Create / CreatePickupable / CreateDefault and similar
                    if (m.Name.IndexOf("Create", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length < 2)
                        continue;

                    var args = new object[ps.Length];
                    bool hasElement = false;
                    bool hasMass = false;

                    for (int a = 0; a < ps.Length; a++)
                    {
                        var pt = ps[a].ParameterType;
                        string pn = ps[a].Name ?? string.Empty;

                        if (pt == typeof(Vector3))
                        {
                            args[a] = spawnPos;
                            continue;
                        }

                        if (pt == typeof(int))
                        {
                            // cell or disease count
                            if (pn.IndexOf("cell", StringComparison.OrdinalIgnoreCase) >= 0)
                                args[a] = Grid.PosToCell(spawnPos);
                            else if (pn.IndexOf("disease", StringComparison.OrdinalIgnoreCase) >= 0)
                                args[a] = 0;
                            else
                                args[a] = 0;
                            continue;
                        }

                        if (pt == typeof(byte))
                        {
                            args[a] = byte.MaxValue; // no disease
                            continue;
                        }

                        if (pt == typeof(float))
                        {
                            if (!hasMass && (pn.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             pn.IndexOf("units", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = massKg;
                                hasMass = true;
                                continue;
                            }
                            if (pn.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                pn.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                args[a] = temperatureK;
                                continue;
                            }

                            // Fallback: first float after element is mass
                            if (!hasMass)
                            {
                                args[a] = massKg;
                                hasMass = true;
                            }
                            else
                            {
                                args[a] = 0f;
                            }
                            continue;
                        }

                        if (pt.IsEnum)
                        {
                            // element identifier
                            if (!hasElement && (pn.IndexOf("element", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                pt.Name.IndexOf("SimHashes", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = Enum.ToObject(pt, simHashInt);
                                hasElement = true;
                            }
                            else
                            {
                                args[a] = Enum.ToObject(pt, 0);
                            }
                            continue;
                        }

                        if (pt == typeof(Tag))
                        {
                            args[a] = selected;
                            continue;
                        }

                        if (pt == typeof(bool))
                        {
                            args[a] = true;
                            continue;
                        }

                        if (pt.IsValueType)
                        {
                            try { args[a] = Activator.CreateInstance(pt); }
                            catch { args[a] = null; }
                            continue;
                        }

                        args[a] = null;
                    }

                    // Must have at least element + mass to be meaningful
                    if (!hasMass || !hasElement)
                        continue;

                    object result = null;
                    try { result = m.Invoke(null, args); }
                    catch { continue; }

                    var go = CoerceToGameObject(result);
                    if (go != null)
                    {
                        try { go.SetActive(true); } catch { }
                        try { ApplyResourcePackMassNow(go, (int)Mathf.Round(massKg)); } catch { }
                        return go;
                    }

                    if (m.ReturnType == typeof(void))
                    {
                        var found = FindNearestPrintedDebrisByElementId(simHashInt, spawnPos, 6f, massKg);
                        if (found != null)
                        {
                            try { ApplyResourcePackMassNow(found, (int)Mathf.Round(massKg)); } catch { }
                            return found;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        // =========================================================
        // Helper: locate printed debris by PrimaryElement.ElementID (robust for elements where tags don't match)
        // =========================================================
        private static GameObject FindNearestPrintedDebrisByElementId(int elementIdInt, Vector3 around, float maxDist, float expectedKg)
        {
            try
            {
                float bestDist = float.MaxValue;
                GameObject best = null;

                var pickupables = UnityEngine.Object.FindObjectsOfType<Pickupable>();
                if (pickupables == null || pickupables.Length == 0)
                    return null;

                for (int i = 0; i < pickupables.Length; i++)
                {
                    var p = pickupables[i];
                    if (p == null || p.gameObject == null)
                        continue;

                    var go = p.gameObject;

                    float d = 0f;
                    try { d = Vector3.Distance(go.transform.position, around); }
                    catch { d = float.MaxValue; }

                    if (d > maxDist)
                        continue;

                    var pe = go.GetComponent<PrimaryElement>() ?? go.GetComponentInChildren<PrimaryElement>(true);
                    if (pe == null)
                        continue;

                    int curId = 0;
                    try { curId = (int)pe.ElementID; } catch { curId = 0; }

                    if (curId != elementIdInt)
                        continue;

                    // Avoid grabbing an existing pile near the output.
                    // We accept either a freshly spawned default ~1kg chunk, or a chunk whose mass already matches the expected pack.
                    float curMass = 0f;
                    try { curMass = pe.Mass; } catch { curMass = 0f; }

                    bool massOk = true;
                    if (expectedKg > 0f)
                    {
                        float tol = Mathf.Max(0.25f, expectedKg * 0.10f);
                        massOk = (curMass > 0f && (curMass <= 2.5f || Mathf.Abs(curMass - expectedKg) <= tol));
                    }

                    if (!massOk)
                        continue;

                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = go;
                    }
                }

                return best;
            }
            catch { }

            return null;
        }



        private static GameObject CoerceToGameObject(object result)
        {
            try
            {
                if (result == null)
                    return null;

                if (result is GameObject g)
                    return g;

                if (result is Component c)
                    return c.gameObject;

                return null;
            }
            catch { }
            return null;
        }

        private static GameObject TrySpawnElementDebrisFromTag(Tag selected, Vector3 spawnPos, float massKg, float temperatureK)
        {
            try
            {
                if (massKg <= 0f)
                    return null;

                string id = PrinterceptorCatalog.SafeTagToString(selected);
                if (string.IsNullOrEmpty(id))
                    return null;

                // Prefer name-based element resolution (no hashing assumptions).
                object element = FindElementByNameFlexible(id);

                // Fallback: resolve via SimHashes enum value when available.
                if (element == null)
                {
                    Type simHashesType;
                    object simHashObj;
                    int simHashInt;
                    if (TryParseSimHashes(id, out simHashesType, out simHashObj, out simHashInt))
                        element = FindElementByHashFlexibleAny(simHashInt, simHashObj, simHashesType);
                }

                // Older fallback: hash-based lookups (kept for compatibility).
                if (element == null)
                {
                    int elementHash = 0;
                    if (!TryGetSdbmLowerHash(id, out elementHash))
                    {
                        if (!TryGetTagInternalHash(selected, out elementHash))
                            return null;
                    }

                    element = FindElementByHashFlexible(elementHash);
                }

                if (element == null)
                    return null;

                // Element.substance
                object substance = null;
                try
                {
                    var et = element.GetType();
                    var f = AccessTools.Field(et, "substance");
                    if (f != null) substance = f.GetValue(element);
                    if (substance == null)
                    {
                        var p = AccessTools.Property(et, "substance");
                        if (p != null) substance = p.GetValue(element, null);
                    }
                }
                catch { substance = null; }

                if (substance == null)
                    return null;

                // Substance.SpawnResource(...): resolve arguments by parameter names to avoid signature drift.
                object spawnedObj = InvokeSpawnResourceFlexible(substance, spawnPos, massKg, temperatureK);
                var go = CoerceToGameObject(spawnedObj);
                if (go != null)
                {
                    try { ApplyResourcePackMassNow(go, (int)Mathf.Round(massKg)); } catch { }
                    return go;
                }

                return null;
            }
            catch { }
            return null;
        }


        private static bool TryGetSdbmLowerHash(string id, out int hash)
        {
            hash = 0;
            try
            {
                var hashType = AccessTools.TypeByName("Hash");
                if (hashType == null) return false;

                var methods = hashType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "SDBMLower") continue;
                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 1 || ps[0].ParameterType != typeof(string)) continue;

                    object r = m.Invoke(null, new object[] { id });
                    if (r is int iv)
                    {
                        hash = iv;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetTagInternalHash(Tag tag, out int hash)
        {
            hash = 0;
            try
            {
                var tt = tag.GetType();
                var f = AccessTools.Field(tt, "hash") ?? AccessTools.Field(tt, "Hash");
                if (f != null && f.FieldType == typeof(int))
                {
                    hash = (int)f.GetValue(tag);
                    return true;
                }

                // Some Tag implementations store an Id and compute hash lazily; fallback to GetHashCode.
                hash = tag.GetHashCode();
                return hash != 0;
            }
            catch { }
            return false;
        }

        private static object FindElementByHashFlexible(int elementHash)
        {
            try
            {
                var t = AccessTools.TypeByName("ElementLoader");
                if (t == null) return null;

                var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "FindElementByHash") continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 1) continue;

                    var pt = ps[0].ParameterType;
                    object arg = null;

                    if (pt == typeof(int))
                        arg = elementHash;
                    else if (pt.IsEnum)
                        arg = Enum.ToObject(pt, elementHash);
                    else
                        continue;

                    try
                    {
                        return m.Invoke(null, new object[] { arg });
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

                private static object InvokeSpawnResourceFlexible(object substance, Vector3 pos, float massKg, float tempK)
        {
            if (substance == null) return null;

            try
            {
                var t = substance.GetType();
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "SpawnResource") continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length < 2) continue;

                    // First parameter should always be position.
                    if (ps[0].ParameterType != typeof(Vector3)) continue;

                    var args = new object[ps.Length];
                    args[0] = pos;

                    bool setMass = false;
                    bool setTemp = false;

                    for (int a = 1; a < ps.Length; a++)
                    {
                        var pt = ps[a].ParameterType;
                        string pn = ps[a].Name ?? string.Empty;

                        if (pt == typeof(float))
                        {
                            if (!setMass && (pn.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0 || pn.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = massKg;
                                setMass = true;
                                continue;
                            }

                            if (!setTemp && (pn.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0 || pn.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                args[a] = tempK;
                                setTemp = true;
                                continue;
                            }

                            // Fallback by order: mass first, then temperature.
                            if (!setMass)
                            {
                                args[a] = massKg;
                                setMass = true;
                            }
                            else if (!setTemp)
                            {
                                args[a] = tempK;
                                setTemp = true;
                            }
                            else
                            {
                                args[a] = 0f;
                            }

                            continue;
                        }

                        if (pt == typeof(int))
                        {
                            args[a] = 0;
                            continue;
                        }

                        if (pt == typeof(byte))
                        {
                            args[a] = (byte)0;
                            continue;
                        }

                        if (pt == typeof(bool))
                        {
                            args[a] = true;
                            continue;
                        }

                        if (pt.IsEnum)
                        {
                            args[a] = Enum.ToObject(pt, 0);
                            continue;
                        }

                        if (pt.IsValueType)
                        {
                            try { args[a] = Activator.CreateInstance(pt); }
                            catch { args[a] = null; }
                            continue;
                        }

                        args[a] = null;
                    }

                    try
                    {
                        return m.Invoke(substance, args);
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }


        
        private static void ApplyResourcePackMassNow(GameObject spawned, int kg)
        {
            if (spawned == null)
                return;

            float massKg = (float)kg;

            // NOTE:
            // For loose elements/debris, the value used by tooltips & stacking is usually "Units" (kg),
            // not a separate "Mass" field. So we set BOTH Units and Mass fields/properties + call SetMass when available.

            // PrimaryElement (root OR child)
            try
            {
                var pe = spawned.GetComponent<PrimaryElement>() ?? spawned.GetComponentInChildren<PrimaryElement>(true);
                if (pe != null)
                {
                    // Prefer units-based fields/properties first.
                    TrySetFieldOrProperty(pe, "Units", "units", massKg);
                    TrySetFieldOrProperty(pe, "m_units", "mUnits", massKg);
                    TrySetFieldOrProperty(pe, "m_Units", "mUnits", massKg);

                    // Also set mass mirrors (some builds expose Mass only).
                    TrySetFieldOrProperty(pe, "Mass", "mass", massKg);
                    TrySetFieldOrProperty(pe, "m_mass", "mMass", massKg);
                    TrySetFieldOrProperty(pe, "m_Mass", "mMass", massKg);

                    // Finally, invoke SetMass if present.
                    TrySetMassFlexible(pe, massKg);
                }
            }
            catch { }

            // ElementChunk (root OR child) – best effort (some element debris uses ElementChunk internally)
            try
            {
                object chunkComp = null;

                try { chunkComp = spawned.GetComponent("ElementChunk"); } catch { chunkComp = null; }

                if (chunkComp == null)
                {
                    try
                    {
                        var chunkType = AccessTools.TypeByName("ElementChunk");
                        if (chunkType != null)
                            chunkComp = spawned.GetComponentInChildren(chunkType, true);
                    }
                    catch { chunkComp = null; }
                }

                if (chunkComp != null)
                {
                    TrySetFieldOrProperty(chunkComp, "Units", "units", massKg);
                    TrySetFieldOrProperty(chunkComp, "m_units", "mUnits", massKg);
                    TrySetFieldOrProperty(chunkComp, "m_Units", "mUnits", massKg);

                    TrySetFieldOrProperty(chunkComp, "Mass", "mass", massKg);
                    TrySetFieldOrProperty(chunkComp, "m_mass", "mMass", massKg);
                    TrySetFieldOrProperty(chunkComp, "m_Mass", "mMass", massKg);

                    TrySetMassFlexible(chunkComp, massKg);
                }
            }
            catch { }

            // Refresh so tooltips/UI reflect the new mass (best-effort)
            try
            {
                var pickup = spawned.GetComponent<Pickupable>() ?? spawned.GetComponentInChildren<Pickupable>(true);
                if (pickup != null)
                {
                    try { Traverse.Create(pickup).Method("RefreshMass").GetValue(); } catch { }
                    try { Traverse.Create(pickup).Method("UpdateMass").GetValue(); } catch { }
                }
            }
            catch { }
        }


        private static void TrySetFieldOrProperty(object obj, string name1, string name2, float value)
        {
            if (obj == null)
                return;

            try
            {
                var t = obj.GetType();

                try
                {
                    var f = AccessTools.Field(t, name1) ?? AccessTools.Field(t, name2);
                    if (f != null)
                        f.SetValue(obj, value);
                }
                catch { }

                try
                {
                    var p = AccessTools.Property(t, name1) ?? AccessTools.Property(t, name2);
                    if (p != null && p.CanWrite)
                        p.SetValue(obj, value, null);
                }
                catch { }
            }
            catch { }
        }

        private static void TrySetMassFlexible(object obj, float mass)
        {
            if (obj == null)
                return;

            try
            {
                var t = obj.GetType();
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m == null || m.Name != "SetMass")
                        continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length < 1 || ps[0].ParameterType != typeof(float))
                        continue;

                    var args = new object[ps.Length];
                    args[0] = mass;

                    for (int a = 1; a < ps.Length; a++)
                    {
                        var pt = ps[a].ParameterType;
                        if (pt == typeof(bool)) args[a] = true;
                        else if (pt == typeof(int)) args[a] = 0;
                        else if (pt == typeof(float)) args[a] = 0f;
                        else if (pt.IsValueType) args[a] = Activator.CreateInstance(pt);
                        else args[a] = null;
                    }

                    m.Invoke(obj, args);
                    return; // first compatible SetMass wins
                }
            }
            catch { }
        }


private static Storage FindStorage(object instance)
        {
            if (instance == null) return null;

            try
            {
                var f = AccessTools.Field(instance.GetType(), "m_storage");
                if (f != null && typeof(Storage).IsAssignableFrom(f.FieldType))
                    return f.GetValue(instance) as Storage;

                var fields = instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var fi in fields)
                {
                    if (typeof(Storage).IsAssignableFrom(fi.FieldType))
                    {
                        var s = fi.GetValue(instance) as Storage;
                        if (s != null) return s;
                    }
                }
            }
            catch { }

            return null;
        }

        internal static Vector3 GetPrinterSpawnPos(PrinterceptorScreen screen)
        {
            try
            {
                object target = Traverse.Create(screen).Field<object>("target").Value;
                if (target is Component c && c != null)
                    return c.transform.position + new Vector3(0f, 1f, 0f);

                if (target is GameObject go && go != null)
                    return go.transform.position + new Vector3(0f, 1f, 0f);
            }
            catch { }

            return Vector3.zero;
        }
    }

    // =========================================================
    // Mutated seeds: arm during print when tab MutatedSeeds is active
    // Then mutate the freshly spawned seed (Seed OR PlantableSeed) near spawn pos.
    // =========================================================
    
    // =========================================================
    // F) MUTATED SEEDS — state arming + ultra robuste application
    // Objectif:
    // - Quand l'onglet "Graines Mutées" est actif et qu'on imprime une seed,
    //   on force une mutation immédiatement et on garantit l'affichage de l'icône rouge "Mutation"
    //   (seed non-identifiée) jusqu'à l'analyse.
    // =========================================================
    

// =========================================================
// Resource pack spawn override (fixes Printerceptor printing 1000g for element resources)
// We arm this when a Resource option is selected on the PrinterceptorScreen, then intercept
// Util/GameUtil.KInstantiate(...) to set ElementChunk units BEFORE the object becomes active.
// =========================================================
internal static class GravitasResourcePackPrintArm
{
    private static bool _armed;
    private static float _untilRealtime;
    private static Tag _expectedTag;
    private static int _packKg;
    private static int _armedByScreenId;

    public static bool IsActive
    {
        get
        {
            try { return _armed && Time.realtimeSinceStartup <= _untilRealtime; }
            catch { return _armed; }
        }
    }

    public static void Arm(PrinterceptorScreen screen, Tag expectedTag, int packKg)
    {
        try
        {
            if (packKg <= 0) return;
            if (!PrinterceptorCatalog.IsResource(expectedTag)) return;

            _armed = true;
            _expectedTag = expectedTag;
            _packKg = packKg;
            _armedByScreenId = screen != null ? screen.GetInstanceID() : 0;

            // short window is enough: user selects resource then clicks print
            _untilRealtime = Time.realtimeSinceStartup + 6.0f;
        }
        catch
        {
            _armed = true;
            _expectedTag = expectedTag;
            _packKg = packKg;
            _armedByScreenId = 0;
            _untilRealtime = 0f;
        }
    }

    public static bool TryConsumeForPrefab(GameObject originalPrefab, out int packKg)
    {
        packKg = 0;

        if (!IsActive) return false;
        if (originalPrefab == null) return false;
        if (_packKg <= 0) return false;

        try
        {
            var kpid = originalPrefab.GetComponent<KPrefabID>();
            if (kpid == null) return false;

            // Match the selected resource tag (printer prints the same prefab)
            if (!kpid.HasTag(_expectedTag))
                return false;

            packKg = _packKg;

            // One-shot: disarm so we don't affect any unrelated instantiations
            _armed = false;
            _expectedTag = default(Tag);
            _packKg = 0;
            _armedByScreenId = 0;
            _untilRealtime = 0f;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyToInactiveObject(GameObject go, int packKg)
    {
        if (go == null || packKg <= 0)
            return;

        float massKg = packKg;

        try
        {
            // PrimaryElement is the main source of truth for tooltips
            var pe = go.GetComponent<PrimaryElement>() ?? go.GetComponentInChildren<PrimaryElement>(true);
            if (pe != null)
            {
                pe.Mass = massKg;
            }
        }
        catch { }

        try
        {
            // ElementChunk uses Units for stack size/tooltip; set it BEFORE activation so it won't reset to 1kg.
            var chunk = go.GetComponent<ElementChunk>() ?? go.GetComponentInChildren<ElementChunk>(true);
            if (chunk != null)
            {
                // Try common fields/properties without relying on exact API names
                TrySetFieldOrProperty(chunk, "Units", "units", massKg);
                TrySetFieldOrProperty(chunk, "m_units", "mUnits", massKg);
                TrySetFieldOrProperty(chunk, "Mass", "mass", massKg);
                TrySetFieldOrProperty(chunk, "m_mass", "mMass", massKg);
            }
        }
        catch { }
    }

    private static void TrySetFieldOrProperty(object obj, string propName, string fieldName, float value)
    {
        if (obj == null) return;

        try
        {
            var t = obj.GetType();

            var p = AccessTools.Property(t, propName);
            if (p != null && p.CanWrite && (p.PropertyType == typeof(float) || p.PropertyType == typeof(Single)))
            {
                p.SetValue(obj, value, null);
                return;
            }

            var f = AccessTools.Field(t, fieldName);
            if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(Single)))
            {
                f.SetValue(obj, value);
                return;
            }
        }
        catch { }
    }
}

[HarmonyPatch]
internal static class Util_KInstantiate_ResourcePackMass_Patch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type t = typeof(Util);
        var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            var m = methods[i];
            if (m == null) continue;
            if (m.Name != "KInstantiate") continue;
            if (m.ReturnType != typeof(GameObject)) continue;

            var ps = m.GetParameters();
            if (ps == null || ps.Length < 2) continue;
            if (ps[0].ParameterType != typeof(GameObject)) continue;

            bool hasVector3 = false;
            for (int p = 0; p < ps.Length; p++)
                if (ps[p].ParameterType == typeof(Vector3)) { hasVector3 = true; break; }

            if (!hasVector3) continue;

            yield return m;
        }
    }

    private static void Postfix(object[] __args, GameObject __result)
    {
        try
        {
            if (__result == null) return;
            if (__args == null || __args.Length == 0) return;

            var original = __args[0] as GameObject;
            if (original == null) return;

            int kg;
            if (!GravitasResourcePackPrintArm.TryConsumeForPrefab(original, out kg))
                return;

            GravitasResourcePackPrintArm.ApplyToInactiveObject(__result, kg);
        }
        catch { }
    }
}

[HarmonyPatch]
internal static class GameUtil_KInstantiate_ResourcePackMass_Patch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var t = AccessTools.TypeByName("GameUtil");
        if (t == null) yield break;

        var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            var m = methods[i];
            if (m == null) continue;
            if (m.Name != "KInstantiate") continue;
            if (m.ReturnType != typeof(GameObject)) continue;

            var ps = m.GetParameters();
            if (ps == null || ps.Length < 2) continue;
            if (ps[0].ParameterType != typeof(GameObject)) continue;

            bool hasVector3 = false;
            for (int p = 0; p < ps.Length; p++)
                if (ps[p].ParameterType == typeof(Vector3)) { hasVector3 = true; break; }

            if (!hasVector3) continue;

            yield return m;
        }
    }

    private static void Postfix(object[] __args, GameObject __result)
    {
        try
        {
            if (__result == null) return;
            if (__args == null || __args.Length == 0) return;

            var original = __args[0] as GameObject;
            if (original == null) return;

            int kg;
            if (!GravitasResourcePackPrintArm.TryConsumeForPrefab(original, out kg))
                return;

            GravitasResourcePackPrintArm.ApplyToInactiveObject(__result, kg);
        }
        catch { }
    }
}

internal static class MutatedSeedArmState
    {
        public static bool Armed;
        public static float UntilRealtime;
        public static Tag ExpectedSeedTag = default(Tag);
        public static Vector3 SpawnPos;
        public static readonly HashSet<int> Snapshot = new HashSet<int>();

        public static bool IsActive
        {
            get
            {
                try
                {
                    return Armed && Time.realtimeSinceStartup <= UntilRealtime;
                }
                catch
                {
                    return Armed;
                }
            }
        }

        public static void Arm(Tag expected, Vector3 spawnPos, float seconds)
        {
            Armed = true;
            ExpectedSeedTag = expected;
            SpawnPos = spawnPos;
            Snapshot.Clear();
            try { UntilRealtime = Time.realtimeSinceStartup + seconds; }
            catch { UntilRealtime = 0f; }
        }

        public static void Disarm()
        {
            Armed = false;
            ExpectedSeedTag = default(Tag);
            Snapshot.Clear();
        }
    }

    // (1) Arm + PostPrint fallback
    [HarmonyPatch(typeof(HijackedHeadquarters.Instance), "PrintSelectedEntity")]
    public static class Printerceptor_MutatedSeeds_ArmAndPostPrint_Patch
    {
        private const float ARM_SECONDS = 3.0f;
        private const float SEARCH_RADIUS = 14f;

        [HarmonyPrefix]
        public static void Prefix(HijackedHeadquarters.Instance __instance)
        {
            try
            {
                var screen = PrinterceptorScreen.Instance;
                if (screen == null) return;

                var tabs = screen.gameObject.GetComponent<PrinterceptorTabsState>();
                if (tabs == null || tabs.Current != PrinterceptorTab.MutatedSeeds)
                    return;

                Tag selected = default(Tag);
                try { selected = PrinterceptorScreenInstance.GetSelectedTag(screen); } catch { }

                if (!IsSeedTag(selected))
                    return;

                Vector3 spawnPos = Printerceptor_CustomPrint_Patch.GetPrinterSpawnPos(screen);

                MutatedSeedArmState.Arm(selected, spawnPos, ARM_SECONDS);

                // snapshot des seeds existantes (évite de muter une seed déjà là)
                CaptureExistingSeedsNear(selected, spawnPos, SEARCH_RADIUS, MutatedSeedArmState.Snapshot);
            }
            catch { }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!MutatedSeedArmState.IsActive)
                return;

            try
            {
                // Tente immédiat
                var go = FindNewSeedNear(MutatedSeedArmState.ExpectedSeedTag, MutatedSeedArmState.SpawnPos, SEARCH_RADIUS, MutatedSeedArmState.Snapshot);
                if (go != null)
                {
                    PendingMutantSeedUtil.PrepareUnassignedMutantSeed(go);
                    ScheduleEnsure(go);
                    try { MutatedSeedArmState.UntilRealtime = Time.realtimeSinceStartup + 0.75f; } catch { }
                    return;
                }

                // retry léger
                TryScheduleFindAgain(0.15f);
                TryScheduleFindAgain(0.35f);
            }
            catch { }
        }

        private static void TryScheduleFindAgain(float delay)
        {
            try
            {
                GameScheduler.Instance.Schedule("Gravitas_FindMutantSeedAgain", delay, _ =>
                {
                    try
                    {
                        if (!MutatedSeedArmState.IsActive) return;

                        var go = FindNewSeedNear(MutatedSeedArmState.ExpectedSeedTag, MutatedSeedArmState.SpawnPos, SEARCH_RADIUS, MutatedSeedArmState.Snapshot);
                        if (go != null)
                        {
                            PendingMutantSeedUtil.PrepareUnassignedMutantSeed(go);
                            ScheduleEnsure(go);
                            try { MutatedSeedArmState.UntilRealtime = Time.realtimeSinceStartup + 0.75f; } catch { }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static void ScheduleEnsure(GameObject go)
        {
            try
            {
                GameScheduler.Instance.Schedule("Gravitas_EnsureMutantSeed", 0.25f, _ =>
                {
                    try
                    {
                        if (go == null) return;

                        var mp = go.GetComponent<MutantPlant>();
                        if (mp == null)
                        {
                            PendingMutantSeedUtil.PrepareUnassignedMutantSeed(go);
                            return;
                        }

                        var ids = mp.MutationIDs;
                        if (ids == null || ids.Count == 0)
                            PendingMutantSeedUtil.PrepareUnassignedMutantSeed(go);
                    }
                    catch { }
                });
            }
            catch { }
        }

        // ✅ FIX: seed-like (Seed OU PlantableSeed)
        private static bool IsSeedTag(Tag tag)
        {
            try
            {
                GameObject prefab = null;
                try { prefab = Assets.GetPrefab(tag); } catch { }
                return SeedLikeUtil.IsSeedLikePrefab(prefab);
            }
            catch { }
            return false;
        }

        private static void CaptureExistingSeedsNear(Tag tag, Vector3 center, float radius, HashSet<int> outIds)
        {
            outIds.Clear();
            try
            {
                foreach (Pickupable p in Components.Pickupables)
                {
                    if (p == null) continue;
                    var go = p.gameObject;
                    if (go == null) continue;

                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid == null) continue;

                    if (kpid.PrefabTag != tag) continue;

                    // ✅ seed-like instance (inclut PlantableSeed)
                    if (!SeedLikeUtil.IsSeedLikeInstance(go)) continue;

                    float d = Vector3.Distance(go.transform.position, center);
                    if (d <= radius)
                        outIds.Add(go.GetInstanceID());
                }
            }
            catch { }
        }

        private static GameObject FindNewSeedNear(Tag tag, Vector3 center, float radius, HashSet<int> snapshot)
        {
            GameObject best = null;
            float bestDist = float.MaxValue;

            try
            {
                foreach (Pickupable p in Components.Pickupables)
                {
                    if (p == null) continue;
                    var go = p.gameObject;
                    if (go == null) continue;

                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid == null) continue;

                    if (kpid.PrefabTag != tag) continue;

                    // ✅ seed-like instance (inclut PlantableSeed)
                    if (!SeedLikeUtil.IsSeedLikeInstance(go)) continue;

                    int id = go.GetInstanceID();
                    if (snapshot.Contains(id)) continue;

                    float d = Vector3.Distance(go.transform.position, center);
                    if (d > radius) continue;

                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = go;
                    }
                }
            }
            catch { }

            return best;
        }
    }

    // (2) Hook ultra-robuste : dès qu’une PlantableSeed spawn pendant la fenêtre armée => mutate
    [HarmonyPatch]
    internal static class MutatedSeeds_PlantableSeed_OnSpawn_Patch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                var t = AccessTools.TypeByName("PlantableSeed");
                if (t == null) return null;
                return AccessTools.Method(t, "OnSpawn");
            }
            catch { return null; }
        }

        private static void Postfix(object __instance)
        {
            try
            {
                if (!MutatedSeedArmState.IsActive)
                    return;

                var comp = __instance as Component;
                if (comp == null) return;

                var go = comp.gameObject;
                if (go == null) return;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null) return;

                // ✅ seed-like instance (inclut PlantableSeed)
                if (!SeedLikeUtil.IsSeedLikeInstance(go))
                    return;

                if (!kpid.PrefabTag.Equals(MutatedSeedArmState.ExpectedSeedTag))
                    return;

                var mp = go.GetComponent<MutantPlant>();
                if (mp != null)
                {
                    var ids = mp.MutationIDs;
                    if (ids != null && ids.Count > 0)
                        return;
                }

                PendingMutantSeedUtil.PrepareUnassignedMutantSeed(go);

                try { MutatedSeedArmState.UntilRealtime = Time.realtimeSinceStartup + 0.75f; } catch { }
            }
            catch { }
        }
    }

    // =========================================================
    // Marker "mutation en attente / non identifiée"
    // =========================================================
    internal sealed class PendingMutationMarker : MonoBehaviour { }

    // =========================================================
    // ✅ Encapsulation Printerceptor
    // =========================================================
    internal sealed class GravitasPrinterceptorSeedMarker : MonoBehaviour { }

    // Kick visuel: force la mise à jour des status/items quelques frames, puis s’auto-détruit.
    internal sealed class GravitasMutantVisualKick : MonoBehaviour
    {
        private int _frames;

        private void LateUpdate()
        {
            _frames++;

            try
            {
                var mp = GetComponent<MutantPlant>();
                if (mp != null)
                {
                    PendingMutantSeedUtil.ForceAnalyzedFalse_Public(mp);
                    try { mp.ApplyMutations(); } catch { }
                    try { mp.UpdateNameAndTags(); } catch { }
                }

                var sel = GetComponent<KSelectable>();
                if (sel != null)
                {
                    TryInvoke(sel, "UpdateStatusItems");
                    TryInvoke(sel, "RefreshStatusItems");
                    TryInvoke(sel, "UpdateStatusItem");
                }
            }
            catch { }

            if (_frames >= 6)
            {
                try { Destroy(this); } catch { }
            }
        }

        private static void TryInvoke(object instance, string methodName)
        {
            try
            {
                if (instance == null) return;
                var m = AccessTools.Method(instance.GetType(), methodName);
                if (m != null) m.Invoke(instance, null);
            }
            catch { }
        }
    }

    // =========================================================
    // Index espèce -> subspecies (pour tirer directement 1 mutation valide)
    // =========================================================
    internal static class PlantsSubSpeciesCatalogIndexV3
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, List<string>> _speciesToSubs;

        public static List<string> GetSubspeciesForSpecies(Tag speciesTag)
        {
            var key = speciesTag.ToString();
            if (string.IsNullOrEmpty(key)) return null;

            EnsureBuilt();

            lock (_lock)
            {
                if (_speciesToSubs != null && _speciesToSubs.TryGetValue(key, out var list))
                    return list;
            }
            return null;
        }

        private static void EnsureBuilt()
        {
            lock (_lock)
            {
                if (_speciesToSubs != null) return;
                _speciesToSubs = Build();
            }
        }

        private static Dictionary<string, List<string>> Build()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var catType = AccessTools.TypeByName("PlantsSubSpeciesCatalog");
                if (catType == null) return map;

                object instance = GetCatalogInstance(catType);

                Type subInfoType =
                    catType.GetNestedType("SubSpeciesInfo", BindingFlags.Public | BindingFlags.NonPublic) ??
                    catType.GetNestedType("SubspeciesInfo", BindingFlags.Public | BindingFlags.NonPublic);

                if (subInfoType == null)
                    return map;

                var roots = new List<object>();
                AddAllStaticMembers(catType, roots);
                if (instance != null) AddAllInstanceMembers(catType, instance, roots);

                var visited = new HashSet<int>();
                for (int i = 0; i < roots.Count; i++)
                    CollectPairsRecursive(roots[i], subInfoType, map, visited, 0);
            }
            catch { }

            return map;
        }

        private static void CollectPairsRecursive(object obj, Type subInfoType, Dictionary<string, List<string>> map, HashSet<int> visited, int depth)
        {
            if (obj == null || depth > 7) return;

            int key = 0;
            try { key = obj.GetHashCode(); } catch { key = 0; }
            if (key != 0 && visited.Contains(key)) return;
            if (key != 0) visited.Add(key);

            if (subInfoType.IsAssignableFrom(obj.GetType()))
            {
                ExtractPair(obj, map);
                return;
            }

            if (obj is IDictionary dict)
            {
                try { foreach (var v in dict.Values) CollectPairsRecursive(v, subInfoType, map, visited, depth + 1); } catch { }
                return;
            }

            if (obj is IEnumerable en && !(obj is string))
            {
                try { foreach (var it in en) CollectPairsRecursive(it, subInfoType, map, visited, depth + 1); } catch { }
                return;
            }

            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in t.GetFields(BF))
                {
                    object v = null;
                    try { v = f.GetValue(obj); } catch { v = null; }
                    if (v != null) CollectPairsRecursive(v, subInfoType, map, visited, depth + 1);
                }
            }
            catch { }

            try
            {
                foreach (var p in t.GetProperties(BF))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;

                    object v = null;
                    try { v = p.GetValue(obj, null); } catch { v = null; }
                    if (v != null) CollectPairsRecursive(v, subInfoType, map, visited, depth + 1);
                }
            }
            catch { }
        }

        private static void ExtractPair(object subInfo, Dictionary<string, List<string>> map)
        {
            try
            {
                string species = null;
                string subspecies = null;

                var t = subInfo.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var f in t.GetFields(BF))
                {
                    if (f == null) continue;
                    var n = f.Name ?? "";

                    if (species == null && n.IndexOf("species", StringComparison.OrdinalIgnoreCase) >= 0)
                        species = ReadAsString(f.FieldType, () => f.GetValue(subInfo));

                    if (subspecies == null && (n.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("mutation", StringComparison.OrdinalIgnoreCase) >= 0))
                        subspecies = ReadAsString(f.FieldType, () => f.GetValue(subInfo));
                }

                foreach (var p in t.GetProperties(BF))
                {
                    if (p == null || !p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    var n = p.Name ?? "";

                    if (species == null && n.IndexOf("species", StringComparison.OrdinalIgnoreCase) >= 0)
                        species = ReadAsString(p.PropertyType, () => p.GetValue(subInfo, null));

                    if (subspecies == null && (n.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("mutation", StringComparison.OrdinalIgnoreCase) >= 0))
                        subspecies = ReadAsString(p.PropertyType, () => p.GetValue(subInfo, null));
                }

                if (string.IsNullOrEmpty(species) || string.IsNullOrEmpty(subspecies))
                    return;

                if (!map.TryGetValue(species, out var list))
                {
                    list = new List<string>();
                    map[species] = list;
                }

                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i], subspecies, StringComparison.OrdinalIgnoreCase))
                        return;

                list.Add(subspecies);
            }
            catch { }
        }

        private static string ReadAsString(Type type, Func<object> getter)
        {
            try
            {
                var v = getter();
                if (v == null) return null;

                if (type == typeof(Tag))
                    return ((Tag)v).ToString();

                if (type == typeof(string))
                    return v as string;

                return v.ToString();
            }
            catch { }
            return null;
        }

        private static object GetCatalogInstance(Type catType)
        {
            const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                var p = catType.GetProperty("Instance", BF);
                if (p != null && p.CanRead) return p.GetValue(null, null);
            }
            catch { }

            try
            {
                var f = catType.GetField("Instance", BF);
                if (f != null) return f.GetValue(null);
            }
            catch { }

            return null;
        }

        private static void AddAllStaticMembers(Type catType, List<object> roots)
        {
            const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in catType.GetFields(BF))
                {
                    object v = null;
                    try { v = f.GetValue(null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }

            try
            {
                foreach (var p in catType.GetProperties(BF))
                {
                    if (!p.CanRead) continue;
                    object v = null;
                    try { v = p.GetValue(null, null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }
        }

        private static void AddAllInstanceMembers(Type catType, object instance, List<object> roots)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in catType.GetFields(BF))
                {
                    object v = null;
                    try { v = f.GetValue(instance); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }

            try
            {
                foreach (var p in catType.GetProperties(BF))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;

                    object v = null;
                    try { v = p.GetValue(instance, null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }
        }
    }

    // =========================================================
    // ✅ PENDING MUTANT SEED UTIL — amélioré: mutation valide immédiate + UI rouge immédiate
    // =========================================================
    internal static class PendingMutantSeedUtil
    {
        private const int MAX_PROBES = 120;

        internal static void ForceAnalyzedFalse_Public(MutantPlant mp) => ForceAnalyzedFalse(mp);

        public static void PrepareUnassignedMutantSeed(GameObject seedGo)
        {
            if (seedGo == null) return;

            // Encapsulation: n’applique que si (fenêtre armée) OU déjà marqué comme venant du Printerceptor
            bool allowed = false;
            try { allowed = MutatedSeedArmState.IsActive; } catch { allowed = false; }
            if (!allowed && seedGo.GetComponent<GravitasPrinterceptorSeedMarker>() == null)
                return;

            if (seedGo.GetComponent<GravitasPrinterceptorSeedMarker>() == null)
                seedGo.AddComponent<GravitasPrinterceptorSeedMarker>();

            if (seedGo.GetComponent<GravitasMutantVisualKick>() == null)
                seedGo.AddComponent<GravitasMutantVisualKick>();

            // Marker "pending"
            if (seedGo.GetComponent<PendingMutationMarker>() == null)
                seedGo.AddComponent<PendingMutationMarker>();

            // Resolve species (plant derrière la seed)
            Tag speciesTag;
            TryResolveSpeciesTagFromSeed(seedGo, out speciesTag);

            // Choisit 1 subspecies valide pour CETTE espèce (sinon fallback nuke)
            string chosenSubId = PickRandomSubspeciesFor(speciesTag);

            MutantPlant mutant = null;
            bool addedNow = false;

            try { mutant = seedGo.GetComponent<MutantPlant>(); } catch { mutant = null; }

            if (mutant == null)
            {
                try
                {
                    mutant = seedGo.AddComponent<MutantPlant>();
                    addedNow = true;

                    // KMonoBehaviour init d’abord (safe)
                    TryInvokeNoArgs(mutant, "OnPrefabInit");
                }
                catch { mutant = null; }
            }

            if (mutant == null) return;

            // IMPORTANT: set state AVANT OnSpawn (sinon status items "rouges" pas posés tout de suite)
            if (!IsDefaultTag(speciesTag))
                TrySetSpecies(mutant, speciesTag);

            bool ok = false;

            if (!string.IsNullOrEmpty(chosenSubId))
            {
                ok = TryForceOneMutation(mutant, speciesTag, chosenSubId);
            }

            // fallback: soft mutate
            if (!ok)
            {
                try
                {
                    ForceAnalyzedFalse(mutant);
                    mutant.Mutate();
                    mutant.ApplyMutations();
                    mutant.UpdateNameAndTags();
                    ok = HasAnyMutation(mutant);
                }
                catch { ok = false; }
            }

            // fallback: NUKE V2+ pool global
            if (!ok)
                ok = NukeV2_Probe(mutant, speciesTag);

            // maintenant seulement OnSpawn si on vient d’ajouter le component
            if (addedNow)
            {
                TryInvokeNoArgs(mutant, "OnSpawn");
            }

            // final refresh + keep unknown
            ForceAnalyzedFalse(mutant);
            try { mutant.ApplyMutations(); } catch { }
            try { mutant.UpdateNameAndTags(); } catch { }
            ForceAnalyzedFalse(mutant);

            if (!ok)
            {
                try { Debug.LogWarning($"[GravitasMod] MutatedSeed FAILED: seed={seedGo.name}, species={speciesTag}"); }
                catch { }
            }
        }

        private static string PickRandomSubspeciesFor(Tag speciesTag)
        {
            try
            {
                if (IsDefaultTag(speciesTag)) return null;

                var list = PlantsSubSpeciesCatalogIndexV3.GetSubspeciesForSpecies(speciesTag);
                if (list == null || list.Count == 0) return null;

                int idx = UnityEngine.Random.Range(0, list.Count);
                if (idx < 0 || idx >= list.Count) return null;

                return list[idx];
            }
            catch { }
            return null;
        }

        private static bool NukeV2_Probe(MutantPlant mutant, Tag speciesTag)
        {
            if (mutant == null) return false;

            if (IsDefaultTag(speciesTag))
            {
                try { speciesTag = mutant.SpeciesID; } catch { }
            }

            var pool = PlantsSubSpeciesCatalogNukeV2.GetAllKnownSubSpeciesIds();
            if (pool == null || pool.Count == 0)
                return false;

            var candidates = new List<string>(pool);
            ShuffleInPlace(candidates);

            int tries = Mathf.Min(MAX_PROBES, candidates.Count);

            for (int i = 0; i < tries; i++)
            {
                var subId = candidates[i];
                if (string.IsNullOrEmpty(subId)) continue;

                if (TryForceOneMutation(mutant, speciesTag, subId))
                    return true;
            }

            return false;
        }

        private static bool TryForceOneMutation(MutantPlant mutant, Tag speciesTag, string subId)
        {
            try
            {
                if (!IsDefaultTag(speciesTag))
                    TrySetSpecies(mutant, speciesTag);

                ForceAnalyzedFalse(mutant);

                // clear + set mutationIDs field (AVANT apply)
                ForceSetMutationIdsField(mutant, new List<string> { subId });

                // voie “officielle” si dispo
                TryCallListString(mutant, "DummySetSubspecies", new List<string> { subId });
                TryCallListString(mutant, "SetSubSpecies", new List<string> { subId });

                // cachedSubspeciesID (aide UI)
                ForceSetTagField(mutant, "cachedSubspeciesID", MakeTag(subId));

                mutant.ApplyMutations();
                mutant.UpdateNameAndTags();

                if (!HasAnyMutation(mutant)) return false;

                Tag ss = default(Tag);
                try { ss = mutant.SubSpeciesID; } catch { }

                if (!IsDefaultTag(ss))
                    return true;

                try
                {
                    var ids = mutant.MutationIDs;
                    if (ids != null)
                    {
                        for (int i = 0; i < ids.Count; i++)
                            if (string.Equals(ids[i], subId, StringComparison.OrdinalIgnoreCase))
                                return true;
                    }
                }
                catch { }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---- Resolve species (seed -> plant) sans dépendance compile-time ----
        private static void TryResolveSpeciesTagFromSeed(GameObject seedGo, out Tag speciesTag)
        {
            speciesTag = default(Tag);
            if (seedGo == null) return;

            try
            {
                var psType = AccessTools.TypeByName("PlantableSeed");
                if (psType != null)
                {
                    var ps = seedGo.GetComponent(psType);
                    if (ps != null)
                    {
                        string plantId = ReadFirstStringMember(ps, new[] { "plantID", "PlantID", "plantId", "PlantId", "plantPrefabID", "PlantPrefabID" });
                        Tag plantTag = ReadFirstTagMember(ps, new[] { "plantTag", "PlantTag", "plantPrefabTag", "PlantPrefabTag", "plantPrefabIDTag" });

                        if (!string.IsNullOrEmpty(plantId))
                        {
                            var plantPrefab = TryGetPrefabByIdString(plantId);
                            var kpid = (plantPrefab != null) ? plantPrefab.GetComponent<KPrefabID>() : null;
                            if (kpid != null)
                            {
                                speciesTag = kpid.PrefabTag;
                                return;
                            }

                            speciesTag = MakeTag(plantId);
                            return;
                        }

                        if (!IsDefaultTag(plantTag))
                        {
                            var plantPrefab = TryGetPrefabByTag(plantTag);
                            var kpid = (plantPrefab != null) ? plantPrefab.GetComponent<KPrefabID>() : null;
                            if (kpid != null)
                            {
                                speciesTag = kpid.PrefabTag;
                                return;
                            }
                            speciesTag = plantTag;
                            return;
                        }
                    }
                }
            }
            catch { }

            // fallback : seedTag - "Seed"
            try
            {
                var kpidSeed = seedGo.GetComponent<KPrefabID>();
                if (kpidSeed != null)
                {
                    string seedId = kpidSeed.PrefabTag.ToString();
                    string maybePlant = StripSuffixIgnoreCase(seedId, "Seed");
                    if (!string.IsNullOrEmpty(maybePlant) && maybePlant != seedId)
                    {
                        var plantPrefab = TryGetPrefabByIdString(maybePlant);
                        var kpidPlant = (plantPrefab != null) ? plantPrefab.GetComponent<KPrefabID>() : null;
                        if (kpidPlant != null)
                        {
                            speciesTag = kpidPlant.PrefabTag;
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        private static GameObject TryGetPrefabByIdString(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return Assets.GetPrefab(TagManager.Create(id)); } catch { }
            try { return Assets.GetPrefab(new Tag(id)); } catch { }
            return null;
        }

        private static GameObject TryGetPrefabByTag(Tag t)
        {
            try { return Assets.GetPrefab(t); } catch { }
            return null;
        }

        // ---- helpers ----
        private static void TrySetSpecies(MutantPlant mutant, Tag speciesTag)
        {
            try { mutant.SpeciesID = speciesTag; } catch { }
            ForceSetTagField(mutant, "speciesID", speciesTag);
        }

        private static bool HasAnyMutation(MutantPlant mutant)
        {
            try
            {
                var ids = mutant.MutationIDs;
                return ids != null && ids.Count > 0;
            }
            catch { }
            return false;
        }

        private static bool TryCallListString(object instance, string methodName, List<string> args)
        {
            try
            {
                if (instance == null) return false;
                var m = AccessTools.Method(instance.GetType(), methodName, new[] { typeof(List<string>) });
                if (m == null) return false;
                m.Invoke(instance, new object[] { args });
                return true;
            }
            catch { }
            return false;
        }

        private static void ForceSetMutationIdsField(MutantPlant mutant, List<string> ids)
        {
            try
            {
                var f = AccessTools.Field(typeof(MutantPlant), "mutationIDs");
                if (f != null) f.SetValue(mutant, ids);
            }
            catch { }
        }

        private static void ForceSetTagField(object instance, string fieldName, Tag value)
        {
            if (instance == null) return;

            try
            {
                var f = AccessTools.Field(instance.GetType(), fieldName);
                if (f != null && f.FieldType == typeof(Tag))
                {
                    f.SetValue(instance, value);
                    return;
                }
            }
            catch { }

            try
            {
                var f2 = AccessTools.Field(typeof(MutantPlant), fieldName);
                if (f2 != null && f2.FieldType == typeof(Tag))
                {
                    f2.SetValue(instance, value);
                }
            }
            catch { }
        }

        private static void ForceAnalyzedFalse(MutantPlant mutant)
        {
            try
            {
                var analyzedField = AccessTools.Field(typeof(MutantPlant), "analyzed");
                if (analyzedField != null)
                    analyzedField.SetValue(mutant, false);
            }
            catch { }
        }

        private static void TryInvokeNoArgs(object instance, string methodName)
        {
            try
            {
                if (instance == null) return;
                var m = AccessTools.Method(instance.GetType(), methodName);
                if (m != null) m.Invoke(instance, null);
            }
            catch { }
        }

        private static bool IsDefaultTag(Tag t)
        {
            return t.Equals(default(Tag)) || string.IsNullOrEmpty(t.ToString());
        }

        private static Tag MakeTag(string s)
        {
            if (string.IsNullOrEmpty(s)) return default(Tag);
            try { return TagManager.Create(s); } catch { }
            try { return new Tag(s); } catch { }
            return default(Tag);
        }

        private static string StripSuffixIgnoreCase(string s, string suf)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(suf)) return s;
            if (!s.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) return s;
            return s.Substring(0, s.Length - suf.Length);
        }

        private static void ShuffleInPlace(List<string> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static string ReadFirstStringMember(object obj, string[] preferredNames)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var n in preferredNames)
            {
                try
                {
                    var f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(string))
                        return f.GetValue(obj) as string;

                    var p = t.GetProperty(n, BF);
                    if (p != null && p.CanRead && p.PropertyType == typeof(string))
                        return p.GetValue(obj, null) as string;
                }
                catch { }
            }
            return null;
        }

        private static Tag ReadFirstTagMember(object obj, string[] preferredNames)
        {
            if (obj == null) return default(Tag);
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var n in preferredNames)
            {
                try
                {
                    var f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(Tag))
                        return (Tag)f.GetValue(obj);

                    var p = t.GetProperty(n, BF);
                    if (p != null && p.CanRead && p.PropertyType == typeof(Tag))
                        return (Tag)p.GetValue(obj, null);
                }
                catch { }
            }
            return default(Tag);
        }

        internal static bool HasPendingMarker(MutantPlant mp)
        {
            try { return mp != null && mp.gameObject != null && mp.gameObject.GetComponent<PendingMutationMarker>() != null; }
            catch { return false; }
        }

        internal static void ClearPendingMarker(MutantPlant mp)
        {
            if (mp?.gameObject == null) return;
            try
            {
                var marker = mp.gameObject.GetComponent<PendingMutationMarker>();
                if (marker != null)
                    UnityEngine.Object.Destroy(marker);
            }
            catch { }
        }
    }

    // =========================================================
    // NUKE pool builder — hyper robuste (scan récursif)
    // =========================================================
    internal static class PlantsSubSpeciesCatalogNukeV2
    {
        private static List<string> _cached;
        private static readonly object _lock = new object();

        public static List<string> GetAllKnownSubSpeciesIds()
        {
            lock (_lock)
            {
                if (_cached != null && _cached.Count > 0)
                    return _cached;

                _cached = BuildPool();
                return _cached;
            }
        }

        private static List<string> BuildPool()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var catType = AccessTools.TypeByName("PlantsSubSpeciesCatalog");
                if (catType == null) return new List<string>();

                object instance = GetCatalogInstance(catType);

                Type subInfoType = catType.GetNestedType("SubSpeciesInfo", BindingFlags.Public | BindingFlags.NonPublic)
                                ?? catType.GetNestedType("SubspeciesInfo", BindingFlags.Public | BindingFlags.NonPublic);

                var roots = new List<object>();
                AddAllStaticMembers(catType, roots);
                if (instance != null)
                    AddAllInstanceMembers(catType, instance, roots);

                var visited = new HashSet<int>();
                for (int i = 0; i < roots.Count; i++)
                    CollectIdsRecursive(roots[i], subInfoType, set, visited, 0);
            }
            catch { }

            set.RemoveWhere(s =>
                string.IsNullOrEmpty(s) ||
                s.Length < 2 ||
                s.Equals("Invalid", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("None", StringComparison.OrdinalIgnoreCase)
            );

            return new List<string>(set);
        }

        private static object GetCatalogInstance(Type catType)
        {
            const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                var p = catType.GetProperty("Instance", BF);
                if (p != null && p.CanRead) return p.GetValue(null, null);
            }
            catch { }

            try
            {
                var f = catType.GetField("Instance", BF);
                if (f != null) return f.GetValue(null);
            }
            catch { }

            try
            {
                foreach (var f in catType.GetFields(BF))
                {
                    if (f == null) continue;
                    if (!catType.IsAssignableFrom(f.FieldType)) continue;
                    var v = f.GetValue(null);
                    if (v != null) return v;
                }
            }
            catch { }

            return null;
        }

        private static void AddAllStaticMembers(Type catType, List<object> roots)
        {
            const BindingFlags BF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in catType.GetFields(BF))
                {
                    if (f == null) continue;
                    object v = null;
                    try { v = f.GetValue(null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }

            try
            {
                foreach (var p in catType.GetProperties(BF))
                {
                    if (p == null || !p.CanRead) continue;
                    object v = null;
                    try { v = p.GetValue(null, null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }
        }

        private static void AddAllInstanceMembers(Type catType, object instance, List<object> roots)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in catType.GetFields(BF))
                {
                    if (f == null) continue;
                    object v = null;
                    try { v = f.GetValue(instance); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }

            try
            {
                foreach (var p in catType.GetProperties(BF))
                {
                    if (p == null || !p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    object v = null;
                    try { v = p.GetValue(instance, null); } catch { v = null; }
                    if (v != null) roots.Add(v);
                }
            }
            catch { }
        }

        private static void CollectIdsRecursive(object obj, Type subInfoType, HashSet<string> outSet, HashSet<int> visited, int depth)
        {
            if (obj == null) return;
            if (depth > 7) return;

            int key = 0;
            try { key = obj.GetHashCode(); } catch { key = 0; }
            if (key != 0 && visited.Contains(key)) return;
            if (key != 0) visited.Add(key);

            if (subInfoType != null && subInfoType.IsAssignableFrom(obj.GetType()))
            {
                ExtractIdsFromSubInfo(obj, outSet);
                return;
            }

            if (obj is IDictionary dict)
            {
                try
                {
                    foreach (var v in dict.Values)
                        CollectIdsRecursive(v, subInfoType, outSet, visited, depth + 1);
                }
                catch { }
                return;
            }

            if (obj is IEnumerable en && !(obj is string))
            {
                try
                {
                    foreach (var it in en)
                        CollectIdsRecursive(it, subInfoType, outSet, visited, depth + 1);
                }
                catch { }
                return;
            }

            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in t.GetFields(BF))
                {
                    if (f == null) continue;
                    object v = null;
                    try { v = f.GetValue(obj); } catch { v = null; }
                    if (v != null) CollectIdsRecursive(v, subInfoType, outSet, visited, depth + 1);
                }
            }
            catch { }

            try
            {
                foreach (var p in t.GetProperties(BF))
                {
                    if (p == null || !p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    object v = null;
                    try { v = p.GetValue(obj, null); } catch { v = null; }
                    if (v != null) CollectIdsRecursive(v, subInfoType, outSet, visited, depth + 1);
                }
            }
            catch { }
        }

        private static void ExtractIdsFromSubInfo(object subInfo, HashSet<string> outSet)
        {
            if (subInfo == null) return;
            var t = subInfo.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                foreach (var f in t.GetFields(BF))
                {
                    if (f == null) continue;
                    var name = f.Name ?? "";

                    if (name.IndexOf("species", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (f.FieldType == typeof(Tag))
                    {
                        if (!LooksLikeIdField(name)) continue;
                        Tag tag = default(Tag);
                        try { tag = (Tag)f.GetValue(subInfo); } catch { tag = default(Tag); }
                        var s = tag.ToString();
                        if (!string.IsNullOrEmpty(s)) outSet.Add(s);
                    }
                    else if (f.FieldType == typeof(string))
                    {
                        if (!LooksLikeIdField(name)) continue;
                        string s = null;
                        try { s = f.GetValue(subInfo) as string; } catch { s = null; }
                        if (!string.IsNullOrEmpty(s)) outSet.Add(s);
                    }
                }
            }
            catch { }

            try
            {
                foreach (var p in t.GetProperties(BF))
                {
                    if (p == null || !p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;

                    var name = p.Name ?? "";
                    if (name.IndexOf("species", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (p.PropertyType == typeof(Tag))
                    {
                        if (!LooksLikeIdField(name)) continue;
                        Tag tag = default(Tag);
                        try { tag = (Tag)p.GetValue(subInfo, null); } catch { tag = default(Tag); }
                        var s = tag.ToString();
                        if (!string.IsNullOrEmpty(s)) outSet.Add(s);
                    }
                    else if (p.PropertyType == typeof(string))
                    {
                        if (!LooksLikeIdField(name)) continue;
                        string s = null;
                        try { s = p.GetValue(subInfo, null) as string; } catch { s = null; }
                        if (!string.IsNullOrEmpty(s)) outSet.Add(s);
                    }
                }
            }
            catch { }
        }

        private static bool LooksLikeIdField(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("mutation", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // =========================================================
    // Patches MutantPlant : tant que marker => non identifié
    // =========================================================
    [HarmonyPatch(typeof(MutantPlant), "get_IsOriginal")]
    internal static class MutantPlant_IsOriginal_Patch
    {
        internal static void Postfix(MutantPlant __instance, ref bool __result)
        {
            if (PendingMutantSeedUtil.HasPendingMarker(__instance))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(MutantPlant), "get_IsIdentified")]
    internal static class MutantPlant_IsIdentified_Patch
    {
        internal static void Postfix(MutantPlant __instance, ref bool __result)
        {
            if (PendingMutantSeedUtil.HasPendingMarker(__instance))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(MutantPlant), nameof(MutantPlant.Analyze))]
    internal static class MutantPlant_Analyze_Patch
    {
        internal static void Postfix(MutantPlant __instance)
        {
            if (PendingMutantSeedUtil.HasPendingMarker(__instance))
                PendingMutantSeedUtil.ClearPendingMarker(__instance);
        }
    }
internal static class PrinterceptorScreenInstance
    {
        public static PrinterceptorScreen TryGet()
        {
            try
            {
                var prop = AccessTools.Property(typeof(PrinterceptorScreen), "Instance");
                if (prop != null)
                    return prop.GetValue(null, null) as PrinterceptorScreen;
            }
            catch { }

            try { return UnityEngine.Object.FindObjectOfType<PrinterceptorScreen>(); }
            catch { return null; }
        }

        public static Tag GetSelectedTag(PrinterceptorScreen screen)
        {
            if (screen == null) return default(Tag);

            try
            {
                var f = AccessTools.Field(screen.GetType(), "selectedEntityTag");
                if (f != null && f.FieldType == typeof(Tag))
                    return (Tag)f.GetValue(screen);

                var p = AccessTools.Property(screen.GetType(), "selectedEntityTag");
                if (p != null && p.CanRead && p.PropertyType == typeof(Tag))
                    return (Tag)p.GetValue(screen, null);
            }
            catch { }

            return default(Tag);
        }
    }

    // =========================================================
    // Small UI helper: set LocText safely across ONI versions
    // =========================================================
    internal static class GravitasUIUtil
    {
        public static void SetLocText(LocText lt, string s)
        {
            if (lt == null) return;

            try
            {
                var t = lt.GetType();
                var m = t.GetMethod("SetText", new[] { typeof(string) });
                if (m != null) { m.Invoke(lt, new object[] { s }); return; }

                m = t.GetMethod("SetText", new[] { typeof(string), typeof(bool) });
                if (m != null) { m.Invoke(lt, new object[] { s, true }); return; }
            }
            catch { }

            lt.text = s;
        }
    }
}
