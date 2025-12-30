using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KSerialization;
using UnityEngine;
using UnityEngine.UI;
using STRINGS;

namespace GravitasMod
{
    // ============================================================
    // 0) MOD ENTRY
    // ============================================================
    public sealed class GravitasPrinterceptorCurrencyMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            // Prefab/config: autoriser plastic + state + capacity
            PatchIfExists(harmony,
                AccessTools.Method(typeof(HijackedHeadquartersConfig), "DoPostConfigureComplete"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.HHConfig_DoPostConfigureComplete_Postfix)));

            // Storage runtime: re-apply filters + register printerceptor currency storages + capacity
            PatchIfExists(harmony,
                AccessTools.Method(typeof(HijackedHeadquarters.Instance), "ApplyMaxCapacity"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.HHInstance_ApplyMaxCapacity_Postfix)));

            // IMPORTANT: avant ApplyMaxCapacity vanilla -> sync userMaxCapacity backing field selon la monnaie
            PatchIfExists(harmony,
                AccessTools.Method(typeof(HijackedHeadquarters.Instance), "ApplyMaxCapacity"),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.HHInstance_ApplyMaxCapacity_Prefix)));

            // IUserControlledCapacity (UI "Capacité du stockage automatisisé") -> currency-aware
            PatchIfExists(harmony,
                FindIuccMethod("get_AmountStored", typeof(float)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.IUCC_get_AmountStored_Prefix)));

            PatchIfExists(harmony,
                FindIuccMethod("get_UserMaxCapacity", typeof(float)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.IUCC_get_UserMaxCapacity_Prefix)));

            PatchIfExists(harmony,
                FindIuccMethod("set_UserMaxCapacity", typeof(void), typeof(float)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.IUCC_set_UserMaxCapacity_Postfix)));

            PatchIfExists(harmony,
                FindIuccMethod("get_MaxCapacity", typeof(float)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.IUCC_get_MaxCapacity_Prefix)));

            PatchIfExists(harmony,
                FindIuccMethod("get_CapacityUnits", typeof(LocString)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.IUCC_get_CapacityUnits_Postfix)));

            // Storage validity: allow plastic ONLY for Printerceptor currency storage
            PatchIfExists(harmony,
                AccessTools.Method(typeof(Storage), "IsValidForStore", new[] { typeof(GameObject), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.Storage_IsValidForStore_GO_Prefix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(Storage), "IsValidForStore", new[] { typeof(Tag) }),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.Storage_IsValidForStore_Tag_Prefix)));

            // ============================================================
            // ✅ ROBUST PAYMENT FIX (PRINT):
            // Le jeu peut continuer à payer en DataBank en interne.
            // On reroute automatiquement le Tag demandé au niveau Storage (Printerceptor uniquement),
            // selon la monnaie active (DataBank <-> Plastic).
            // ============================================================
            foreach (var m in FindStorageTagFirstMethods(
                "GetAmountAvailable",
                "GetAmountStored",
                "Consume",
                "TryConsume",
                "ConsumeIgnoringDisease",
                "ConsumeIgnoringDiseases",
                "ConsumeAndGetDiseaseCount",
                "ConsumeAndGetDiseaseCounts"))
            {
                try
                {
                    var ps = m.GetParameters();
                    var p0 = (ps != null && ps.Length > 0) ? ps[0].ParameterType : null;
                    var prefixName = (p0 == typeof(Tag[]))
                        ? nameof(Patches.Storage_RouteCurrencyTagArray_Prefix)
                        : nameof(Patches.Storage_RouteCurrencyTag_Prefix);

                    PatchIfExists(harmony,
                        m,
                        prefix: new HarmonyMethod(typeof(Patches), prefixName));
                }
                catch { }
            }

            // SideScreen: bouton + UI sync (icônes/texte + charges UI + capacity swap)
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorSideScreen), "OnSpawn"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SideScreen_OnSpawn_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorSideScreen), "SetTarget"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SideScreen_SetTarget_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorSideScreen), "RefreshDisplay"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SideScreen_RefreshDisplay_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorSideScreen), "ScreenUpdate"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SideScreen_ScreenUpdate_Postfix)));

            // Printable Menu UI + consume 3 charges on open
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "OnSpawn"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_Any_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "SetTarget"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_SetTarget_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "SelectEntity"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_Any_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "RefreshDisplay"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_Any_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "ScreenUpdate"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_Any_Postfix)));

            // ✅ ROBUST SHOP SWITCH: quand les boutons d'options se régénèrent, on ré-applique les icônes/labels
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "SpawnOptionButton"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_SpawnOptionButton_Postfix)));

            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "SpawnOptionButtons"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_SpawnOptionButtons_Postfix)));

            // Reset session (si existe)
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "OnDeactivate"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_ResetSession_Postfix)));
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "OnHide"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_ResetSession_Postfix)));
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "OnCleanUp"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_ResetSession_Postfix)));
            PatchIfExists(harmony,
                AccessTools.Method(typeof(PrinterceptorScreen), "OnCmpDisable"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrinterScreen_ResetSession_Postfix)));

            // Gameplay réel: currency + wallet dynamique dans IsReadyToPrint + PrintSelectedEntity (patch TOUS les overloads)
            foreach (var m in FindIsReadyToPrintAll())
            {
                PatchIfExists(harmony,
                    m,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.IsReadyToPrint_SwapStorage_Prefix)),
                    transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.IsReadyToPrint_Transpiler)),
                    finalizer: new HarmonyMethod(typeof(Patches), nameof(Patches.IsReadyToPrint_SwapStorage_Finalizer)));
            }

            foreach (var m in FindPrintSelectedEntityAll())
            {
                PatchIfExists(harmony,
                    m,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrintSelectedEntity_SwapStorage_Prefix)),
                    transpiler: new HarmonyMethod(typeof(Patches), nameof(Patches.PrintSelectedEntity_Transpiler)),
                    finalizer: new HarmonyMethod(typeof(Patches), nameof(Patches.PrintSelectedEntity_SwapStorage_Finalizer)));
            }


            // ✅ PRINT with Plastic: redirect Storage consume count->kg for Printerceptor currency storage
            PatchPrinterceptorStorageConsume(harmony);

            // Portal charges: robuste -> Intercept force old+1 jusqu'à 15
            PatchIfExists(harmony,
                AccessTools.Method(typeof(HijackedHeadquarters.Instance), "Intercept"),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.HHInstance_Intercept_Prefix)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.HHInstance_Intercept_Postfix)));

            Debug.Log("[GravitasMod] Loaded (currency toggle + wallet robust + plastic storage + capacity swap DataBanks/Plastic + printable menu sync + charges /15 + unlock 3 + consume 3 on shop open + robust shop regen sync + ROBUST PRINT PAYMENT ROUTING).");
        }

        private static void PatchIfExists(Harmony h, MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            if (original == null) return;
            try { h.Patch(original, prefix, postfix, transpiler, finalizer); }
            catch (Exception e) { Debug.LogWarning($"[GravitasMod] Patch failed for {original.DeclaringType?.Name}.{original.Name}: {e}"); }
        }


        private static void PatchPrinterceptorStorageConsume(Harmony harmony)
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var m in typeof(Storage).GetMethods(flags))
                {
                    if (m == null || m.IsGenericMethod) continue;
                    if (m.ReturnType != typeof(void)) continue;
                    var n = m.Name ?? "";
                    if (n.IndexOf("Consume", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 2) continue;
                    if (ps[0].ParameterType != typeof(Tag)) continue;

                    if (ps[1].ParameterType == typeof(int))
                        PatchIfExists(harmony, m, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.StorageConsume_TagInt_Prefix)));
                    else if (ps[1].ParameterType == typeof(float))
                        PatchIfExists(harmony, m, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.StorageConsume_TagFloat_Prefix)));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] PatchPrinterceptorStorageConsume failed: {e}");
            }
        }

        private static IEnumerable<MethodInfo> FindIsReadyToPrintAll()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var m in typeof(HijackedHeadquarters).GetMethods(flags))
            {
                if (m == null || m.Name != "IsReadyToPrint") continue;
                var p = m.GetParameters();
                // On cible uniquement les overloads qui prennent l'Instance en 1er argument (Harmony prefix __0)
                if (p.Length >= 1 && p[0].ParameterType == typeof(HijackedHeadquarters.Instance))
                    yield return m;
            }
        }

        private static IEnumerable<MethodInfo> FindPrintSelectedEntityAll()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var m in typeof(HijackedHeadquarters.Instance).GetMethods(flags))
            {
                if (m == null || m.Name != "PrintSelectedEntity") continue;
                yield return m;
            }
        }

        private static IEnumerable<MethodInfo> FindStorageTagFirstMethods(params string[] names)
        {
            var wanted = new HashSet<string>(names ?? Array.Empty<string>());
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var m in typeof(Storage).GetMethods(flags))
            {
                if (m == null) continue;
                if (!wanted.Contains(m.Name)) continue;

                var ps = m.GetParameters();
                if (ps == null || ps.Length == 0) continue;
                var p0 = ps[0].ParameterType;
                if (p0 != typeof(Tag) && p0 != typeof(Tag[])) continue;

                yield return m;
            }
        }

        private static MethodInfo FindIuccMethod(string nameEndsWith, Type returnType, params Type[] args)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var m in typeof(HijackedHeadquarters.Instance).GetMethods(flags))
            {
                if (m == null) continue;
                if (m.ReturnType != returnType) continue;

                var n = m.Name ?? "";
                bool nameOk =
                    n.EndsWith(nameEndsWith, StringComparison.Ordinal) ||
                    n.EndsWith("IUserControlledCapacity." + nameEndsWith, StringComparison.Ordinal) ||
                    n.IndexOf("IUserControlledCapacity." + nameEndsWith, StringComparison.Ordinal) >= 0;

                if (!nameOk) continue;

                var ps = m.GetParameters();
                if ((args == null || args.Length == 0) && ps.Length == 0) return m;

                if (args != null && ps.Length == args.Length)
                {
                    bool ok = true;
                    for (int i = 0; i < args.Length; i++)
                        if (ps[i].ParameterType != args[i]) { ok = false; break; }
                    if (ok) return m;
                }
            }
            return null;
        }
    }

    // ============================================================
    // 0b) Helper: ConditionalWeakTable GetOrCreate (compat)
    // ============================================================
    internal static class CwtExt
    {
        public static TValue GetOrCreate<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> cwt, TKey key)
            where TKey : class
            where TValue : class, new()
        {
            if (!cwt.TryGetValue(key, out var v))
            {
                v = new TValue();
                cwt.Add(key, v);
            }
            return v;
        }
    }

    // ============================================================
    // 0c) TUNING
    // ============================================================
    // ============================================================
    // 0c) TUNING
    // ============================================================
    internal static class Tuning
    {
        // Data Banks (unités)
        public const float DATABANK_USERMAX_DEFAULT = 500f;
        public const float DATABANK_USERMAX_ABS = 500f;

        // Gravitas Coupon (unités)
        public const float COUPON_USERMAX_DEFAULT = 2000f;
        public const float COUPON_USERMAX_ABS = 2000f;

        // Plastic (en tonnes, 1 unité = 1 tonne = 1000 kg)
        public const float PLASTIC_USERMAX_DEFAULT_TONS = 500f;
        public const float PLASTIC_USERMAX_ABS_TONS = 500f;

        public const float TONS_TO_KG = 1000f;

        // stockage physique du Storage (kg) -> doit pouvoir contenir 500t de plastique + marge
        public const float CURRENCY_CAPACITY_KG = (PLASTIC_USERMAX_ABS_TONS * TONS_TO_KG);
    }

    // 1) STATE (persistant, par Printerceptor placé)
    // ============================================================
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PrinterceptorCurrencyState : KMonoBehaviour
    {
        public enum CurrencyMode : byte
        {
            Coupon = 0,
            Plastic = 1,
            DataBank = 2,
        }

        // Cycle demandé : Coupon → Plastic → DataBank → (repeat)
        [Serialize] public CurrencyMode Mode = CurrencyMode.DataBank;

        [Serialize] public float UserMax_DataBanks = Tuning.DATABANK_USERMAX_DEFAULT;
        [Serialize] public float UserMax_PlasticTons = Tuning.PLASTIC_USERMAX_DEFAULT_TONS;
        [Serialize] public float UserMax_Coupons = Tuning.COUPON_USERMAX_DEFAULT;

        public void Toggle()
        {
            // Rotation : DataBank -> Coupon -> Plastic -> DataBank
            switch (Mode)
            {
                case CurrencyMode.Coupon:
                    Mode = CurrencyMode.Plastic;
                    break;
                case CurrencyMode.Plastic:
                    Mode = CurrencyMode.DataBank;
                    break;
                default:
                    Mode = CurrencyMode.Coupon;
                    break;
            }
        }
    }


    // ============================================================
    // 1b) CAPACITY SWAP (IUserControlledCapacity currency-aware)
    // ============================================================
    // ============================================================
    // 1b) CAPACITY SWAP (IUserControlledCapacity currency-aware)
    // ============================================================
    internal static class CapacitySwap
    {
        private static FieldInfo _fiUserMaxCapacity;

        private static void EnsureDefaults(PrinterceptorCurrencyState s)
        {
            if (s == null) return;

            if (s.UserMax_DataBanks <= 0f) s.UserMax_DataBanks = Tuning.DATABANK_USERMAX_DEFAULT;
            if (s.UserMax_Coupons <= 0f) s.UserMax_Coupons = Tuning.COUPON_USERMAX_DEFAULT;
            if (s.UserMax_PlasticTons <= 0f) s.UserMax_PlasticTons = Tuning.PLASTIC_USERMAX_DEFAULT_TONS;

            // Clamp safety
            s.UserMax_DataBanks = Mathf.Clamp(s.UserMax_DataBanks, 0f, Tuning.DATABANK_USERMAX_ABS);
            s.UserMax_Coupons = Mathf.Clamp(s.UserMax_Coupons, 0f, Tuning.COUPON_USERMAX_ABS);
            s.UserMax_PlasticTons = Mathf.Clamp(s.UserMax_PlasticTons, 0f, Tuning.PLASTIC_USERMAX_ABS_TONS);
        }

        public static PrinterceptorCurrencyState GetState(HijackedHeadquarters.Instance inst)
        {
            var go = Currency.GetBuildingGO(inst);
            return Currency.GetOrAddState(go);
        }

        public static PrinterceptorCurrencyState.CurrencyMode GetMode(HijackedHeadquarters.Instance inst)
        {
            var s = GetState(inst);
            EnsureDefaults(s);
            return s != null ? s.Mode : PrinterceptorCurrencyState.CurrencyMode.DataBank;
        }

        public static float GetActiveUserMax(HijackedHeadquarters.Instance inst)
        {
            var s = GetState(inst);
            EnsureDefaults(s);
            if (s == null) return Tuning.DATABANK_USERMAX_DEFAULT;

            switch (s.Mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    return s.UserMax_Coupons;
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    return s.UserMax_PlasticTons;
                default:
                    return s.UserMax_DataBanks;
            }
        }

        public static float GetAbsoluteMax(HijackedHeadquarters.Instance inst)
        {
            switch (GetMode(inst))
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    return Tuning.COUPON_USERMAX_ABS;
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    return Tuning.PLASTIC_USERMAX_ABS_TONS;
                default:
                    return Tuning.DATABANK_USERMAX_ABS;
            }
        }

        public static void SetActiveUserMax(HijackedHeadquarters.Instance inst, float value)
        {
            var s = GetState(inst);
            EnsureDefaults(s);
            if (s == null) return;

            switch (s.Mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    s.UserMax_Coupons = Mathf.Clamp(value, 0f, Tuning.COUPON_USERMAX_ABS);
                    break;
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    s.UserMax_PlasticTons = Mathf.Clamp(value, 0f, Tuning.PLASTIC_USERMAX_ABS_TONS);
                    break;
                default:
                    s.UserMax_DataBanks = Mathf.Clamp(value, 0f, Tuning.DATABANK_USERMAX_ABS);
                    break;
            }

            SyncBackingField(inst);
        }

        public static float GetActiveAmountStored(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return 0f;

            var tag = Currency.GetCurrencyTag(inst);
            var st = Currency.GetWalletStorage(inst, tag);
            if (st == null) st = Currency.GetStorage(inst);
            if (st == null) return 0f;

            PrinterceptorStorageFix.EnsureCurrencyStorage(st);

            float raw = Currency.GetAmountAvailable(st, tag);

            // Plastic: raw == kg -> UI uses tons
            if (tag == Currency.PlasticTag || tag == Currency.GravitasTicketTag)
                return raw / Tuning.TONS_TO_KG;

            return raw;
        }

        public static LocString GetCapacityUnits(HijackedHeadquarters.Instance inst, LocString original)
        {
            switch (GetMode(inst))
            {
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    return new LocString("Tons of Plastic");
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    return new LocString("Gravitas Coupon");
                default:
                    return original;
            }
        }

        public static void SyncBackingField(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return;

            try
            {
                _fiUserMaxCapacity ??= AccessTools.Field(inst.GetType(), "userMaxCapacity");
                if (_fiUserMaxCapacity != null && _fiUserMaxCapacity.FieldType == typeof(float))
                    _fiUserMaxCapacity.SetValue(inst, GetActiveUserMax(inst));
            }
            catch { }
        }

        public static void AfterCurrencyToggle(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return;

            // Important : met à jour le backing field + recalcul vanilla
            SyncBackingField(inst);

            try
            {
                AccessTools.Method(inst.GetType(), "ApplyMaxCapacity")?.Invoke(inst, null);
                AccessTools.Method(inst.GetType(), "UpdateMeter")?.Invoke(inst, null);
                AccessTools.Method(inst.GetType(), "UpdateStatusItems")?.Invoke(inst, null);
            }
            catch { }
        }
    }


    // ============================================================
    // 2) CURRENCY & STORAGE HELPERS (WALLET ROBUST)
    // ============================================================
    // ============================================================
    // 2) CURRENCY & STORAGE HELPERS (WALLET ROBUST)
    // ============================================================
    internal static class Currency
    {
        public static readonly Tag DataBankTag = new Tag("DataBank");
        public static readonly Tag DataBankTagFallback = new Tag("Data Bank");
        public static readonly Tag PlasticTag = SimHashes.Polypropylene.CreateTag();

        // ✅ Third currency
        public static readonly Tag GravitasTicketTag = new Tag("GravitasTicket");

        private static FieldInfo _hhStorageField;
        private static FieldInfo _storageItemsField;
        private static PropertyInfo _storageItemsProp;

        private static readonly BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static PropertyInfo _piInstGameObject;
        private static FieldInfo _fiInstGameObject;
        private static PropertyInfo _piInstComponent;
        private static FieldInfo _fiInstComponent;

        public static GameObject GetRootGO(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return null;
            var t = inst.GetType();

            try
            {
                if (_piInstGameObject == null)
                    _piInstGameObject = t.GetProperty("gameObject", AnyInst);

                if (_piInstGameObject != null && _piInstGameObject.PropertyType == typeof(GameObject))
                {
                    var go = _piInstGameObject.GetValue(inst, null) as GameObject;
                    if (go != null) return go;
                }
            }
            catch { }

            try
            {
                if (_fiInstGameObject == null)
                    _fiInstGameObject = t.GetField("gameObject", AnyInst);

                if (_fiInstGameObject != null && _fiInstGameObject.FieldType == typeof(GameObject))
                {
                    var go = _fiInstGameObject.GetValue(inst) as GameObject;
                    if (go != null) return go;
                }
            }
            catch { }

            try
            {
                if (_piInstComponent == null)
                {
                    foreach (var p in t.GetProperties(AnyInst))
                    {
                        if (p == null) continue;
                        if (!typeof(Component).IsAssignableFrom(p.PropertyType)) continue;
                        _piInstComponent = p;
                        break;
                    }
                }

                if (_piInstComponent != null)
                {
                    var comp = _piInstComponent.GetValue(inst, null) as Component;
                    if (comp != null) return comp.gameObject;
                }
            }
            catch { }

            try
            {
                if (_fiInstComponent == null)
                {
                    foreach (var f in t.GetFields(AnyInst))
                    {
                        if (f == null) continue;
                        if (!typeof(Component).IsAssignableFrom(f.FieldType)) continue;
                        _fiInstComponent = f;
                        break;
                    }
                }

                if (_fiInstComponent != null)
                {
                    var comp = _fiInstComponent.GetValue(inst) as Component;
                    if (comp != null) return comp.gameObject;
                }
            }
            catch { }

            return null;
        }

        private sealed class StorageCache
        {
            public int lastFrame;
            public Storage[] storages;
            public Storage bestData;
            public Storage bestPlastic;
            public Storage bestTicket;
        }

        private static readonly ConditionalWeakTable<GameObject, StorageCache> _storageCaches = new ConditionalWeakTable<GameObject, StorageCache>();

        public static Storage GetStorage(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return null;
            try
            {
                _hhStorageField ??= AccessTools.Field(inst.GetType(), "m_storage");
                return _hhStorageField?.GetValue(inst) as Storage;
            }
            catch { return null; }
        }

        public static GameObject GetBuildingGO(HijackedHeadquarters.Instance inst)
        {
            var root = GetRootGO(inst);
            if (root != null) return root;

            var st = GetStorage(inst);
            return st != null ? st.gameObject : null;
        }

        public static PrinterceptorCurrencyState GetOrAddState(GameObject go)
            => go != null ? go.AddOrGet<PrinterceptorCurrencyState>() : null;

        public static Storage GetWalletStorage(HijackedHeadquarters.Instance inst, Tag currencyTag)
        {
            var root = GetBuildingGO(inst);
            if (root == null) return GetStorage(inst);

            var cache = _storageCaches.GetOrCreate(root);

            if (cache.storages == null || cache.storages.Length == 0 || (Time.frameCount - cache.lastFrame) > 60)
            {
                cache.lastFrame = Time.frameCount;
                cache.storages = root.GetComponentsInChildren<Storage>(true);
                cache.bestData = null;
                cache.bestPlastic = null;
                cache.bestTicket = null;

                if (cache.storages != null)
                {
                    for (int i = 0; i < cache.storages.Length; i++)
                    {
                        var s = cache.storages[i];
                        if (s == null) continue;

                        if (PrinterceptorStorageFix.LooksLikeCurrencyStorage(s) || PrinterceptorStorageFix.IsPrinterceptorStorage(s))
                        {
                            PrinterceptorStorageFix.EnsureCurrencyStorage(s);
                        }
                    }
                }
            }

            Storage best = null;
            if (currencyTag == PlasticTag) best = cache.bestPlastic;
            else if (currencyTag == GravitasTicketTag) best = cache.bestTicket;
            else best = cache.bestData;

            if (best != null) return best;

            best = FindBestStorage(cache.storages, currencyTag);

            if (currencyTag == PlasticTag) cache.bestPlastic = best;
            else if (currencyTag == GravitasTicketTag) cache.bestTicket = best;
            else cache.bestData = best;

            if (best != null) return best;

            var legacy = GetStorage(inst);
            if (legacy != null) return legacy;

            if (cache.storages != null && cache.storages.Length > 0) return cache.storages[0];
            return null;
        }

        private static Storage FindBestStorage(Storage[] storages, Tag currencyTag)
        {
            if (storages == null || storages.Length == 0) return null;

            int bestScore = int.MinValue;
            Storage best = null;

            for (int i = 0; i < storages.Length; i++)
            {
                var st = storages[i];
                if (st == null) continue;

                if (!PrinterceptorStorageFix.LooksLikeCurrencyStorage(st) && !PrinterceptorStorageFix.IsPrinterceptorStorage(st))
                    continue;

                int score = 0;

                try
                {
                    if (st.storageFilters != null)
                    {
                        if (st.storageFilters.Contains(currencyTag)) score += 50;
                        if (currencyTag == DataBankTag && st.storageFilters.Contains(DataBankTagFallback)) score += 40;
                    }
                }
                catch { }

                try
                {
                    float amt = GetAmountAvailable(st, currencyTag);
                    if (amt > 0f) score += 1000;
                }
                catch { }

                try { if (st.capacityKg > 0f) score += 10; } catch { }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = st;
                }
            }

            return best;
        }

        public static Tag GetCurrencyTag(HijackedHeadquarters.Instance inst)
        {
            var go = GetBuildingGO(inst);
            if (go == null) return DataBankTag;

            var s = GetOrAddState(go);
            if (s == null) return DataBankTag;

            switch (s.Mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    return GravitasTicketTag;
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    return PlasticTag;
                default:
                    return DataBankTag;
            }
        }

        private static List<GameObject> GetStorageItems(Storage st)
        {
            if (st == null) return null;

            try
            {
                _storageItemsProp ??= st.GetType().GetProperty("items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (_storageItemsProp != null)
                {
                    var v = _storageItemsProp.GetValue(st, null) as List<GameObject>;
                    if (v != null) return v;
                }
            }
            catch { }

            try
            {
                _storageItemsField ??= AccessTools.Field(st.GetType(), "items");
                return _storageItemsField?.GetValue(st) as List<GameObject>;
            }
            catch { }

            return null;
        }

        private static bool IsDataBankGO(GameObject go)
        {
            if (go == null) return false;

            try
            {
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid != null)
                {
                    if (kpid.PrefabTag == DataBankTag || kpid.PrefabTag == DataBankTagFallback)
                        return true;

                    var n = kpid.name ?? "";
                    if (n.Equals("DataBank", StringComparison.OrdinalIgnoreCase) ||
                        n.Equals("Data Bank", StringComparison.OrdinalIgnoreCase) ||
                        n.Replace(" ", "").IndexOf("databank", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                var gn = go.name ?? "";
                if (gn.Equals("DataBank", StringComparison.OrdinalIgnoreCase) ||
                    gn.Equals("Data Bank", StringComparison.OrdinalIgnoreCase) ||
                    gn.Replace(" ", "").IndexOf("databank", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch { }

            return false;
        }

        private static bool IsTicketGO(GameObject go)
        {
            if (go == null) return false;

            try
            {
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid != null)
                {
                    if (kpid.PrefabTag == GravitasTicketTag) return true;

                    var n = kpid.name ?? "";
                    if (n.IndexOf("GravitasTicket", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("gravitas_ticket", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }

                var gn = go.name ?? "";
                if (gn.IndexOf("GravitasTicket", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (gn.IndexOf("gravitas_ticket", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            catch { }

            return false;
        }

        private static bool IsPlasticGO(GameObject go, out float massKg)
        {
            massKg = 0f;
            if (go == null) return false;

            try
            {
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null && pe.ElementID == SimHashes.Polypropylene)
                {
                    massKg = pe.Mass;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static float GetAmountAvailable(Storage st, Tag currencyTag)
        {
            if (st == null) return 0f;

            try
            {
                var m = AccessTools.Method(st.GetType(), "GetAmountAvailable", new[] { typeof(Tag) });
                if (m != null)
                {
                    var v = m.Invoke(st, new object[] { currencyTag });
                    if (v is float f && f > 0f) return f;
                    if (v is int i && i > 0) return i;
                }
            }
            catch { }

            var items = GetStorageItems(st);
            if (items == null) return 0f;

            if (currencyTag == PlasticTag)
            {
                float totalKg = 0f;
                for (int i = 0; i < items.Count; i++)
                    if (IsPlasticGO(items[i], out var kg))
                        totalKg += kg;
                return totalKg;
            }

            // Coupon + DataBank = unités
            int count = 0;
            float kgSum = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool isUnit =
                    (currencyTag == GravitasTicketTag) ? IsTicketGO(it) : IsDataBankGO(it);

                if (!isUnit) continue;

                count++;

                try
                {
                    var pe = it.GetComponent<PrimaryElement>();
                    if (pe != null && pe.Mass > 0f) kgSum += pe.Mass;
                }
                catch { }
            }

            // Heuristic: if mass is used instead of count, return mass; else count
            if (kgSum > (count + 0.5f)) return kgSum;
            return count;
        }

        // --- PRINT helper: consume Plastic as kilograms from a Storage (elements are stored as chunks with PrimaryElement mass).
        private static PropertyInfo _piPrimaryElement_Mass;
        private static FieldInfo _fiPrimaryElement_Mass;
        private static MethodInfo _miPrimaryElement_SetMass;

        public static bool TryConsumePlasticKg(Storage st, float kg)
        {
            if (st == null) return false;

            kg = Mathf.Max(0f, kg);
            if (kg <= 0.001f) return true;

            // Ensure we have enough
            float available = GetAmountAvailable(st, PlasticTag);
            if (available + 0.001f < kg) return false;

            var items = GetStorageItems(st);
            if (items == null || items.Count == 0) return false;

            float remaining = kg;

            // Iterate backwards to reduce risk if underlying list changes during destruction
            for (int i = items.Count - 1; i >= 0 && remaining > 0.001f; i--)
            {
                var go = items[i];
                if (!IsPlasticGO(go, out var mass) || mass <= 0f) continue;

                if (mass <= remaining + 0.001f)
                {
                    remaining -= mass;
                    SafeDestroy(go);
                }
                else
                {
                    // Partial consume: reduce chunk mass
                    float newMass = Mathf.Max(0f, mass - remaining);
                    if (!TrySetPrimaryElementMass(go, newMass))
                    {
                        // Fallback: destroy chunk (slight overpay) rather than blocking PRINT
                        SafeDestroy(go);
                    }
                    remaining = 0f;
                    break;
                }
            }

            return remaining <= 0.001f;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            try { Util.KDestroyGameObject(go); }
            catch { try { UnityEngine.Object.Destroy(go); } catch { } }
        }

        private static bool TrySetPrimaryElementMass(GameObject go, float newMass)
        {
            if (go == null) return false;

            var pe = go.GetComponent<PrimaryElement>();
            if (pe == null) return false;

            if (newMass <= 0.001f)
            {
                SafeDestroy(go);
                return true;
            }

            try
            {
                if (_miPrimaryElement_SetMass == null)
                {
                    // Try common Klei API name
                    _miPrimaryElement_SetMass = AccessTools.Method(typeof(PrimaryElement), "SetMass", new[] { typeof(float) });
                }

                if (_miPrimaryElement_SetMass != null)
                {
                    _miPrimaryElement_SetMass.Invoke(pe, new object[] { newMass });
                    return true;
                }

                if (_piPrimaryElement_Mass == null)
                    _piPrimaryElement_Mass = AccessTools.Property(typeof(PrimaryElement), "Mass");

                if (_piPrimaryElement_Mass != null && _piPrimaryElement_Mass.CanWrite)
                {
                    _piPrimaryElement_Mass.SetValue(pe, newMass, null);
                    return true;
                }

                if (_fiPrimaryElement_Mass == null)
                    _fiPrimaryElement_Mass = AccessTools.Field(typeof(PrimaryElement), "Mass");

                if (_fiPrimaryElement_Mass != null)
                {
                    _fiPrimaryElement_Mass.SetValue(pe, newMass);
                    return true;
                }
            }
            catch { }

            return false;
        }

        // --- UI Sprite (DataBank / Plastic / GravitasTicket)
        private static Sprite _ticketSprite;
        private static Color _ticketColor = Color.white;
        private static bool _ticketTried;

        public static bool TryGetUISpriteSafe(Tag tag, out Sprite sprite, out Color color)
        {
            sprite = null;
            color = Color.white;

            try
            {
                var ui = Def.GetUISprite(tag, "ui");
                if (ui != null && ui.first != null)
                {
                    sprite = ui.first;
                    color = ui.second;
                    return true;
                }
            }
            catch { }

            try
            {
                if (tag == PlasticTag)
                {
                    var element = ElementLoader.FindElementByHash(SimHashes.Polypropylene);
                    if (element != null)
                    {
                        var substanceProp = AccessTools.Property(element.GetType(), "substance");
                        var substance = substanceProp?.GetValue(element, null);
                        if (substance != null)
                        {
                            var uiSpriteField = AccessTools.Field(substance.GetType(), "uiSprite");
                            var sp = uiSpriteField?.GetValue(substance) as Sprite;
                            if (sp != null)
                            {
                                sprite = sp;
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                if (tag == DataBankTag)
                {
                    var ui = Def.GetUISprite(DataBankTagFallback, "ui");
                    if (ui != null && ui.first != null)
                    {
                        sprite = ui.first;
                        color = ui.second;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (tag == GravitasTicketTag)
                {
                    if (_ticketSprite != null)
                    {
                        sprite = _ticketSprite;
                        color = _ticketColor;
                        return true;
                    }

                    if (!_ticketTried)
                    {
                        _ticketTried = true;

                        // Try Assets sprite database first
                        try
                        {
                            var sp = Assets.GetSprite("gravitas_ticket_icon") ?? Assets.GetSprite("gravitas_ticket_icon.png");
                            if (sp != null)
                            {
                                _ticketSprite = sp;
                                sprite = sp;
                                return true;
                            }
                        }
                        catch { }

                        // Fallback: load png from mod folder (recursive)
                        if (TryLoadSpriteFromPng("gravitas_ticket_icon.png", out var sp2))
                        {
                            _ticketSprite = sp2;
                            sprite = sp2;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool TryLoadSpriteFromPng(string fileName, out Sprite sp)
        {
            sp = null;
            try
            {
                var asmPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(asmPath)) return false;

                var dir = Path.GetDirectoryName(asmPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;

                string found = null;

                try
                {
                    var files = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
                    if (files != null && files.Length > 0) found = files[0];
                }
                catch { }

                if (string.IsNullOrEmpty(found) || !File.Exists(found)) return false;

                var data = File.ReadAllBytes(found);
                if (data == null || data.Length < 8) return false;

                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(data)) return false;

                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = Path.GetFileNameWithoutExtension(fileName);
                return sp != null;
            }
            catch { return false; }
        }
    }


    // ============================================================
    // 2a) PRINT WALLET SWAP (runtime) : force m_storage vers le wallet actif
    // ============================================================
    internal sealed class StorageSwapState
    {
        public Storage previous;
        public bool changed;
    }

    internal static class PrintWalletSwap
    {
        private static FieldInfo _fiMStorage;


        private static FieldInfo ResolveStorageField(Type t)
        {
            if (_fiMStorage != null && typeof(Storage).IsAssignableFrom(_fiMStorage.FieldType))
                return _fiMStorage;

            try
            {
                _fiMStorage = AccessTools.Field(t, "m_storage") ?? AccessTools.Field(t, "storage");
                if (_fiMStorage != null && typeof(Storage).IsAssignableFrom(_fiMStorage.FieldType))
                    return _fiMStorage;

                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var f in t.GetFields(flags))
                {
                    if (f == null) continue;
                    if (typeof(Storage).IsAssignableFrom(f.FieldType))
                    {
                        _fiMStorage = f;
                        return _fiMStorage;
                    }
                }
            }
            catch { }

            return _fiMStorage;
        }
        public static StorageSwapState Enter(HijackedHeadquarters.Instance inst)
        {
            var state = new StorageSwapState();
            if (inst == null) return state;

            try
            {
                _fiMStorage ??= ResolveStorageField(inst.GetType());
                if (_fiMStorage == null || !typeof(Storage).IsAssignableFrom(_fiMStorage.FieldType)) return state;

                var prev = _fiMStorage.GetValue(inst) as Storage;
                state.previous = prev;

                var tag = Currency.GetCurrencyTag(inst);
                var desired = Currency.GetWalletStorage(inst, tag);
                if (desired != null && !ReferenceEquals(desired, prev))
                {
                    _fiMStorage.SetValue(inst, desired);
                    state.changed = true;
                }
            }
            catch { }

            return state;
        }

        public static void Exit(HijackedHeadquarters.Instance inst, StorageSwapState state)
        {
            if (inst == null || state == null) return;
            if (!state.changed) return;

            try
            {
                _fiMStorage ??= ResolveStorageField(inst.GetType());
                if (_fiMStorage == null) return;
                _fiMStorage.SetValue(inst, state.previous);
            }
            catch { }
        }
    }

    // ============================================================
    // 2b) GLOBAL SPRITE CACHE
    // ============================================================
    // ============================================================
    // 2b) GLOBAL SPRITE CACHE
    // ============================================================
    internal static class CurrencySpriteCache
    {
        private static Sprite _dbSprite;
        private static Color _dbColor = Color.white;

        private static Sprite _plSprite;
        private static Color _plColor = Color.white;

        private static Sprite _cpSprite;
        private static Color _cpColor = Color.white;

        public static void Observe(Tag tag, Sprite sp, Color col)
        {
            if (sp == null) return;

            if (tag == Currency.DataBankTag || tag == Currency.DataBankTagFallback)
            {
                _dbSprite = sp; _dbColor = col; return;
            }
            if (tag == Currency.PlasticTag || tag == Currency.GravitasTicketTag)
            {
                _plSprite = sp; _plColor = col; return;
            }
            if (tag == Currency.GravitasTicketTag)
            {
                _cpSprite = sp; _cpColor = col; return;
            }
        }

        public static bool TryGet(Tag tag, out Sprite sp, out Color col)
        {
            if (Currency.TryGetUISpriteSafe(tag, out sp, out col) && sp != null)
            {
                Observe(tag, sp, col);
                return true;
            }

            if (tag == Currency.DataBankTag || tag == Currency.DataBankTagFallback)
            {
                if (_dbSprite != null) { sp = _dbSprite; col = _dbColor; return true; }
            }
            if (tag == Currency.PlasticTag || tag == Currency.GravitasTicketTag)
            {
                if (_plSprite != null) { sp = _plSprite; col = _plColor; return true; }
            }
            if (tag == Currency.GravitasTicketTag)
            {
                if (_cpSprite != null) { sp = _cpSprite; col = _cpColor; return true; }
            }

            sp = null;
            col = Color.white;
            return false;
        }
    }


    // ============================================================
    // 2c) CURRENCY ROUTER (PRINT payment fix)
    // ============================================================
    // ============================================================
    // 2c) CURRENCY ROUTER (PRINT payment fix)
    // ============================================================
    internal static class CurrencyRouter
    {
        public static Tag Route(Storage st, Tag requested)
        {
            if (st == null) return requested;

            // Printerceptor-only routing: if this Storage belongs to a Printerceptor that has our state,
            // we force *any* known currency tag (DataBank / Plastic / Coupon) to the active one.
            PrinterceptorCurrencyState state = null;
            try { state = st.GetComponentInParent<PrinterceptorCurrencyState>(true); } catch { state = null; }
            if (state == null) return requested;

            Tag active;
            switch (state.Mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon:
                    active = Currency.GravitasTicketTag;
                    break;
                case PrinterceptorCurrencyState.CurrencyMode.Plastic:
                    active = Currency.PlasticTag;
                    break;
                default:
                    active = Currency.DataBankTag;
                    break;
            }

            try { PrinterceptorStorageFix.EnsureCurrencyStorage(st); } catch { }

            if (requested == Currency.DataBankTag || requested == Currency.DataBankTagFallback ||
                requested == Currency.PlasticTag || requested == Currency.GravitasTicketTag)
            {
                return active;
            }

            return requested;
        }
    }


    // ============================================================
    // 3) STORAGE FIX: accept DataBank + Plastic in Printerceptor currency storage
    // ============================================================
    // ============================================================
    // 3) STORAGE FIX: accept DataBank + Plastic + GravitasTicket in Printerceptor currency storage
    // ============================================================
    internal static class PrinterceptorStorageFix
    {
        private static readonly HashSet<int> _printerceptorStorageIds = new HashSet<int>();

        public static void Register(Storage st)
        {
            if (st == null) return;
            _printerceptorStorageIds.Add(st.GetInstanceID());
        }

        public static bool IsPrinterceptorStorage(Storage st)
        {
            return st != null && _printerceptorStorageIds.Contains(st.GetInstanceID());
        }

        public static bool LooksLikeCurrencyStorage(Storage st)
        {
            if (st == null) return false;

            try
            {
                var f = st.storageFilters;
                if (f != null)
                {
                    if (f.Contains(Currency.DataBankTag) || f.Contains(Currency.DataBankTagFallback)) return true;
                    if (f.Contains(Currency.PlasticTag)) return true;
                    if (f.Contains(Currency.GravitasTicketTag)) return true;
                }
            }
            catch { }

            try
            {
                var n = (st.name ?? "").ToLowerInvariant();
                if (n.Contains("databank") || (n.Contains("data") && n.Contains("bank"))) return true;
                if (n.Contains("plastic") || n.Contains("plastique") || n.Contains("polypropylene")) return true;
                if (n.Contains("gravitas") && (n.Contains("ticket") || n.Contains("coupon"))) return true;
            }
            catch { }

            return false;
        }

        public static void EnsureFilters(Storage st)
        {
            if (st == null) return;

            try
            {
                if (st.storageFilters == null)
                    st.storageFilters = new List<Tag>();

                if (!st.storageFilters.Contains(Currency.DataBankTag))
                    st.storageFilters.Add(Currency.DataBankTag);

                if (!st.storageFilters.Contains(Currency.PlasticTag))
                    st.storageFilters.Add(Currency.PlasticTag);

                if (!st.storageFilters.Contains(Currency.GravitasTicketTag))
                    st.storageFilters.Add(Currency.GravitasTicketTag);
            }
            catch { }
        }

        public static void EnsureCapacity(Storage st)
        {
            if (st == null) return;
            try
            {
                if (st.capacityKg < Tuning.CURRENCY_CAPACITY_KG - 0.1f)
                    st.capacityKg = Tuning.CURRENCY_CAPACITY_KG;
            }
            catch { }
        }

        public static void EnsureCurrencyStorage(Storage st)
        {
            if (st == null) return;
            Register(st);
            EnsureFilters(st);
            EnsureCapacity(st);
        }
    }


    // ============================================================
    // 4) UI ICON CACHE (SideScreen)
    // ============================================================
    internal static class PrinterceptorUiIconCache
    {
        private sealed class Cache
        {
            public Sprite[] sideSprites;
            public Color[] sideColors;
        }

        private static readonly ConditionalWeakTable<object, Cache> _caches = new ConditionalWeakTable<object, Cache>();

        public static void TryCaptureSideDefaults(object screenObj, Image[] icons)
        {
            if (screenObj == null || icons == null || icons.Length == 0) return;

            if (!_caches.TryGetValue(screenObj, out var cache))
            {
                cache = new Cache();
                _caches.Add(screenObj, cache);
            }

            if (cache.sideSprites != null && cache.sideSprites.Length == icons.Length)
                return;

            for (int i = 0; i < icons.Length; i++)
                if (icons[i] == null || icons[i].sprite == null)
                    return;

            cache.sideSprites = new Sprite[icons.Length];
            cache.sideColors = new Color[icons.Length];

            for (int i = 0; i < icons.Length; i++)
            {
                cache.sideSprites[i] = icons[i].sprite;
                cache.sideColors[i] = icons[i].color;
            }
        }

        public static bool RestoreSideDefaults(object screenObj, Image[] icons)
        {
            if (screenObj == null || icons == null) return false;
            if (!_caches.TryGetValue(screenObj, out var cache)) return false;
            if (cache.sideSprites == null || cache.sideSprites.Length != icons.Length) return false;

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].sprite = cache.sideSprites[i];
                icons[i].color = cache.sideColors[i];
            }
            return true;
        }
        private static void TryApplyCurrencyToAllIcons(GameObject root, Tag activeTag)
        {
            if (root == null) return;

            // Target sprite/color
            if (!CurrencySpriteCache.TryGet(activeTag, out var targetSp, out var targetCol) || targetSp == null)
                return;

            // Known currency sprites to detect/replace (DataBank / Plastic)
            CurrencySpriteCache.TryGet(Currency.DataBankTag, out var dbSp, out _);
            CurrencySpriteCache.TryGet(Currency.PlasticTag, out var plSp, out _);

            var imgs = root.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img == null) continue;
                var sp = img.sprite;
                if (sp == null) continue;

                bool candidate =
                    (SpriteMatches(sp, dbSp) || SpriteMatches(sp, plSp));

                if (!candidate)
                {
                    var nm = (img.name ?? string.Empty).ToLowerInvariant();
                    if (nm.Contains("databank") || nm.Contains("data_bank") || nm.Contains("wallet") || nm.Contains("currency") || nm.Contains("cost"))
                        candidate = true;
                }

                if (!candidate) continue;

                img.sprite = targetSp;
                img.color = targetCol;
            }
        }

        private static bool SpriteMatches(Sprite a, Sprite b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Same atlas region (handles duplicated Sprite instances)
            if (ReferenceEquals(a.texture, b.texture))
            {
                Rect ra = a.rect;
                Rect rb = b.rect;

                if (Mathf.Abs(ra.x - rb.x) < 0.01f &&
                    Mathf.Abs(ra.y - rb.y) < 0.01f &&
                    Mathf.Abs(ra.width - rb.width) < 0.01f &&
                    Mathf.Abs(ra.height - rb.height) < 0.01f)
                    return true;
            }

            // Last resort: name match
            if (!string.IsNullOrEmpty(a.name) && a.name == b.name) return true;

            return false;
        }


    }

    // ============================================================
    // 5) PORTAL CHARGES (inchangé)
    // ============================================================
    internal static class PortalCharges
    {
        public const int MAX_CHARGES = 15;
        public const int UNLOCK_COST = 3;

        private static bool _init;
        private static object _interceptChargesParamInstance;
        private static MethodInfo _miGet;
        private static MethodInfo _miSet;

        private static FieldInfo _fiSSProgressIndicators;
        private static FieldInfo _fiSSInterceptStateLabel;
        private static FieldInfo _fiSSTarget;
        private static MethodInfo _miScheduleNextFrame;

        private static FieldInfo _fiPSTarget;

        private sealed class ShopSession
        {
            public int lastBuildingId = -1;
            public bool charged = false;
        }
        private static readonly ConditionalWeakTable<PrinterceptorScreen, ShopSession> _shopSessions = new ConditionalWeakTable<PrinterceptorScreen, ShopSession>();

        private static readonly BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool TryBindChargesParam(object candidate)
        {
            if (candidate == null) return false;

            try
            {
                var t = candidate.GetType();
                MethodInfo get = null, set = null;

                foreach (var m in t.GetMethods(AnyInst))
                {
                    if (m == null) continue;
                    if (m.ReturnType != typeof(int)) continue;
                    if (m.Name != "Get") continue;

                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (!ps[0].ParameterType.IsAssignableFrom(typeof(HijackedHeadquarters.Instance)))
                        continue;

                    get = m;
                    break;
                }

                foreach (var m in t.GetMethods(AnyInst))
                {
                    if (m == null) continue;
                    if (m.ReturnType != typeof(void)) continue;
                    if (m.Name != "Set") continue;

                    var ps = m.GetParameters();
                    if (ps.Length != 2) continue;
                    if (!ps[0].ParameterType.IsAssignableFrom(typeof(HijackedHeadquarters.Instance)))
                        continue;
                    if (ps[1].ParameterType != typeof(int))
                        continue;

                    set = m;
                    break;
                }

                if (get == null || set == null) return false;

                _interceptChargesParamInstance = candidate;
                _miGet = get;
                _miSet = set;
                return true;
            }
            catch { return false; }
        }

        private static void ResolveChargesParam()
        {
            try
            {
                var fi = AccessTools.Field(typeof(HijackedHeadquarters), "interceptCharges");
                if (fi != null)
                {
                    var obj = fi.GetValue(null);
                    if (TryBindChargesParam(obj)) return;
                }
            }
            catch { }

            try
            {
                foreach (var f in typeof(HijackedHeadquarters).GetFields(StaticAny))
                {
                    if (f == null) continue;
                    if (!f.IsStatic) continue;

                    var n = f.Name ?? "";
                    var low = n.ToLowerInvariant();
                    if (!(low.Contains("intercept") && low.Contains("charge"))) continue;

                    var obj = f.GetValue(null);
                    if (TryBindChargesParam(obj)) return;
                }
            }
            catch { }

            try
            {
                foreach (var p in typeof(HijackedHeadquarters).GetProperties(StaticAny))
                {
                    if (p == null) continue;
                    var n = p.Name ?? "";
                    var low = n.ToLowerInvariant();
                    if (!(low.Contains("intercept") && low.Contains("charge"))) continue;

                    var obj = p.GetValue(null, null);
                    if (TryBindChargesParam(obj)) return;
                }
            }
            catch { }
        }

        public static void EnsureInit()
        {
            if (_init) return;
            _init = true;

            ResolveChargesParam();

            _fiSSProgressIndicators = AccessTools.Field(typeof(PrinterceptorSideScreen), "progressIndicators");
            _fiSSInterceptStateLabel = AccessTools.Field(typeof(PrinterceptorSideScreen), "interceptStateLabel");
            _fiSSTarget = AccessTools.Field(typeof(PrinterceptorSideScreen), "target");

            _fiPSTarget = AccessTools.Field(typeof(PrinterceptorScreen), "target");
            if (_fiPSTarget == null || _fiPSTarget.FieldType != typeof(HijackedHeadquarters.Instance))
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var f in typeof(PrinterceptorScreen).GetFields(flags))
                {
                    if (f.FieldType == typeof(HijackedHeadquarters.Instance))
                    {
                        _fiPSTarget = f;
                        break;
                    }
                }
            }
        }

        private static HijackedHeadquarters.Instance GetSideTargetRobust(PrinterceptorSideScreen screen)
        {
            if (screen == null) return null;

            try
            {
                EnsureInit();

                var inst = _fiSSTarget?.GetValue(screen) as HijackedHeadquarters.Instance;
                if (inst != null) return inst;

                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var t = screen.GetType();
                foreach (var f in t.GetFields(flags))
                {
                    if (f.FieldType == typeof(HijackedHeadquarters.Instance))
                    {
                        _fiSSTarget = f;
                        return f.GetValue(screen) as HijackedHeadquarters.Instance;
                    }
                }
            }
            catch { }

            return null;
        }

        private static int ParseChargesFromText(string txt)
        {
            if (string.IsNullOrEmpty(txt)) return -1;
            int slash = txt.IndexOf('/');
            if (slash <= 0) return -1;

            int i = 0;
            int a = 0;
            while (i < slash && char.IsDigit(txt[i]))
            {
                a = (a * 10) + (txt[i] - '0');
                i++;
            }
            return (i == slash) ? a : -1;
        }

        private static int FallbackChargesFromIndicators(PrinterceptorSideScreen screen)
        {
            try
            {
                var arr = _fiSSProgressIndicators?.GetValue(screen) as GameObject[];
                if (arr == null) return -1;

                int c = 0;
                for (int i = 0; i < arr.Length; i++)
                {
                    var go = arr[i];
                    if (go != null && go.activeSelf) c++;
                }
                return c;
            }
            catch { return -1; }
        }

        public static int GetCharges(HijackedHeadquarters.Instance inst)
        {
            try
            {
                EnsureInit();
                if (inst == null || _interceptChargesParamInstance == null || _miGet == null) return 0;
                return (int)_miGet.Invoke(_interceptChargesParamInstance, new object[] { inst });
            }
            catch { return 0; }
        }

        public static bool SetCharges(HijackedHeadquarters.Instance inst, int value)
        {
            try
            {
                EnsureInit();
                if (inst == null || _interceptChargesParamInstance == null || _miSet == null) return false;
                _miSet.Invoke(_interceptChargesParamInstance, new object[] { inst, value });
                return true;
            }
            catch { return false; }
        }

        public static void ClampToMax(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return;
            int c = GetCharges(inst);
            if (c > MAX_CHARGES) SetCharges(inst, MAX_CHARGES);
            if (c < 0) SetCharges(inst, 0);
        }

        private static string ExtractSuffix(string oldText)
        {
            if (string.IsNullOrEmpty(oldText)) return "Stored Charges";
            int spaceIdx = oldText.IndexOf(' ');
            if (spaceIdx >= 0 && spaceIdx + 1 < oldText.Length)
            {
                var candidate = oldText.Substring(spaceIdx + 1).Trim();
                if (!string.IsNullOrEmpty(candidate)) return candidate;
            }
            return "Stored Charges";
        }

        private static bool LooksLikeChargeCounterText(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.IndexOf("charge", StringComparison.OrdinalIgnoreCase) < 0) return false;

            int slash = s.IndexOf('/');
            if (slash <= 0 || slash > 5) return false;
            for (int i = 0; i < slash; i++)
                if (!char.IsDigit(s[i])) return false;

            if (slash + 1 >= s.Length || !char.IsDigit(s[slash + 1])) return false;
            return true;
        }

        private static void ApplyChargeLabelByScan(GameObject root, int current)
        {
            if (root == null) return;

            var locs = root.GetComponentsInChildren<LocText>(true);
            if (locs == null || locs.Length == 0) return;

            for (int i = 0; i < locs.Length; i++)
            {
                var lt = locs[i];
                if (lt == null) continue;

                var txt = lt.text ?? "";
                if (!LooksLikeChargeCounterText(txt)) continue;

                var suffix = ExtractSuffix(txt);
                lt.SetText($"{current}/{MAX_CHARGES} {suffix}");
            }
        }

        private static void ApplyChargeLabelByScan(PrinterceptorSideScreen screen, int current)
        {
            if (screen == null) return;
            ApplyChargeLabelByScan(screen.gameObject, current);
        }

        public static void ApplyChargesUI(PrinterceptorSideScreen screen)
        {
            try
            {
                EnsureInit();
                if (screen == null) return;

                string oldText = null;
                try
                {
                    var label0 = _fiSSInterceptStateLabel?.GetValue(screen) as LocText;
                    if (label0 != null) oldText = label0.text;
                }
                catch { }

                var inst = GetSideTargetRobust(screen);
                if (inst == null) return;

                ClampToMax(inst);
                int current = Mathf.Clamp(GetCharges(inst), 0, MAX_CHARGES);

                if (current == 0)
                {
                    int parsed = ParseChargesFromText(oldText ?? "");
                    if (parsed > 0) current = parsed;
                    else
                    {
                        int ind = FallbackChargesFromIndicators(screen);
                        if (ind > 0) current = ind;
                    }
                }

                var arr = _fiSSProgressIndicators?.GetValue(screen) as GameObject[];
                if (arr != null && arr.Length > 0)
                {
                    int max = arr.Length;
                    int show = Mathf.Min(current, max);
                    for (int i = 0; i < max; i++)
                    {
                        var go = arr[i];
                        if (go != null) go.SetActive(i < show);
                    }
                }

                var label = _fiSSInterceptStateLabel?.GetValue(screen) as LocText;
                if (label != null)
                {
                    string suffix = ExtractSuffix(label.text ?? oldText ?? "");
                    label.SetText($"{current}/{MAX_CHARGES} {suffix}");
                }

                ApplyChargeLabelByScan(screen, current);
            }
            catch { }
        }

        public static void ApplyChargesUIDeferred(PrinterceptorSideScreen screen)
        {
            if (screen == null) return;

            try
            {
                var gs = GameScheduler.Instance;
                if (gs == null)
                {
                    ApplyChargesUI(screen);
                    return;
                }

                _miScheduleNextFrame ??= AccessTools.Method(gs.GetType(), "ScheduleNextFrame",
                    new[] { typeof(string), typeof(Action<object>), typeof(object) });

                if (_miScheduleNextFrame != null)
                {
                    _miScheduleNextFrame.Invoke(gs, new object[]
                    {
                        "GravitasMod_ChargesUI",
                        (Action<object>)(_ => ApplyChargesUI(screen)),
                        null
                    });
                    return;
                }

                _miScheduleNextFrame ??= AccessTools.Method(gs.GetType(), "ScheduleNextFrame",
                    new[] { typeof(string), typeof(Action<object>) });

                if (_miScheduleNextFrame != null)
                {
                    _miScheduleNextFrame.Invoke(gs, new object[]
                    {
                        "GravitasMod_ChargesUI",
                        (Action<object>)(_ => ApplyChargesUI(screen))
                    });
                    return;
                }
            }
            catch { }

            ApplyChargesUI(screen);
        }

        public static void ApplyChargesUIRobust(PrinterceptorSideScreen screen)
        {
            ApplyChargesUI(screen);
            ApplyChargesUIDeferred(screen);
        }

        public static void ResetShopSession(PrinterceptorScreen ps)
        {
            if (ps == null) return;
            try
            {
                if (_shopSessions.TryGetValue(ps, out var s))
                {
                    s.charged = false;
                    s.lastBuildingId = -1;
                }
            }
            catch { }
        }

        private static int GetBuildingId(HijackedHeadquarters.Instance inst)
        {
            var go = Currency.GetBuildingGO(inst);
            if (go != null) return go.GetInstanceID();
            return inst != null ? inst.GetHashCode() : 0;
        }

        public static void TryConsumeShopEntry(PrinterceptorScreen ps)
        {
            try
            {
                EnsureInit();
                if (ps == null || ps.gameObject == null) return;

                var inst = _fiPSTarget?.GetValue(ps) as HijackedHeadquarters.Instance;
                if (inst == null) return;

                var session = _shopSessions.GetOrCreate(ps);

                int bid = GetBuildingId(inst);
                if (session.lastBuildingId != bid)
                {
                    session.lastBuildingId = bid;
                    session.charged = false;
                }

                if (session.charged) return;

                ClampToMax(inst);
                int current = GetCharges(inst);
                if (current < UNLOCK_COST) return;

                int newVal = Mathf.Max(0, current - UNLOCK_COST);
                if (SetCharges(inst, newVal))
                {
                    session.charged = true;

                    UIX.RefreshPrintableMenusSafe();
                    UIX.RefreshSideScreensForTargetSafe(inst);

                    AccessTools.Method(inst.GetType(), "UpdateMeter")?.Invoke(inst, null);
                    AccessTools.Method(inst.GetType(), "UpdateStatusItems")?.Invoke(inst, null);
                }
            }
            catch { }
        }
    }

    // ============================================================
    // 6) UI APPLY + SAFE REFRESH
    // ============================================================
    // ============================================================
    // 6) UI APPLY + SAFE REFRESH
    // ============================================================
    internal static class UIX
    {
        private const string SwitchBtnName = "GravitasMod_SwitchCurrencyButton";

        private static readonly FieldInfo SS_InterceptButton = AccessTools.Field(typeof(PrinterceptorSideScreen), "interceptButton");
        private static readonly FieldInfo SS_DatabankIcons = AccessTools.Field(typeof(PrinterceptorSideScreen), "databankIcon");
        private static readonly FieldInfo SS_DatabankLabel = AccessTools.Field(typeof(PrinterceptorSideScreen), "databankCountLabel");
        private static readonly FieldInfo SS_TargetInstance = AccessTools.Field(typeof(PrinterceptorSideScreen), "target");

        private static FieldInfo _psTargetField;
        private static readonly FieldInfo PS_WalletIconField = AccessTools.Field(typeof(PrinterceptorScreen), "dataWalletIcon");
        private static readonly FieldInfo PS_WalletLabel = AccessTools.Field(typeof(PrinterceptorScreen), "dataWalletLabel");
        private static readonly FieldInfo PS_CostIconField = AccessTools.Field(typeof(PrinterceptorScreen), "selectedCostIcon");
        private static readonly FieldInfo PS_CostLabel = AccessTools.Field(typeof(PrinterceptorScreen), "selectedCostLabel");

        private static bool IsSceneObject(GameObject go)
        {
            if (go == null) return false;
            return go.scene.IsValid() && go.scene.isLoaded;
        }

        private static HijackedHeadquarters.Instance GetSideTarget(PrinterceptorSideScreen ss)
            => SS_TargetInstance?.GetValue(ss) as HijackedHeadquarters.Instance;

        private static FieldInfo GetPrinterTargetField()
        {
            if (_psTargetField != null) return _psTargetField;

            _psTargetField = AccessTools.Field(typeof(PrinterceptorScreen), "target");
            if (_psTargetField != null && _psTargetField.FieldType == typeof(HijackedHeadquarters.Instance))
                return _psTargetField;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var f in typeof(PrinterceptorScreen).GetFields(flags))
            {
                if (f.FieldType == typeof(HijackedHeadquarters.Instance))
                {
                    _psTargetField = f;
                    break;
                }
            }
            return _psTargetField;
        }

        private static HijackedHeadquarters.Instance GetPrinterTarget(PrinterceptorScreen ps)
        {
            try { return GetPrinterTargetField()?.GetValue(ps) as HijackedHeadquarters.Instance; }
            catch { return null; }
        }

        private static void ClearKButtonOnClick(KButton btn)
        {
            if (btn == null) return;
            try
            {
                var clear = AccessTools.Method(btn.GetType(), "ClearOnClick");
                if (clear != null) { clear.Invoke(btn, null); return; }

                var f = AccessTools.Field(btn.GetType(), "onClick");
                if (f != null && typeof(Delegate).IsAssignableFrom(f.FieldType))
                    f.SetValue(btn, null);
            }
            catch { }
        }

        private static PrinterceptorCurrencyState.CurrencyMode GetMode(GameObject buildingGO)
        {
            var st = Currency.GetOrAddState(buildingGO);
            return st != null ? st.Mode : PrinterceptorCurrencyState.CurrencyMode.DataBank;
        }

        private static string ModeLabel(PrinterceptorCurrencyState.CurrencyMode mode)
        {
            switch (mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon: return "Coupon";
                case PrinterceptorCurrencyState.CurrencyMode.Plastic: return "Plastic";
                default: return "Data Bank";
            }
        }

        private static string NextModeLabel(PrinterceptorCurrencyState.CurrencyMode mode)
        {
            // Cycle : Coupon -> Plastic -> DataBank -> Coupon
            switch (mode)
            {
                case PrinterceptorCurrencyState.CurrencyMode.Coupon: return "Plastic";
                case PrinterceptorCurrencyState.CurrencyMode.Plastic: return "Data Bank";
                default: return "Coupon";
            }
        }

        public static void RefreshPrintableMenusSafe()
        {
            try
            {
                var arr = Resources.FindObjectsOfTypeAll<PrinterceptorScreen>();
                if (arr == null) return;

                for (int i = 0; i < arr.Length; i++)
                {
                    var ps = arr[i];
                    if (ps == null || ps.gameObject == null) continue;
                    if (!IsSceneObject(ps.gameObject)) continue;
                    if (!ps.gameObject.activeInHierarchy) continue;

                    try { ApplyPrinterScreen(ps); } catch { }
                }
            }
            catch { }
        }

        public static void RefreshSideScreensForTargetSafe(HijackedHeadquarters.Instance inst)
        {
            if (inst == null) return;

            try
            {
                var arr = Resources.FindObjectsOfTypeAll<PrinterceptorSideScreen>();
                if (arr == null) return;

                for (int i = 0; i < arr.Length; i++)
                {
                    var ss = arr[i];
                    if (ss == null || ss.gameObject == null) continue;
                    if (!IsSceneObject(ss.gameObject)) continue;
                    if (!ss.gameObject.activeInHierarchy) continue;

                    var t = GetSideTarget(ss);
                    if (!ReferenceEquals(t, inst)) continue;

                    try
                    {
                        ApplySideScreen(ss);
                        PortalCharges.ApplyChargesUIRobust(ss);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void EnsureSwitchButton(PrinterceptorSideScreen ss)
        {
            if (ss == null || ss.gameObject == null) return;
            if (!IsSceneObject(ss.gameObject)) return;

            var intercept = SS_InterceptButton?.GetValue(ss) as KButton;
            if (intercept == null) return;

            var parent = intercept.transform != null ? intercept.transform.parent : null;
            if (parent == null) return;

            var existing = parent.Find(SwitchBtnName);
            if (existing != null)
            {
                var btn = existing.GetComponent<KButton>();
                if (btn != null)
                {
                    ClearKButtonOnClick(btn);
                    btn.onClick += () => ToggleCurrency(ss);
                }
                UpdateSwitchLabel(existing.gameObject, Currency.GetBuildingGO(GetSideTarget(ss)));
                return;
            }

            GameObject clone = null;

            try
            {
                clone = Util.KInstantiateUI(intercept.gameObject, parent.gameObject, true);
            }
            catch
            {
                try
                {
                    clone = UnityEngine.Object.Instantiate(intercept.gameObject);
                    clone.transform.SetParent(parent, false);
                    clone.SetActive(true);
                }
                catch { clone = null; }
            }

            if (clone == null) return;

            clone.name = SwitchBtnName;
            clone.transform.SetSiblingIndex(intercept.transform.GetSiblingIndex());

            var kbtn = clone.GetComponent<KButton>();
            if (kbtn != null)
            {
                ClearKButtonOnClick(kbtn);
                kbtn.onClick += () => ToggleCurrency(ss);
            }

            UpdateSwitchLabel(clone, Currency.GetBuildingGO(GetSideTarget(ss)));
        }

        private static void ToggleCurrency(PrinterceptorSideScreen ss)
        {
            var inst = GetSideTarget(ss);
            if (inst == null) return;

            var buildingGo = Currency.GetBuildingGO(inst);
            if (buildingGo == null) return;

            var state = Currency.GetOrAddState(buildingGo);
            if (state == null) return;

            state.Toggle();

            // sync capacity UI + backing field selon monnaie + recalcul vanilla
            CapacitySwap.AfterCurrencyToggle(inst);

            ApplySideScreen(ss);
            PortalCharges.ApplyChargesUIRobust(ss);
            RefreshPrintableMenusSafe();

            TryPatchCapacityUnitsText(ss.gameObject, state.Mode);
        }

        private static void UpdateSwitchLabel(GameObject btnGo, GameObject buildingGO)
        {
            if (btnGo == null) return;
            var txt = btnGo.GetComponentInChildren<LocText>(true);
            if (txt == null) return;

            var mode = GetMode(buildingGO);
            var cur = ModeLabel(mode);
            var next = NextModeLabel(mode);
            txt.SetText($"Switch Currency ({cur} → {next})");
        }

        // Patch minimal texte (remplace l'unité affichée si le jeu a des textes codés)
        private static void TryPatchCapacityUnitsText(GameObject root, PrinterceptorCurrencyState.CurrencyMode mode)
        {
            if (root == null) return;

            try
            {
                var locs = root.GetComponentsInChildren<LocText>(true);
                if (locs == null) return;

                for (int i = 0; i < locs.Length; i++)
                {
                    var lt = locs[i];
                    if (lt == null) continue;

                    var t = lt.text ?? "";
                    if (t.Length < 3) continue;

                    bool mentionsDb =
                        t.IndexOf("Data Bank", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (t.IndexOf("Banque", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("Donn", StringComparison.OrdinalIgnoreCase) >= 0);

                    bool mentionsPl =
                        t.IndexOf("Plastic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.IndexOf("Plastique", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool mentionsCp =
                        t.IndexOf("Gravitas", StringComparison.OrdinalIgnoreCase) >= 0 && (t.IndexOf("Coupon", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Ticket", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (mode == PrinterceptorCurrencyState.CurrencyMode.Plastic && mentionsDb)
                    {
                        lt.SetText(t.Replace("Data Banks", "Tons of Plastic").Replace("Data Bank", "Tons of Plastic")
                            .Replace("Banques de Données", "Tonnes de Plastique").Replace("Banque de Données", "Tonnes de Plastique"));
                    }
                    else if (mode == PrinterceptorCurrencyState.CurrencyMode.Coupon && (mentionsDb || mentionsPl))
                    {
                        lt.SetText(t.Replace("Data Banks", "Gravitas Coupon").Replace("Data Bank", "Gravitas Coupon")
                            .Replace("Plastic", "Gravitas Coupon").Replace("Plastique", "Gravitas Coupon")
                            .Replace("Banques de Données", "Coupon de Gravitas").Replace("Banque de Données", "Coupon de Gravitas"));
                    }
                    else if (mode == PrinterceptorCurrencyState.CurrencyMode.DataBank && (mentionsPl || mentionsCp))
                    {
                        // rollback best-effort
                        lt.SetText(t.Replace("Tons of Plastic", "Data Banks").Replace("Tonnes de Plastique", "Banques de Données")
                            .Replace("Gravitas Coupon", "Data Banks").Replace("Coupon de Gravitas", "Banques de Données"));
                    }
                }
            }
            catch { }
        }

        public static void ApplySideScreen(PrinterceptorSideScreen ss)
        {
            var inst = GetSideTarget(ss);
            if (inst == null) return;

            var buildingGo = Currency.GetBuildingGO(inst);
            if (buildingGo == null) return;

            var state = Currency.GetOrAddState(buildingGo);
            var mode = state != null ? state.Mode : PrinterceptorCurrencyState.CurrencyMode.DataBank;

            var tag = Currency.GetCurrencyTag(inst);
            var st = Currency.GetWalletStorage(inst, tag);
            if (st == null) return;

            PrinterceptorStorageFix.EnsureCurrencyStorage(st);

            // force sync backing field capacity pour l'UI "Max"
            CapacitySwap.SyncBackingField(inst);

            var intercept = SS_InterceptButton?.GetValue(ss) as KButton;
            if (intercept != null && intercept.transform.parent != null)
            {
                var tr = intercept.transform.parent.Find(SwitchBtnName);
                if (tr != null) UpdateSwitchLabel(tr.gameObject, buildingGo);
            }

            var icons = SS_DatabankIcons?.GetValue(ss) as Image[];
            if (icons != null && icons.Length > 0)
            {
                // If databank mode, restore vanilla icons; otherwise, replace.
                if (mode == PrinterceptorCurrencyState.CurrencyMode.DataBank)
                {
                    PrinterceptorUiIconCache.TryCaptureSideDefaults(ss, icons);
                    PrinterceptorUiIconCache.RestoreSideDefaults(ss, icons);

                    if (icons[0] != null && icons[0].sprite != null)
                        CurrencySpriteCache.Observe(Currency.DataBankTag, icons[0].sprite, icons[0].color);
                }
                else
                {
                    if (CurrencySpriteCache.TryGet(tag, out var sp, out var col))
                    {
                        foreach (var img in icons)
                        {
                            if (img == null) continue;
                            img.sprite = sp;
                            img.color = col;
                        }

                        if (icons[0] != null && icons[0].sprite != null)
                            CurrencySpriteCache.Observe(tag, icons[0].sprite, icons[0].color);
                    }
                }
            }

            var lbl = SS_DatabankLabel?.GetValue(ss) as LocText;
            if (lbl != null)
            {
                float raw = Currency.GetAmountAvailable(st, tag);

                if (mode == PrinterceptorCurrencyState.CurrencyMode.Plastic)
                {
                    float tons = raw / Tuning.TONS_TO_KG;
                    lbl.SetText($"{Mathf.FloorToInt(tons)} t Stored Plastic");
                }
                else if (mode == PrinterceptorCurrencyState.CurrencyMode.Coupon)
                {
                    lbl.SetText($"{Mathf.FloorToInt(raw)} Stored Gravitas Coupon");
                }
                else
                {
                    lbl.SetText($"{Mathf.FloorToInt(raw)} Stored Data Banks");
                }
            }

            TryPatchCapacityUnitsText(ss.gameObject, mode);
        }

        private static List<Image> GetWalletImages(PrinterceptorScreen ps, LocText walletLabel)
        {
            var result = new List<Image>();

            try
            {
                var obj = PS_WalletIconField?.GetValue(ps);
                if (obj is Image[] arr)
                {
                    for (int i = 0; i < arr.Length; i++) if (arr[i] != null) result.Add(arr[i]);
                }
                else if (obj is Image one)
                {
                    result.Add(one);
                }
            }
            catch { }

            if (result.Count == 0 && walletLabel != null && walletLabel.transform != null)
            {
                var parent = walletLabel.transform.parent;
                if (parent != null)
                {
                    var imgs = parent.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgs.Length; i++)
                    {
                        var img = imgs[i];
                        if (img == null) continue;
                        if (img.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        result.Add(img);
                    }
                }
            }

            return result;
        }

        private static bool SpriteMatches(Sprite a, Sprite b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;

            try
            {
                if (a.texture == b.texture && a.name == b.name && a.rect == b.rect)
                    return true;
            }
            catch { }

            return false;
        }

        private static bool SpriteNameLooksLike(Sprite sp, string token)
        {
            if (sp == null || string.IsNullOrEmpty(token)) return false;
            try
            {
                var n = (sp.name ?? "").ToLowerInvariant();
                return n.Contains(token);
            }
            catch { return false; }
        }

        // Updates every currency icon in the printable shop UI (grid tiles + footer), based on the active currency.
        private static void TryApplyCurrencyToAllIcons(GameObject root, Tag activeTag)
        {
            if (root == null) return;

            // Resolve sprites for all currencies
            Sprite dbSp = null, plSp = null, cpSp = null;
            Color dbCol = Color.white, plCol = Color.white, cpCol = Color.white;

            CurrencySpriteCache.TryGet(Currency.DataBankTag, out dbSp, out dbCol);
            CurrencySpriteCache.TryGet(Currency.PlasticTag, out plSp, out plCol);
            CurrencySpriteCache.TryGet(Currency.GravitasTicketTag, out cpSp, out cpCol);

            if (dbSp == null) Currency.TryGetUISpriteSafe(Currency.DataBankTag, out dbSp, out dbCol);
            if (plSp == null) Currency.TryGetUISpriteSafe(Currency.PlasticTag, out plSp, out plCol);
            if (cpSp == null) Currency.TryGetUISpriteSafe(Currency.GravitasTicketTag, out cpSp, out cpCol);

            Sprite activeSp = null;
            Color activeCol = Color.white;

            if (activeTag == Currency.PlasticTag) { activeSp = plSp; activeCol = plCol; }
            else if (activeTag == Currency.GravitasTicketTag) { activeSp = cpSp; activeCol = cpCol; }
            else { activeSp = dbSp; activeCol = dbCol; }

            if (activeSp == null) return;

            try
            {
                var imgs = root.GetComponentsInChildren<Image>(true);
                if (imgs == null) return;

                for (int i = 0; i < imgs.Length; i++)
                {
                    var img = imgs[i];
                    if (img == null || img.sprite == null) continue;

                    var n = img.name ?? "";
                    if (n.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    var sp = img.sprite;

                    bool isCurrencyIcon =
                        SpriteMatches(sp, dbSp) || SpriteMatches(sp, plSp) || SpriteMatches(sp, cpSp) ||
                        SpriteNameLooksLike(sp, "databank") || SpriteNameLooksLike(sp, "data_bank") ||
                        SpriteNameLooksLike(sp, "plastic") || SpriteNameLooksLike(sp, "polypropylene") ||
                        SpriteNameLooksLike(sp, "gravitas") || SpriteNameLooksLike(sp, "ticket") || SpriteNameLooksLike(sp, "coupon");

                    if (!isCurrencyIcon) continue;

                    img.sprite = activeSp;
                    img.color = activeCol;
                }
            }
            catch { }
        }

        public static void ApplyPrinterScreen(PrinterceptorScreen ps)
        {
            if (ps == null || ps.gameObject == null) return;

            var inst = GetPrinterTarget(ps);
            if (inst == null) return;

            var buildingGo = Currency.GetBuildingGO(inst);
            if (buildingGo == null) return;

            var state = Currency.GetOrAddState(buildingGo);
            var mode = state != null ? state.Mode : PrinterceptorCurrencyState.CurrencyMode.DataBank;

            var tag = Currency.GetCurrencyTag(inst);
            var st = Currency.GetWalletStorage(inst, tag);
            if (st == null) return;

            PrinterceptorStorageFix.EnsureCurrencyStorage(st);

            // sync capacity UI
            CapacitySwap.SyncBackingField(inst);

            var walletLbl = PS_WalletLabel?.GetValue(ps) as LocText;
            var costIcon = PS_CostIconField?.GetValue(ps) as Image;

            if (walletLbl != null)
            {
                float raw = Currency.GetAmountAvailable(st, tag);

                if (mode == PrinterceptorCurrencyState.CurrencyMode.Plastic)
                {
                    float tons = raw / Tuning.TONS_TO_KG;
                    walletLbl.SetText($"{Mathf.FloorToInt(tons)} t Plastic");
                }
                else if (mode == PrinterceptorCurrencyState.CurrencyMode.Coupon)
                {
                    walletLbl.SetText($"{Mathf.FloorToInt(raw)} Gravitas Coupon");
                }
                else
                {
                    walletLbl.SetText($"{Mathf.FloorToInt(raw)} Data Banks");
                }
            }

            if (CurrencySpriteCache.TryGet(tag, out var sp, out var col))
            {
                var walletImgs = GetWalletImages(ps, walletLbl);
                for (int i = 0; i < walletImgs.Count; i++)
                {
                    var img = walletImgs[i];
                    if (img == null) continue;
                    img.sprite = sp;
                    img.color = col;
                }

                if (costIcon != null)
                {
                    costIcon.sprite = sp;
                    costIcon.color = col;
                }
            }

            // Also update all currency icons in the shop UI (tiles + required + misc)
            TryApplyCurrencyToAllIcons(ps.gameObject, tag);

            var costLbl = PS_CostLabel?.GetValue(ps) as LocText;
            if (costLbl != null)
            {
                if (int.TryParse(costLbl.text, out var v))
                {
                    if (mode == PrinterceptorCurrencyState.CurrencyMode.Plastic) costLbl.SetText($"{v} t");
                    else if (mode == PrinterceptorCurrencyState.CurrencyMode.Coupon) costLbl.SetText($"{v}");
                    // databank: keep as-is
                }
            }

            TryUpdateRequiredText(ps, mode);
            TryPatchCapacityUnitsText(ps.gameObject, mode);
        }

        private static void TryUpdateRequiredText(PrinterceptorScreen ps, PrinterceptorCurrencyState.CurrencyMode mode)
        {
            var locs = ps.gameObject.GetComponentsInChildren<LocText>(true);
            foreach (var t in locs)
            {
                if (t == null) continue;
                var s = t.text;
                if (string.IsNullOrEmpty(s)) continue;

                var lower = s.ToLowerInvariant();
                bool looksLikeRequired =
                    lower.Contains("required") || lower.Contains("requis") || lower.Contains("requise") || lower.Contains("requises");

                bool mentionsDb =
                    lower.Contains("data bank") || lower.Contains("data banks") ||
                    (lower.Contains("banque") && lower.Contains("donn"));

                bool mentionsPl = lower.Contains("plastic") || lower.Contains("plastique") || lower.Contains("tonnes") || lower.Contains("tons");
                bool mentionsCp = lower.Contains("gravitas") && (lower.Contains("coupon") || lower.Contains("ticket"));

                if (!looksLikeRequired) continue;

                bool french = lower.Contains("banque") || lower.Contains("donn") || lower.Contains("requis");

                if (mode == PrinterceptorCurrencyState.CurrencyMode.Plastic && (mentionsDb || mentionsCp))
                    t.SetText(french ? "Plastique requis :" : "Plastic Required:");
                else if (mode == PrinterceptorCurrencyState.CurrencyMode.Coupon && (mentionsDb || mentionsPl))
                    t.SetText(french ? "Coupon de Gravitas requis :" : "Gravitas Coupon Required:");
                else if (mode == PrinterceptorCurrencyState.CurrencyMode.DataBank && (mentionsPl || mentionsCp))
                    t.SetText(french ? "Banques de Données requises :" : "Data Banks Required:");
            }
        }
    }


    // ============================================================
    // 7) PATCHES
    // ============================================================
    internal static class Patches
    {

        // ---- Storage Consume override (PRINT with Plastic): some Printerceptor code consumes currency as an int count.
        // For Plastic, we treat that int as kilograms and consume element mass from the Printerceptor currency Storage.
        public static bool StorageConsume_TagInt_Prefix(Storage __instance, Tag __0, int __1)
        {
            try
            {
                if (__instance == null) return true;
                if (!PrinterceptorStorageFix.IsPrinterceptorStorage(__instance)) return true;
                if (__0 != Currency.PlasticTag) return true;

                float kg = Mathf.Max(0f, __1) * Tuning.TONS_TO_KG;

                if (kg <= 0f) return false; // nothing to consume

                // Consume ourselves (kg) and skip original.
                if (Currency.TryConsumePlasticKg(__instance, kg))
                    return false;
            }
            catch { }
            // fallback to vanilla behavior if our consumption fails
            return true;
        }

        public static bool StorageConsume_TagFloat_Prefix(Storage __instance, Tag __0, float __1)
        {
            try
            {
                if (__instance == null) return true;
                if (!PrinterceptorStorageFix.IsPrinterceptorStorage(__instance)) return true;
                if (__0 != Currency.PlasticTag) return true;

                float kg = Mathf.Max(0f, __1) * Tuning.TONS_TO_KG;
                if (kg <= 0f) return false;

                if (Currency.TryConsumePlasticKg(__instance, kg))
                    return false;
            }
            catch { }
            return true;
        }

        // ---- Prefab: state + currency storage filters + 20t cap + init capacity defaults ----
        public static void HHConfig_DoPostConfigureComplete_Postfix(GameObject go)
        {
            try
            {
                if (go == null) return;

                var stt = go.AddOrGet<PrinterceptorCurrencyState>();
                if (stt != null)
                {
                    if (stt.UserMax_DataBanks <= 0f) stt.UserMax_DataBanks = Tuning.DATABANK_USERMAX_DEFAULT;
                    if (stt.UserMax_PlasticTons <= 0f) stt.UserMax_PlasticTons = Tuning.PLASTIC_USERMAX_DEFAULT_TONS;
                    if (stt.UserMax_Coupons <= 0f) stt.UserMax_Coupons = Tuning.COUPON_USERMAX_DEFAULT;
                }

                foreach (var st in go.GetComponentsInChildren<Storage>(true))
                {
                    if (!PrinterceptorStorageFix.LooksLikeCurrencyStorage(st) && !PrinterceptorStorageFix.IsPrinterceptorStorage(st))
                        continue;

                    PrinterceptorStorageFix.EnsureCurrencyStorage(st);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] DoPostConfigureComplete postfix error: {e}");
            }
        }

        // ---- IMPORTANT: prefix ApplyMaxCapacity -> sync backing field userMaxCapacity selon monnaie ----
        public static void HHInstance_ApplyMaxCapacity_Prefix(HijackedHeadquarters.Instance __instance)
        {
            try { CapacitySwap.SyncBackingField(__instance); } catch { }
        }

        // ---- Runtime: re-apply filters+cap + clamp portal charges ----
        public static void HHInstance_ApplyMaxCapacity_Postfix(HijackedHeadquarters.Instance __instance)
        {
            try
            {
                var go = Currency.GetBuildingGO(__instance);
                if (go != null)
                {
                    Currency.GetOrAddState(go);

                    // Ensure the legacy printerceptor storage also supports both currencies
                    // (Print payment may use m_storage directly depending on game internals).
                    var legacy = Currency.GetStorage(__instance);
                    if (legacy != null)
                        PrinterceptorStorageFix.EnsureCurrencyStorage(legacy);

                    var sts = go.GetComponentsInChildren<Storage>(true);
                    if (sts != null)
                    {
                        for (int i = 0; i < sts.Length; i++)
                        {
                            var s = sts[i];
                            if (s == null) continue;

                            if (!PrinterceptorStorageFix.LooksLikeCurrencyStorage(s) && !PrinterceptorStorageFix.IsPrinterceptorStorage(s))
                                continue;

                            PrinterceptorStorageFix.EnsureCurrencyStorage(s);
                        }
                    }
                }

                PortalCharges.ClampToMax(__instance);
            }
            catch { }
        }

        // ---- IUserControlledCapacity: currency-aware ----
        public static bool IUCC_get_AmountStored_Prefix(HijackedHeadquarters.Instance __instance, ref float __result)
        {
            __result = CapacitySwap.GetActiveAmountStored(__instance);
            return false;
        }

        public static bool IUCC_get_UserMaxCapacity_Prefix(HijackedHeadquarters.Instance __instance, ref float __result)
        {
            __result = CapacitySwap.GetActiveUserMax(__instance);
            return false;
        }

        public static void IUCC_set_UserMaxCapacity_Postfix(HijackedHeadquarters.Instance __instance, float __0)
        {
            try { CapacitySwap.SetActiveUserMax(__instance, __0); } catch { }
        }

        public static bool IUCC_get_MaxCapacity_Prefix(HijackedHeadquarters.Instance __instance, ref float __result)
        {
            __result = CapacitySwap.GetAbsoluteMax(__instance);
            return false;
        }

        public static void IUCC_get_CapacityUnits_Postfix(HijackedHeadquarters.Instance __instance, ref LocString __result)
        {
            __result = CapacitySwap.GetCapacityUnits(__instance, __result);
        }

        // ---- ✅ PRINT Payment fix: reroute Tag param for Storage calls (Printerceptor only) ----
        public static void Storage_RouteCurrencyTag_Prefix(Storage __instance, ref Tag __0)
        {
            try { __0 = CurrencyRouter.Route(__instance, __0); } catch { }
        }



        // Also route Tag[]-based consumption APIs (some ONI codepaths batch-consume tags).
        public static void Storage_RouteCurrencyTagArray_Prefix(Storage __instance, Tag[] __0)
        {
            try
            {
                if (__instance == null || __0 == null) return;
                for (int i = 0; i < __0.Length; i++)
                    __0[i] = CurrencyRouter.Route(__instance, __0[i]);
            }
            catch { }
        }
        // ---- Storage validity (ONLY for registered Printerceptor currency storage) ----
        public static bool Storage_IsValidForStore_GO_Prefix(Storage __instance, GameObject go, ref bool __result)
        {
            if (!PrinterceptorStorageFix.IsPrinterceptorStorage(__instance))
                return true;

            if (go == null) return true;

            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null && pe.ElementID == SimHashes.Polypropylene)
            {
                __result = true;
                return false;
            }

            return true;
        }

        public static bool Storage_IsValidForStore_Tag_Prefix(Storage __instance, Tag tag, ref bool __result)
        {
            if (!PrinterceptorStorageFix.IsPrinterceptorStorage(__instance))
                return true;

            if (tag == Currency.PlasticTag || tag == Currency.GravitasTicketTag)
            {
                __result = true;
                return false;
            }

            return true;
        }

        // ---- SideScreen UI ----
        public static void SideScreen_OnSpawn_Postfix(PrinterceptorSideScreen __instance)
        {
            try
            {
                UIX.EnsureSwitchButton(__instance);
                UIX.ApplySideScreen(__instance);
                PortalCharges.ApplyChargesUIRobust(__instance);
            }
            catch { }
        }

        public static void SideScreen_SetTarget_Postfix(PrinterceptorSideScreen __instance, GameObject __0)
        {
            try
            {
                var inst = AccessTools.Field(typeof(PrinterceptorSideScreen), "target")?.GetValue(__instance) as HijackedHeadquarters.Instance;
                if (inst != null)
                {
                    var go = Currency.GetBuildingGO(inst);
                    if (go != null)
                    {
                        Currency.GetOrAddState(go);

                        var sts = go.GetComponentsInChildren<Storage>(true);
                        if (sts != null)
                        {
                            for (int i = 0; i < sts.Length; i++)
                            {
                                var s = sts[i];
                                if (s == null) continue;

                                if (!PrinterceptorStorageFix.LooksLikeCurrencyStorage(s) && !PrinterceptorStorageFix.IsPrinterceptorStorage(s))
                                    continue;

                                PrinterceptorStorageFix.EnsureCurrencyStorage(s);
                            }
                        }
                    }

                    // sync capacity backing for UI
                    CapacitySwap.SyncBackingField(inst);
                }

                UIX.EnsureSwitchButton(__instance);
                UIX.ApplySideScreen(__instance);
                PortalCharges.ApplyChargesUIRobust(__instance);

                UIX.RefreshPrintableMenusSafe();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GravitasMod] SideScreen.SetTarget postfix error: {e}");
            }
        }

        public static void SideScreen_RefreshDisplay_Postfix(PrinterceptorSideScreen __instance)
        {
            try
            {
                UIX.EnsureSwitchButton(__instance);
                UIX.ApplySideScreen(__instance);
                PortalCharges.ApplyChargesUIRobust(__instance);
            }
            catch { }
        }

        public static void SideScreen_ScreenUpdate_Postfix(PrinterceptorSideScreen __instance)
        {
            try
            {
                UIX.EnsureSwitchButton(__instance);
                UIX.ApplySideScreen(__instance);
                PortalCharges.ApplyChargesUIRobust(__instance);
            }
            catch { }
        }

        // ---- PrinterceptorScreen UI ----
        public static void PrinterScreen_Any_Postfix(PrinterceptorScreen __instance)
        {
            try { UIX.ApplyPrinterScreen(__instance); } catch { }
        }

        public static void PrinterScreen_SetTarget_Postfix(PrinterceptorScreen __instance, GameObject __0)
        {
            try
            {
                PortalCharges.TryConsumeShopEntry(__instance);
                UIX.ApplyPrinterScreen(__instance);
            }
            catch { }
        }

        public static void PrinterScreen_SpawnOptionButton_Postfix(PrinterceptorScreen __instance)
        {
            try { UIX.ApplyPrinterScreen(__instance); } catch { }
        }

        public static void PrinterScreen_SpawnOptionButtons_Postfix(PrinterceptorScreen __instance)
        {
            try { UIX.ApplyPrinterScreen(__instance); } catch { }
        }

        public static void PrinterScreen_ResetSession_Postfix(PrinterceptorScreen __instance)
        {
            try { PortalCharges.ResetShopSession(__instance); } catch { }
        }

        // ---- Portal charges: Intercept button (robuste, dépasse 3) ----
        public static void HHInstance_Intercept_Prefix(HijackedHeadquarters.Instance __instance, ref int __state)
        {
            try { __state = PortalCharges.GetCharges(__instance); }
            catch { __state = 0; }
        }

        public static void HHInstance_Intercept_Postfix(HijackedHeadquarters.Instance __instance, int __state)
        {
            try
            {
                int desired = Mathf.Min(__state + 1, PortalCharges.MAX_CHARGES);
                int now = PortalCharges.GetCharges(__instance);

                if (now != desired)
                    PortalCharges.SetCharges(__instance, desired);

                UIX.RefreshSideScreensForTargetSafe(__instance);

                AccessTools.Method(__instance.GetType(), "UpdateMeter")?.Invoke(__instance, null);
                AccessTools.Method(__instance.GetType(), "UpdateStatusItems")?.Invoke(__instance, null);
            }
            catch { }
        }

        // ---- Print button: forcer m_storage vers le wallet actif (DataBank/Plastic/...) ----
        public static void IsReadyToPrint_SwapStorage_Prefix(HijackedHeadquarters.Instance __0, ref StorageSwapState __state)
        {
            try { __state = PrintWalletSwap.Enter(__0); }
            catch { __state = null; }
        }

        public static Exception IsReadyToPrint_SwapStorage_Finalizer(HijackedHeadquarters.Instance __0, StorageSwapState __state, Exception __exception)
        {
            try { PrintWalletSwap.Exit(__0, __state); }
            catch { }
            return __exception;
        }

        public static void PrintSelectedEntity_SwapStorage_Prefix(HijackedHeadquarters.Instance __instance, ref StorageSwapState __state)
        {
            try { __state = PrintWalletSwap.Enter(__instance); }
            catch { __state = null; }
        }

        public static Exception PrintSelectedEntity_SwapStorage_Finalizer(HijackedHeadquarters.Instance __instance, StorageSwapState __state, Exception __exception)
        {
            try { PrintWalletSwap.Exit(__instance, __state); }
            catch { }
            return __exception;
        }

        private static int GetInstArgIndex(MethodBase original)
        {
            if (original == null) return -1;
            try
            {
                if (!original.IsStatic)
                {
                    // instance method: arg0 == this
                    return 0;
                }

                var ps = original.GetParameters();
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].ParameterType == typeof(HijackedHeadquarters.Instance))
                        return i;
            }
            catch { }
            return -1;
        }

        private static CodeInstruction Ldarg(int index)
        {
            switch (index)
            {
                case 0: return new CodeInstruction(OpCodes.Ldarg_0);
                case 1: return new CodeInstruction(OpCodes.Ldarg_1);
                case 2: return new CodeInstruction(OpCodes.Ldarg_2);
                case 3: return new CodeInstruction(OpCodes.Ldarg_3);
                default: return new CodeInstruction(OpCodes.Ldarg_S, (byte)index);
            }
        }

        // ---- Gameplay transpilers (currency) ----
        public static IEnumerable<CodeInstruction> IsReadyToPrint_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = new List<CodeInstruction>(instructions);

            int instArg = GetInstArgIndex(original);
            if (instArg < 0) return code;

            var tagCtor = AccessTools.Constructor(typeof(Tag), new[] { typeof(string) });
            var getCurrency = AccessTools.Method(typeof(Currency), nameof(Currency.GetCurrencyTag));
            if (getCurrency == null) return code;

            for (int i = 0; i < code.Count; i++)
            {
                if (tagCtor != null &&
                    code[i].opcode == OpCodes.Ldstr &&
                    code[i].operand is string s &&
                    (s == "DataBank" || s == "Data Bank") &&
                    i + 1 < code.Count &&
                    code[i + 1].opcode == OpCodes.Newobj &&
                    code[i + 1].operand is ConstructorInfo ci &&
                    ci == tagCtor)
                {
                    code[i] = Ldarg(instArg);
                    code[i + 1] = new CodeInstruction(OpCodes.Call, getCurrency);
                    i++;
                    continue;
                }

                if (code[i].opcode == OpCodes.Ldsfld &&
                    code[i].operand is FieldInfo fi &&
                    fi.FieldType == typeof(Tag) &&
                    fi.Name.IndexOf("DataBank", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    code[i] = Ldarg(instArg);
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, getCurrency));
                    i++;
                }
            }

            return code;
        }

        public static IEnumerable<CodeInstruction> PrintSelectedEntity_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = new List<CodeInstruction>(instructions);

            int instArg = GetInstArgIndex(original);
            if (instArg < 0) return code;

            var tagCtor = AccessTools.Constructor(typeof(Tag), new[] { typeof(string) });
            var getCurrency = AccessTools.Method(typeof(Currency), nameof(Currency.GetCurrencyTag));
            if (getCurrency == null) return code;

            for (int i = 0; i < code.Count; i++)
            {
                if (tagCtor != null &&
                    code[i].opcode == OpCodes.Ldstr &&
                    code[i].operand is string s &&
                    (s == "DataBank" || s == "Data Bank") &&
                    i + 1 < code.Count &&
                    code[i + 1].opcode == OpCodes.Newobj &&
                    code[i + 1].operand is ConstructorInfo ci &&
                    ci == tagCtor)
                {
                    code[i] = Ldarg(instArg);
                    code[i + 1] = new CodeInstruction(OpCodes.Call, getCurrency);
                    i++;
                    continue;
                }

                if (code[i].opcode == OpCodes.Ldsfld &&
                    code[i].operand is FieldInfo fi &&
                    fi.FieldType == typeof(Tag) &&
                    fi.Name.IndexOf("DataBank", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    code[i] = Ldarg(instArg);
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, getCurrency));
                    i++;
                }
            }

            return code;
        }
    }
}