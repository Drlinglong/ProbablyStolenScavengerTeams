// Deliberately small host doubles, NOT the Unity engine or proof of in-game compatibility.
// GraphUtils.TryAcceptAll matches the real API's unusual convention: 0 means no remainder.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public static class JsonUtility
    {
        public static string ToJson(object value) => new JavaScriptSerializer().Serialize(value);
        public static T FromJson<T>(string value) => new JavaScriptSerializer().Deserialize<T>(value);
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
}
public interface IMod { void Init(ModManifest manifest); void OnEnable(); void OnDisable(); }
public class ModManifest { }
public static class RenderHandler { public static object LoadFromAtlas(string atlas, string sprite) => atlas + "/" + sprite; }
public class ModLog
{
    public ModLog(ModManifest manifest) { }
    public void Log(string value) { }
    public void Warning(string value) { }
    public void Error(string value) { }
}
public static class ModHook
{
    public static event Action<ModItemDirectory> OnModItemDirectoryInit;
    public static event Action<StoreClientManager> OnGenerateCustomerVeryLate;
    public static event Action OnGameLoadedLate;
    public static event Action OnGameLoadedEarly;
    public static event Action<string> OnNewGameStarted;
    public static void RegisterItems(ModItemDirectory directory) => OnModItemDirectoryInit?.Invoke(directory);
    public static void Generate(StoreClientManager manager) => OnGenerateCustomerVeryLate?.Invoke(manager);
    public static void Loaded() { OnGameLoadedEarly?.Invoke(); OnGameLoadedLate?.Invoke(); }
    public static void Started() => OnNewGameStarted?.Invoke("test");
}
public class UpgradeDefinition
{
    public int Cost;
    public string[] Prerequisites;
    public Action Unlock;
    public string Tab, Subtitle;
}
public static class ModHelper
{
    public static string Locale = "zh";
    public static readonly Dictionary<string, Dictionary<string, string>> Locales = new Dictionary<string, Dictionary<string, string>>();
    public static string GetLocalized(string modId, string key, params object[] args)
    {
        string value;
        if (!(Locales.TryGetValue(Locale, out var table) && table.TryGetValue(key, out value))
            && !(Locales.TryGetValue("en", out table) && table.TryGetValue(key, out value))) return key;
        return args.Length == 0 ? value : string.Format(value, args);
    }
    public static readonly Dictionary<string, UpgradeDefinition> Upgrades = new Dictionary<string, UpgradeDefinition>();
    public static readonly Dictionary<string, Func<string>> Tabs = new Dictionary<string, Func<string>>();
    public static void SetNetworkTabLabel(string id, Func<string> label) => Tabs[id] = label;
    public static void AddNetworkUpgrade(string modId, string id, int cost, string subtitle,
        Func<string> title, Func<string> description, Action onUnlock, string tab = null,
        string iconKey = null, string[] prerequisites = null, int cooldownDays = 0,
        Func<string> subtitleLabel = null, Func<string> alreadyBought = null, bool recommended = false)
    {
        Upgrades[id] = new UpgradeDefinition { Cost = cost, Prerequisites = prerequisites,
            Unlock = onUnlock, Tab = tab, Subtitle = subtitleLabel == null ? subtitle : subtitleLabel() };
        // Real RegisterModUpgrade stores its definition, THEN dereferences the lazy singleton.
        var player = PlayerStore.Instance;
    }
    public static void RemoveNetworkUpgrades(string id)
    { foreach (var key in Upgrades.Keys.Where(k => k.StartsWith(id + ".")).ToArray()) Upgrades.Remove(key); }
    public static bool GetModDataBool(string id, string key)
        => bool.TryParse(GetModData(id, key), out var result) && result;
    public static void SetModDataBool(string id, string key, bool value) => SetModData(id, key, value.ToString());
    public static void AddCredit(int value, bool playSound = true) => PlayerStore.Instance.playerCash += value;
    public static bool IsNetworkUpgradeUnlocked(string id) => PlayerStore.Instance.Unlocked.Contains(id);
    public static string GetModData(string id, string key)
        => PlayerStore.Instance.Data.TryGetValue(id + key, out var value) ? value : null;
    public static void SetModData(string id, string key, string value) => PlayerStore.Instance.Data[id + key] = value;
    public static void SetSource(GameItem item, string id, string name) { item.EnableTag("MOD_ID_TAG"); }
    public static void SetClientSource(StoreClient client, string id) { }
}
public class ModItemDirectory
{
    public void Add(string id, Func<GameItem> factory) => DirectoryMaster.Factories[id] = factory;
}
public static class DirectoryMaster
{
    public static readonly Dictionary<string, Func<GameItem>> Factories = new Dictionary<string, Func<GameItem>>();
    public static string FailId;
    public static int FailAfter = -1;
    public static GameItem Item(string id)
    {
        if (id == FailId && FailAfter-- == 0) throw new Exception("injected factory failure");
        if (Factories.TryGetValue(id, out var factory)) return factory();
        var value = id == "printer_plastic" ? 12 : id == "scrap_metal" ? 10
            : id == "common_electronic" ? 33 : id == "common_ore" ? 15 : id == "wine_bottle" ? 12 : 1;
        var item = new GameItemElement { identifier = id, unitValue = value };
        item.SetShape(new GridShapeBuilder(id == "printer_plastic" ? 2 : 1, id == "printer_plastic" ? 2 : 1));
        if (id == "wine_bottle")
        {
            item.SetShape(new GridShapeBuilder(1,4).SetDataFill(1,4));
            item.EnableTag("WINE_CONTAINER_TAG");
            item.ModifyTag("LIQUID_CONTAINER_CAPACITY", t=>t.Enable().SetInt(750));
            item.itemTypes.Add("HOUSEHOLD_GOOD");
        }
        if (id == "printer_plastic")
        {
            item.EnableTag("PRINTER_MATERIAL_TAG");
            item.EnableTag("FUTURE_MATERIAL_TAG"); // Ensure inheritance is not a hardcoded list.
            item.itemTypes.AddRange(new[] { "MATERIAL", "BASIC_MATERIAL" });
        }
        if (id == "scrap_metal" || id == "common_electronic" || id == "common_ore")
        {
            item.EnableTag("SOURCE_TAG_" + id);
            item.EnableTag(id == "scrap_metal" ? "SCRAP_METAL" : id == "common_ore" ? "ORE_TAG" : "MATERIAL_TAG");
            item.itemTypes.Add("MATERIAL");
            item.mayThisTargetItemFunc = (a, b) => true;
            item.onThisTargetItemFunc = (a, b) => a.Destroy();
        }
        return item;
    }
}
public static class ItemSpawner { public static GameItem Spawn(string id) => DirectoryMaster.Item(id); }
public static class ItemDirectory { public static GameItem CreateEmptyItem() => new GameItemElement(); }
public interface GraphNodeStorage { }
public class GameInventory : GraphNodeStorage
{
    public int Capacity = 1000;
    public bool ThrowOnPlace;
    public List<GameItem> Items = new List<GameItem>();
    public int Used => Items.Sum(i => i.shape.width * i.shape.height);
}
public class SlotMarker { }
public class GridShapeBuilder
{
    public int width, height;
    private byte[] data;
    public GridShapeBuilder(int w, int h) { width = w; height = h; data = new byte[w * h]; }
    public GridShapeBuilder(GridShapeBuilder other)
    { width = other.width; height = other.height; data = (byte[])other.data.Clone(); }
    public GridShapeBuilder SetDataFill(int w, int h, byte value = 1)
    { width = w; height = h; data = Enumerable.Repeat(value, w * h).ToArray(); return this; }
    public byte GetLocal(int x, int y) => data[y * width + x];
}
public class TagState
{
    public int Value;
    public TagState Enable() => this;
    public TagState SetInt(int value) { Value = value; return this; }
    public int GetInt() => Value;
}
public class GameItem
{
    public string identifier, name, shortDescription, flavorText;
    public long unitValue;
    public int unitCount = 1;
    public GameInventory parentInventory;
    public GridShapeBuilder shape = new GridShapeBuilder(1, 1);
    public bool Owned = true;
    public List<string> itemTypes = new List<string>();
    public Dictionary<string, TagState> Tags = new Dictionary<string, TagState>();
    public Func<GameItem, GameItem, bool> mayStackItemFunc = (a, b) => true;
    public Func<GameItem, GameInventory, SlotMarker, bool> mayActivateSlotItemFunc, canActivateSlotItemFunc;
    public Func<GameItem, GameInventory, SlotMarker, bool> maySelectSlotItemFunc, canSelectSlotItemFunc;
    public Func<GameItem, GameItem, bool> mayThisTargetItemFunc, canThisTargetItemFunc;
    public Action<GameItem, GameItem> onThisTargetItemFunc;
    public Action<GameItem, GameInventory, SlotMarker> onActivateSlotItemFunc;
    public Action<GameItem> onLoaded;
    public void EnableTag(string tag) { Tags[tag] = new TagState(); }
    public bool IsTag(string tag) => Tags.ContainsKey(tag);
    public TagState GetTagReadonly(string tag) => Tags[tag];
    public void ModifyTag(string tag, Action<TagState> action)
    { if (!Tags.ContainsKey(tag)) EnableTag(tag); action(Tags[tag]); }
    public void SetShape(GridShapeBuilder value) { shape = value; }
    public void SetSpriteAndShapeFromMod(string id, string key) { }
    public virtual void Destroy() { parentInventory?.Items.Remove(this); parentInventory = null; unitCount = 0; }
}
public class GameItemElement : GameItem
{
    public bool IsDestroyed() => unitCount == 0;
}
public static class GeneralHelper
{
    public static bool IsItemEffectivelyOwned(GameItem item) => item.Owned;
    public static void SetItemOwned(GameItem item, bool owned = true) { item.Owned = owned; }
}
public static class GraphUtils
{
    public static int TryAcceptAll(GraphNodeStorage node, GameItem item)
    {
        var inv = (GameInventory)node;
        if (inv.ThrowOnPlace) throw new Exception("injected placement failure");
        if (inv.Used + item.shape.width * item.shape.height > inv.Capacity) return item.unitCount;
        item.parentInventory?.Items.Remove(item);
        inv.Items.Add(item);
        item.parentInventory = inv;
        return 0;
    }
}
public class EmporiumEntry
{
    public static EmporiumEntry Instance = new EmporiumEntry();
    public GameInventory invElement = new GameInventory(), backInvinvElement = new GameInventory(), frontInvinvElement = new GameInventory();
}
public class GameMaster
{
    public static GameMaster current = new GameMaster();
    public bool Combat;
    public bool InCombat() => Combat;
}
public class StoreStation
{
    public static StoreStation Instance = new StoreStation();
    public static int Day = 10;
    public static int GetDayCounter() => Day;
}
public class StoreClient
{
    public enum ClientIntent { SELL }
    public string identifier, displayName, spriteName, clientFaction;
    public ClientIntent clientIntent;
    public bool dismissable, arrestable, nonShootable, CanReturn, CanReturnAsBuyer, isIntroduced, isOfferWholesale;
    public int wholesaleDiscount;
    public Action OnArrival, OnDismiss;
    public Action<GameItem, int> onItemSold;
    public Dialogue mainDialogue = new Dialogue();
    public void AddBasicDialog() { }
    public void CompleteClientCreation() { }
    public void SetWholesale(int value) { wholesaleDiscount = value; isOfferWholesale = true; }
    public void OnIntroduced() { isIntroduced = true; }
}
public class StoreClientManager
{
    public List<StoreClient> clientStack = new List<StoreClient>();
    public void AddClient(StoreClient client) => clientStack.Add(client);
}
public class StoreClientInstance
{
    private StoreClient client;
    public StoreClientInstance(StoreClient value) { client = value; }
    public StoreClient GetClientBlueprint() => client;
}
public class Dialogue
{
    public Action End, Choice;
    public string Text;
    public Dialogue nextDialogue;
    public Dialogue NextDialogue() => nextDialogue ?? (nextDialogue = new Dialogue());
    public Dialogue SetText(string name, string text) { Text = text; return this; }
    public Dialogue SetEndAction(Action action) { End = action; return this; }
    public Dialogue AddChoice(GameItem item, string name, string description, Action choiceAction)
    { Choice = choiceAction; return this; }
}
public class DialogUIManager
{
    public static DialogUIManager Instance = new DialogUIManager();
    public Dialogue Last;
    public void StartDialogue(Dialogue dialogue) { Last = dialogue; }
}
public class SpriteDictionaryState { public Dictionary<string,object> spriteDictionary = new Dictionary<string,object>(); }
public static class SpriteDict
{
    public static SpriteDictionaryState Instance = new SpriteDictionaryState();
    public static string GetRandomScavSpriteName() => "scav-test-double";
    public static string GetRandomMinerSpriteName() => "miner-test-double";
}
public class PlayerStore
{
    public static bool Ready = true;
    private static PlayerStore instance = new PlayerStore();
    public static PlayerStore Instance
    {
        get { if (!Ready) throw new NullReferenceException("PlayerStore constructor dependencies not ready at boot"); return instance; }
        set { instance = value; }
    }
    public int playerCash = 10000;
    public StoreClientInstance currentClientInstance;
    public Dictionary<string, string> Data = new Dictionary<string, string>();
    public HashSet<string> Unlocked = new HashSet<string>();
    public bool TryAddDirectSellingItemToTable(GameItem item)
    {
        item.Owned = false;
        if (GraphUtils.TryAcceptAll(EmporiumEntry.Instance.frontInvinvElement, item) == 0) return true;
        item.Destroy(); // Mirrors native TryAddDirectSellingItemToTable's failure cleanup.
        return false;
    }
    public int GetWholesalePrice()
    {
        var remaining = EmporiumEntry.Instance.frontInvinvElement.Items.Where(i => !i.Owned).Sum(i => i.unitValue);
        return (int)Math.Round(remaining * (100 + currentClientInstance.GetClientBlueprint().wholesaleDiscount) / 100.0);
    }
}
public class NegociationUIManager { public void OnWholesaleClick() { } }
