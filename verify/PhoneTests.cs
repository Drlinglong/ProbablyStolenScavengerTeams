using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScavengerTeams;

internal static partial class RegressionTests
{
    private static Dialogue Call() => TeamPhone.Build(null);
    private static void SelectPhone(Dialogue menu, int index)
    {
        Require(menu.IsChoiceEnabledAt(index), "disabled phone option");
        var choice = menu.choices[index];
        choice.Item3?.Invoke();
        // Native phone selection invokes Item3, then ContinueConversation advances past the label.
        Require(choice.Item2.Text == choice.Item1.name, "native phone label differs from option name");
        Require(choice.Item2.nextDialogue != null, "phone selection would skip the response");
        choice.Item2.nextDialogue.End?.Invoke();
    }
    private static void RunPhoneTests()
    {
        Check("fixed-width single-column phone menu disables native fitters and restores original slots", () =>
        {
            var parent=new UnityEngine.RectTransform();
            var group=new UnityEngine.RectTransform {parent=parent};
            var ui=new DialoguePhoneUIManager();
            parent.Canvas=ui.Canvas;
            Require(ui.GetComponentInParent<UnityEngine.Canvas>()==null,"test must model the manager outside Canvas");
            for(var i=0;i<3;i++)
            {
                var bubble=new DialogChoiceBubble {index=i,type=1};
                bubble.transform.parent=group;
                ((UnityEngine.RectTransform)bubble.transform).anchoredPosition=new UnityEngine.Vector2(0,-22.5f-i*40);
                ui.dialogChoiceBubbles.Add(bubble);
            }
            var original=ui.dialogChoiceBubbles;
            try
            {
                TeamPhoneLayout.Prepare(ui,true);
                Require(ui.dialogChoiceBubbles.Count==6 && !ReferenceEquals(ui.dialogChoiceBubbles,original),"not six separate slots");
                for(var i=0;i<6;i++)
                {
                    var bubble=ui.dialogChoiceBubbles[i];var rect=(UnityEngine.RectTransform)bubble.transform;
                    Require(rect.sizeDelta.x==520 && rect.anchoredPosition.x==0,"not fixed-width single column");
                    Require(!bubble.Fitter.enabled && !bubble.Layout.enabled,"native auto-width remains active");
                    Require(bubble.TMP.enableWordWrapping && bubble.TMP.enableAutoSizing,"long locale text cannot fit");
                    if(i>0) Require(-rect.anchoredPosition.y>=i*68,"rows overlap");
                }
                var last=ui.dialogChoiceBubbles[5];
                Require(last.index==5 && last.type==1,"wrong cloned click target");
                TeamPhoneLayout.Prepare(ui,true);Require(ui.dialogChoiceBubbles.Count==6,"duplicated clones");
                foreach(var viewport in new[]{new UnityEngine.Vector2(640,480),new UnityEngine.Vector2(1280,720),new UnityEngine.Vector2(1920,1080)})
                {
                    ((UnityEngine.RectTransform)ui.Canvas.transform).rect=new UnityEngine.Rect {width=viewport.x,height=viewport.y};
                    TeamPhoneLayout.Prepare(ui,true);
                    var panel=(UnityEngine.RectTransform)ui.dialogChoiceBubbles[0].transform.parent;
                    Require(panel.anchoredPosition.x<=viewport.x/2-20 && panel.anchoredPosition.x-panel.sizeDelta.x>=-viewport.x/2+20,"menu outside horizontal screen bounds");
                    Require(panel.anchoredPosition.y<=viewport.y/2-20 && panel.anchoredPosition.y-panel.sizeDelta.y>=-viewport.y/2+20,"menu outside vertical screen bounds");
                }
                TeamPhoneLayout.Prepare(ui,false);
                Require(ReferenceEquals(ui.dialogChoiceBubbles,original),"native list not restored");
                Require(original[0].Fitter.enabled && original[0].Layout.enabled,"modified native controls");
                parent.Canvas=null;
                TeamPhoneLayout.Prepare(ui,true);
                Require(ui.dialogChoiceBubbles.Count==6,"missing Canvas should retain all six choices");
                Require(((UnityEngine.RectTransform)ui.dialogChoiceBubbles[0].transform).sizeDelta.x==520,"phone-local fallback missing");
                TeamPhoneLayout.Prepare(ui,true);
            }
            finally {TeamPhoneLayout.Remove();}
            Require(ReferenceEquals(ui.dialogChoiceBubbles,original) && original.Count==3,"cleanup lost original slots");
        });
        Check("independent contact registers once after load and leaves native contacts alone", () =>
        {
            var native = new StorePhoneClient { dialogFuncId="Wild" };
            PlayerStore.Instance.PhoneClientDict[8376] = native;
            ModHook.Loaded(); ModHook.Loaded();
            Require(PlayerStore.Instance.PhoneClientDict.Count==2, "duplicated contact");
            Require(ReferenceEquals(PlayerStore.Instance.PhoneClientDict[8376],native), "changed Wild");
            var contact=PlayerStore.Instance.PhoneClientDict[TeamPhone.Number];
            Require(contact.dialedBefore && contact.phoneState==StorePhoneClient.PhoneState.Regular, "not in phonebook");
            Require(contact.GetCallDialog().choices.Count==6, "must have five teams and hangup");
        });
        Check("phone number collision preserves the existing owner", () =>
        {
            var other=new StorePhoneClient { dialogFuncId="other-mod" };
            PlayerStore.Instance.PhoneClientDict[TeamPhone.Number]=other;
            TeamPhone.Register();
            Require(ReferenceEquals(other,PlayerStore.Instance.PhoneClientDict[TeamPhone.Number]),"overwrote contact");
            TeamPhone.Remove();
            Require(PlayerStore.Instance.PhoneClientDict.ContainsKey(TeamPhone.Number),"removed another contact");
        });
        Check("unbought routes stay disabled and hangup remains available", () =>
        {
            var call=Call(); Require(call.choices.Count==6,"wrong menu length");
            for(var i=0;i<5;i++) Require(!call.IsChoiceEnabledAt(i),"unbought route enabled");
            var ended=PhoneUIManager.Instance.CallsEnded; SelectPhone(call,5);
            Require(PhoneUIManager.Instance.CallsEnded==ended+1,"call did not end");
        });
        Check("each team pauses independently, cancels only its pending visit, and waits three days", () =>
        {
            foreach(var team in TeamDefinitions.All) BuyUpgrade(1,team);
            StoreStation.Day=11;
            var manager=PlayerStore.Instance.storeClientManager;
            ModHook.Generate(manager); Require(manager.clientStack.Count==5,"missing teams");
            manager.AddClient(new StoreClient {identifier="native"});
            SelectPhone(Call(),1);
            Require(manager.clientStack.Count==5 && !manager.clientStack.Any(c=>c.identifier==TeamDefinitions.Metal.ClientId),"wrong queue removal");
            Require(TeamStateRepository.Read(TeamDefinitions.Metal).resumeAvailableDay==14,"wrong cooldown date");
            StoreStation.Day=13; Require(!Call().IsChoiceEnabledAt(1),"early resume available");
            Require(!TeamSupplyControl.SetPaused(TeamDefinitions.Metal,false),"early resume action succeeded");
            StoreStation.Day=14; SelectPhone(Call(),1);
            var state=TeamStateRepository.Read(TeamDefinitions.Metal);
            Require(!state.paused && state.nextDay==15,"resume must be next day");
            Require(!DeliveryRules.CanQueue(state,14,1) && DeliveryRules.CanQueue(state,15,1),"resume delivery date wrong");
            Require(TeamStateRepository.Read(TeamDefinitions.Printer).nextDay==11,"other team rescheduled");
        });
        Check("pause survives load and upgrades without renewing first wholesale or catching up", () =>
        {
            BuyUpgrade(1); var client=Arrive(11); client.OnDismiss();
            StoreStation.Day=12; SelectPhone(Call(),0); BuyUpgrade(2); Reload();
            var state=TeamStateRepository.Read();
            Require(state.paused && state.resumeAvailableDay==15 && state.offerDay==11,"upgrade/load reset schedule");
            StoreStation.Day=30; Require(!DeliveryRules.CanQueue(state,30,2),"paused route auto resumed");
            SelectPhone(Call(),0); Require(TeamStateRepository.Read().nextDay==31,"catchup visit scheduled");
            var resumed=Arrive(31);
            Require(resumed.wholesaleDiscount==-15 && TeamStateRepository.Read().offerLevel==2,"renewed first discount or wrong tier");
        });
        Check("pause before first visit preserves the initial half price and original saves default active", () =>
        {
            var legacy=UnityEngine.JsonUtility.FromJson<DeliveryState>("{\"version\":1,\"nextDay\":11,\"offerDay\":-1,\"closed\":true}");
            Require(DeliveryRules.Valid(legacy) && !legacy.paused && legacy.resumeAvailableDay==0,"legacy fields invalid");
            BuyUpgrade(1); SelectPhone(Call(),0); StoreStation.Day=13; SelectPhone(Call(),0);
            Require(Arrive(14).wholesaleDiscount==-50,"first special lost");
        });
        Check("stale phone actions cannot toggle twice or extend a pause", () =>
        {
            BuyUpgrade(1); var menu=Call(); SelectPhone(menu,0);
            StoreStation.Day=11;
            menu.choices[0].Item3();
            Require(TeamStateRepository.Read().resumeAvailableDay==13,"duplicate action extended cooldown");
            Require(!menu.IsChoiceEnabledAt(0),"stale stop still enabled");
        });
        Check("current supplier trade is kept while later arrivals stop", () =>
        {
            BuyUpgrade(1); var current=Arrive(11);
            SelectPhone(Call(),0);
            Require(ReferenceEquals(PlayerStore.Instance.currentClientInstance.GetClientBlueprint(),current),"dismissed current trade");
            Require(EmporiumEntry.Instance.frontInvinvElement.Items.Count==8,"destroyed current goods");
            Reload(); var queue=new StoreClientManager(); ModHook.Generate(queue);
            Require(queue.clientStack.Count==0,"paused batch requeued on reload");
        });
        Check("all locales expose exactly six phone options without missing keys", () =>
        {
            foreach(var team in TeamDefinitions.All) BuyUpgrade(1,team);
            foreach(var locale in new[]{"zh","en","fr","de","ja","ru","es","pl"})
            {
                ModHelper.Locale=locale; var menu=Call();
                Require(menu.choices.Count==6 && !menu.Text.StartsWith("phone."),"missing localized menu");
                Require(menu.choices.All(c=>!c.Item1.name.StartsWith("phone.")),"missing option text");
                Require(menu.choices.All(c=>c.Item2.Text==c.Item1.name && c.Item2.nextDialogue!=null),"wrong native phone dialogue structure");
                var args=new object[]{new StorePhoneClient{dialogFuncId=TeamPhone.DialogKey},null};
                var native=(bool)typeof(TeamPhoneName).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
                Require(!native && (string)args[1]==Text.Get("phone.contact_name"),"contact name not localized");
            }
        });
        Check("bad state disables only the affected route without erasing it", () =>
        {
            BuyUpgrade(1); BuyUpgrade(1,TeamDefinitions.Bottle);
            var bad="{\"version\":999}"; ModHelper.SetModData(ModEntry.Id,TeamDefinitions.Printer.StateKey,bad);
            var menu=Call(); Require(menu.choices.Count==6 && !menu.IsChoiceEnabledAt(0) && menu.IsChoiceEnabledAt(4),"bad state broke whole menu");
            Require(ModHelper.GetModData(ModEntry.Id,TeamDefinitions.Printer.StateKey)==bad,"bad state reset");
        });
    }
}
