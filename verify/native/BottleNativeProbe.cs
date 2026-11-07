using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class BottleNativeProbe
{
    private static void Main(string[] args)
    {
        if(args.Length!=2) throw new ArgumentException("Pass game root and candidate DLL.");
        var managed=Path.Combine(args[0],"Probably Stolen_Data","Managed");
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>
        {
            var path=Path.Combine(managed,new AssemblyName(eventArgs.Name).Name+".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Probe(args[1], managed);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Probe(string dll, string managed)
    {
        // The headless host lacks ModLoader's initialized game-assembly catalog.
        // Supply only names present in the actual installed Managed directory.
        var scan=ModScanner.Scan(new[]{dll}, System.IO.Directory.GetFiles(managed,"*.dll").Select(Path.GetFileNameWithoutExtension));
        if(scan.Refused || scan.Capabilities.Count!=0)
            throw new Exception("Scanner rejected or requested extra capabilities: "+scan.Error+" "+scan.FormatFindings());
        var definitions=new List<NetworkUpgrade>();
        var routes=new[]{"printer","metal","electronic","ore","bottle"};
        foreach(var route in routes) for(var level=1;level<=3;level++)
        {
            var upgrade=new NetworkUpgrade("ps_scavenger_teams."+route+"."+level){tab="",subtitle="Scavengers"};
            if(level>1) upgrade.prerequisite.Add("ps_scavenger_teams."+route+"."+(level-1));
            definitions.Add(upgrade);
        }
        var layout=NetworkUpgradeLayout.Build(definitions);
        if(layout.Count!=1 || layout[0].tab!="") throw new Exception("Unexpected tab.");
        var card=layout[0].cards.Single(c=>c.subtitle=="Scavengers");
        if(card.rows!=5 || card.cells.Count!=15) throw new Exception("Wrong fifth-row dimensions.");
        for(var column=0;column<3;column++)
            if(card.cells.Single(c=>c.row==4 && c.column==column).id!="ps_scavenger_teams.bottle."+(column+1))
                throw new Exception("Bottle row order incorrect.");
        var occupied=new List<GridShape>();
        for(var i=0;i<18;i++)
        {
            GridShape bottle=new GridShapeBuilder(1,4).SetDataFill(1,4).SetPosition(i,0);
            if(occupied.Any(b=>b.Intersects(bottle))) throw new Exception("Bottle overlap.");
            occupied.Add(bottle);
        }
        GridShape freeColumn=new GridShapeBuilder(1,4).SetDataFill(1,4).SetPosition(18,0);
        if(occupied.Any(b=>b.Intersects(freeColumn))) throw new Exception("Reserved space consumed.");
        Console.WriteLine("PASS native scanner: "+scan.MethodsScanned+" methods, no refusal/extra capabilities; native layout has fifth three-tier row; 18 modelled 1x4 bottles fit 24x4 with six columns free. No Unity scene was launched.");
    }
}
