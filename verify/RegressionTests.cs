using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScavengerTeams;

internal static partial class RegressionTests
{
    private static int passed, failed;
    private static void FinishDialogue(Dialogue dialogue)
    {
        for (var current=dialogue; current!=null; current=current.nextDialogue) current.End?.Invoke();
    }
    private static string GreetingText(StoreClient client)
    {
        var lines=new List<string>();
        for (var current=client.mainDialogue; current!=null; current=current.nextDialogue) lines.Add(current.Text);
        return string.Join("\n",lines);
    }
    private static readonly ModEntry mod = new ModEntry();
    private static void Require(bool value, string reason)
    { if (!value) throw new Exception(reason); }
    private static void Check(string name, Action action)
    {
        Reset();
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    private static void Reset()
    {
        ModHelper.Locale = "zh";
        PlayerStore.Instance = new PlayerStore();
        EmporiumEntry.Instance = new EmporiumEntry();
        StoreStation.Day = 10;
        GameMaster.current.Combat = false;
        DirectoryMaster.FailId = null;
        DirectoryMaster.FailAfter = -1;
        DeliveryCoordinator.ClearRuntime();
        DialogUIManager.Instance.Last = null;
    }
    private static void BuyUpgrade(int level, TeamDefinition team = null)
    {
        var id = (team ?? TeamDefinitions.Printer).UpgradeId(level);
        var definition = ModHelper.Upgrades[id];
        Require(definition.Prerequisites == null || definition.Prerequisites.All(PlayerStore.Instance.Unlocked.Contains), "missing prerequisite");
        Require(PlayerStore.Instance.Unlocked.Add(id), "duplicate purchase");
        PlayerStore.Instance.playerCash -= definition.Cost;
        definition.Unlock();
    }
    private static StoreClient Arrive(int day)
    {
        StoreStation.Day = day;
        var manager = new StoreClientManager();
        ModHook.Generate(manager);
        Require(manager.clientStack.Count == 1, "expected exactly one supplier");
        var client = manager.clientStack[0];
        PlayerStore.Instance.currentClientInstance = new StoreClientInstance(client);
        client.OnArrival();
        FinishDialogue(client.mainDialogue);
        return client;
    }
    private static void Reload()
    {
        // Model a scene reconstruction from the stored values; never read/write real saves.
        var before = PlayerStore.Instance;
        PlayerStore.Instance = new PlayerStore { Data = new Dictionary<string, string>(before.Data),
            Unlocked = new HashSet<string>(before.Unlocked), playerCash = before.playerCash };
        EmporiumEntry.Instance = new EmporiumEntry();
        ModHook.Loaded();
    }
    private static GameItem PlaceCrate(bool owned = true)
    {
        var crate = CrateItems.Create();
        GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, crate);
        crate.Owned = owned;
        return crate;
    }
    private static List<GameItem> Stock => EmporiumEntry.Instance.frontInvinvElement.Items;
    private static void Purchase(StoreClient client, GameItem item)
    {
        client.onItemSold(item, (int)item.unitValue);
        item.Owned = true;
    }
    private static bool Guard(out object[] args)
    {
        args = new object[] { false };
        return (bool)typeof(WholesaleGuard).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
    private static void ReleaseGuard(object[] args)
        => typeof(WholesaleGuard).GetMethod("Finalizer", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { null, args[0] });

    private static void Main()
    {
        LoadLocalization();
        mod.Init(new ModManifest());
        PlayerStore.Ready = false;
        try
        {
            mod.OnEnable();
            Require(ModHelper.Upgrades.Count == 0, "services registered before a run is ready");
            Console.WriteLine("PASS boot does not touch the lazy PlayerStore singleton");
            passed++;
        }
        catch (Exception error)
        {
            Console.WriteLine("FAIL boot lifecycle regression: " + error.Message);
            Environment.ExitCode = 1; return;
        }
        finally { PlayerStore.Ready = true; }
        ModHook.Loaded();
        ModHook.RegisterItems(new ModItemDirectory());
        Check("supplier services share one scavenger category on the native basic panel without a new tab", () =>
        {
            var definitions = ModHelper.Upgrades.Values.ToArray();
            Require(definitions.Length == 15 && definitions.All(d => string.IsNullOrEmpty(d.Tab)), "wrong service count or new tab created");
            Require(definitions.All(d => d.Subtitle == "拾荒者"), "wrong category");
        });
        Check("three native upgrade definitions: exact costs and chained prerequisites", () =>
        {
            Require(ModHelper.Upgrades.Keys.Count(k => k.StartsWith(ModEntry.Id + ".printer.")) == 3, "wrong printer route count");
            BuyUpgrade(1); BuyUpgrade(2); BuyUpgrade(3);
            Require(PlayerStore.Instance.playerCash == 9050, "wrong total fee");
            Require(TeamStateRepository.Read().nextDay == 11, "upgrade reset date");
        });
        Check("next-day first delivery then 3-day cadence without random-client replacement", () =>
        {
            BuyUpgrade(1);
            var manager = new StoreClientManager();
            var random = new StoreClient { identifier = "existing-random" };
            manager.AddClient(random); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 1, "same-day delivery");
            var client = Arrive(11); client.OnDismiss();
            for (var day = 12; day <= 13; day++)
            {
                StoreStation.Day = day; manager = new StoreClientManager(); manager.AddClient(random);
                PlayerStore.Instance.currentClientInstance = null; ModHook.Generate(manager);
                Require(manager.clientStack.SequenceEqual(new[] { random }), "random client displaced");
            }
            EmporiumEntry.Instance = new EmporiumEntry(); Arrive(14);
            Require(TeamStateRepository.Read().nextDay == 17, "wrong cadence");
        });
        Check("repeated generation preserves existing clients and appends only once", () =>
        {
            BuyUpgrade(1); StoreStation.Day = 11;
            var manager = new StoreClientManager(); var existing = new StoreClient(); manager.AddClient(existing);
            ModHook.Generate(manager); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 2 && manager.clientStack[0] == existing, "duplicate or replacement");
        });
        Check("upgrade before arrival changes cargo only; first offer remains 50 percent", () =>
        {
            BuyUpgrade(1); BuyUpgrade(2); BuyUpgrade(3);
            var client = Arrive(11);
            Require(Stock.Count(i => i.identifier == CrateItems.CrateId) == 3, "wrong crates");
            Require(Stock.Count(i => i.identifier == "junk") == 6, "wrong junk");
            Require(client.wholesaleDiscount == -50, "lost first discount");
        });
        Check("upgrade after first arrival resets neither schedule nor first-offer entitlement", () =>
        {
            BuyUpgrade(1); var first = Arrive(11); first.OnDismiss();
            BuyUpgrade(2);
            Require(TeamStateRepository.Read().nextDay == 14, "date reset");
            PlayerStore.Instance.currentClientInstance = null;
            EmporiumEntry.Instance = new EmporiumEntry(); var next = Arrive(14);
            Require(next.wholesaleDiscount == -15, "upgrade granted a second introductory discount");
            Require(Stock.Count(i => i.identifier == CrateItems.CrateId) == 2 && Stock.Count == 6, "wrong level2 stock");
        });
        Check("level1 exact stock and normal retail values", () =>
        {
            BuyUpgrade(1); Arrive(11);
            Require(Stock.Count == 8 && Stock.Count(i => i.identifier == "printer_plastic") == 6, "wrong level1 stock");
            Require(Stock.Where(i => i.identifier == "printer_plastic").All(i => i.unitValue == 12), "first offer discounted retail");
        });
        Check("upgrade during the first trade keeps current cargo and discount; next trade uses the upgrade", () =>
        {
            BuyUpgrade(1); var client = Arrive(11); var before = Stock.ToArray(); BuyUpgrade(2);
            client.OnArrival(); FinishDialogue(client.mainDialogue);
            Require(Stock.SequenceEqual(before) && client.wholesaleDiscount == -50, "active offer changed during upgrade");
            client.OnDismiss(); PlayerStore.Instance.currentClientInstance = null;
            EmporiumEntry.Instance = new EmporiumEntry(); client = Arrive(14);
            Require(client.wholesaleDiscount == -15 && Stock.Count(i => i.identifier == CrateItems.CrateId) == 2,
                "upgrade renewed first offer or failed to change next cargo");
        });
        Check("rejecting the first shipment does not renew the special offer after upgrade and reload", () =>
        {
            BuyUpgrade(1); Arrive(11).OnDismiss(); BuyUpgrade(2); BuyUpgrade(3); Reload();
            var manager = new StoreClientManager(); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 0, "same-day repeat after rejection");
            var client = Arrive(14);
            Require(client.wholesaleDiscount == -15, "reject/reload/upgrade renewed special offer");
        });
        Check("partial purchase and JSON reload restore only remaining stock and same discount", () =>
        {
            BuyUpgrade(1); var client = Arrive(11);
            Purchase(client, Stock[0]); Purchase(client, Stock[6]);
            var mask = TeamStateRepository.Read().soldMask;
            Reload(); client = Arrive(11);
            Require(Stock.Count == 6 && Stock.Count(i => i.identifier == "printer_plastic") == 5, "sold goods revived");
            Require(TeamStateRepository.Read().soldMask == mask && client.wholesaleDiscount == -50, "batch changed on reload");
            FinishDialogue(client.mainDialogue); Require(Stock.Count == 6, "duplicate introduction");
        });
        Check("all stock sold closes batch; dismissing first visit consumes first discount", () =>
        {
            BuyUpgrade(1); var client = Arrive(11);
            foreach (var item in Stock.ToArray()) Purchase(client, item);
            Require(TeamStateRepository.Read().closed, "completed offer left open");
            Reload(); var manager = new StoreClientManager(); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 0, "completed batch regenerated");
            client = Arrive(14); Require(client.wholesaleDiscount == -15, "renewed first discount");
        });
        Check("skipped opening days produce one delayed batch and new 3-day anchor", () =>
        {
            BuyUpgrade(1); var client = Arrive(25);
            Require(TeamStateRepository.Read().nextDay == 28 && Stock.Count == 8, "catch-up spam or wrong anchor");
        });
        Check("materials are placed before junk; overflow junk is discarded without rollback or retry", () =>
        {
            BuyUpgrade(1); EmporiumEntry.Instance.frontInvinvElement.Capacity = 24;
            var client = Arrive(11);
            Require(Stock.Count == 6 && Stock.All(i => i.identifier == "printer_plastic"), "junk displaced material");
            Require(client.isIntroduced && DialogUIManager.Instance.Last == null, "retry dialogue remains");
            Require(TeamStateRepository.Read().soldMask == ((1 << 6) | (1 << 7)), "discarded junk not tracked");
        });
        Check("both level2 crates survive limited capacity and discarded junk stays gone after reload", () =>
        {
            BuyUpgrade(1); BuyUpgrade(2); EmporiumEntry.Instance.frontInvinvElement.Capacity = 19;
            Arrive(11);
            Require(Stock.Count(i => i.identifier == CrateItems.CrateId) == 2 && Stock.Count == 3, "crates not placed first");
            Reload(); Arrive(11);
            Require(Stock.Count(i => i.identifier == CrateItems.CrateId) == 2 && Stock.Count == 3, "discarded junk resurrected");
        });
        Check("a completely full counter follows native destruction and does not recreate the batch", () =>
        {
            BuyUpgrade(1); EmporiumEntry.Instance.frontInvinvElement.Capacity = 0;
            var client = Arrive(11);
            Require(Stock.Count == 0 && client.isIntroduced && TeamStateRepository.Read().closed, "full-counter policy differs from native");
            Reload(); var manager = new StoreClientManager(); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 0, "discarded batch recreated");
        });
        Check("crate is 3x3, six times base value, inherits future material tags and categories", () =>
        {
            var crate = PlaceCrate();
            Require(crate.shape.width == 3 && crate.shape.height == 3 && crate.unitValue == 72, "wrong size/value");
            Require(crate.IsTag("PRINTER_MATERIAL_TAG") && crate.IsTag("FUTURE_MATERIAL_TAG"), "lost source tags");
            Require(crate.itemTypes.Contains("BASIC_MATERIAL"), "lost categories");
            crate.SetShape(new GridShapeBuilder(1, 1)); crate.onLoaded(crate);
            Require(crate.shape.width == 3 && crate.shape.height == 3, "load changed footprint");
        });
        Check("crate occupies all nine cells at creation and repairs empty legacy masks on load", () =>
        {
            var crate = CrateItems.Create();
            Action assertSolid = () =>
            {
                for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
                    Require(crate.shape.GetLocal(x, y) == 1, "3x3 bounds contain an unoccupied cell");
            };
            assertSolid();
            crate.SetShape(new GridShapeBuilder(3, 3));
            crate.shortDescription = "old saved description";
            crate.onLoaded(crate);
            assertSolid();
            Require(crate.shortDescription.Contains("打开前请确保有足够的仓库空间"), "legacy tooltip not updated");
        });
        Check("double activation produces exactly six owned original materials once", () =>
        {
            var crate = PlaceCrate();
            UnpackController.Open(crate); UnpackController.Open(crate);
            var contents = EmporiumEntry.Instance.invElement.Items;
            Require(contents.Count == 6 && contents.All(i => i.identifier == "printer_plastic" && i.Owned), "duplicate or wrong contents");
            Require(crate.unitCount == 0 && contents.All(i => i.mayStackItemFunc(i, i)), "crate survived or stacking not restored");
        });
        Check("unowned crate and combat use cannot yield free material", () =>
        {
            var crate = PlaceCrate(false); UnpackController.Open(crate);
            Require(crate.unitCount == 1 && EmporiumEntry.Instance.invElement.Items.Count == 1, "unowned crate opened");
            crate.Owned = true; GameMaster.current.Combat = true; UnpackController.Open(crate);
            Require(crate.unitCount == 1, "combat activation");
        });
        Check("capacity failure rolls staged materials back and preserves existing goods and crate", () =>
        {
            var crate = PlaceCrate(); var inv = EmporiumEntry.Instance.invElement;
            inv.Capacity = 17; // Room for two spools, then failure on the third.
            EmporiumEntry.Instance.backInvinvElement.Capacity = 0;
            UnpackController.Open(crate);
            Require(inv.Items.Count == 1 && inv.Items[0] == crate && crate.unitCount == 1, "lost crate or leaked spools");
        });
        Check("factory exception mid-unpack rolls back and permits a successful retry", () =>
        {
            var crate = PlaceCrate(); DirectoryMaster.FailId = "printer_plastic"; DirectoryMaster.FailAfter = 3;
            UnpackController.Open(crate);
            Require(crate.unitCount == 1 && EmporiumEntry.Instance.invElement.Items.Count == 1, "failed factory leaked output");
            DirectoryMaster.FailId = null; UnpackController.Open(crate);
            Require(EmporiumEntry.Instance.invElement.Items.Count == 6, "busy lock remained after failure");
        });
        Check("placement exception mid-unpack preserves the unopened crate", () =>
        {
            var crate = PlaceCrate(); EmporiumEntry.Instance.invElement.ThrowOnPlace = true;
            UnpackController.Open(crate);
            Require(crate.unitCount == 1 && EmporiumEntry.Instance.invElement.Items.Count == 1, "exception consumed crate");
        });
        Check("new save slot inherits neither contract nor first offer; prior slot retains progress", () =>
        {
            BuyUpgrade(1); Arrive(11).OnDismiss(); var firstSlot = PlayerStore.Instance;
            PlayerStore.Instance = new PlayerStore();
            Require(TeamStateRepository.Level == 0 && TeamStateRepository.Read().offerDay == -1, "cross-slot state leak");
            PlayerStore.Instance = firstSlot;
            Require(TeamStateRepository.Read().offerDay == 11, "old slot lost state");
        });
        Check("invalid persisted state blocks queue without clearing saved evidence", () =>
        {
            BuyUpgrade(1); var key = PlayerStore.Instance.Data.Keys.Single();
            PlayerStore.Instance.Data[key] = "{\"version\":99}";
            var manager = new StoreClientManager(); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 0 && PlayerStore.Instance.Data[key].Contains("99"), "invalid state silently reset");
        });
        Check("wholesale invocation guard rejects insufficient funds and repeated entry", () =>
        {
            BuyUpgrade(1); Arrive(11);
            PlayerStore.Instance.playerCash = 0;
            Require(!Guard(out var rejected), "insufficient funds accepted"); ReleaseGuard(rejected);
            PlayerStore.Instance.playerCash = 1000;
            Require(Guard(out var accepted), "valid wholesale rejected");
            Require(!Guard(out var duplicate), "reentrant wholesale accepted"); ReleaseGuard(duplicate);
            ReleaseGuard(accepted);
            Require(Guard(out accepted), "guard remained locked"); ReleaseGuard(accepted);
        });
        Check("wholesale guard leaves unrelated native customers alone", () =>
        {
            PlayerStore.Instance.currentClientInstance = new StoreClientInstance(new StoreClient());
            PlayerStore.Instance.playerCash = 0;
            Require(Guard(out var state), "other customer intercepted"); ReleaseGuard(state);
        });
        RunAdditionalRoutes();
        RunLocalizationTests();
        RunBottleTests();
        RunPhoneTests();
        Console.WriteLine($"RESULT {passed} passed, {failed} failed (host doubles; no Unity runtime)");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
