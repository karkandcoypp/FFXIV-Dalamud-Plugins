using System.Reflection;
using Dalamud.Plugin.Services;
using Dalamud.Game.Player;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
var dev=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(context,name)=>{var p=Path.Combine(dev,name.Name+".dll");return File.Exists(p)?context.LoadFromAssemblyPath(p):null;};
var types=new List<Type>{typeof(PlayerAttribute),typeof(IPlayerState),typeof(IUnlockState),typeof(GameInventoryItem),typeof(GameInventoryType),typeof(RaptureGearsetModule.GearsetEntry),typeof(RaptureGearsetModule.GearsetItem),typeof(Item),typeof(Recipe),typeof(ClassJob),typeof(Materia),typeof(ItemFood),typeof(ItemFood.ParamsStruct),typeof(GatheringSubCategory),typeof(ItemAction),typeof(FFXIVClientStructs.FFXIV.Client.Game.InventoryContainer),typeof(CraftAction),typeof(Lumina.Excel.Sheets.Action)};
foreach(var name in new[]{"FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState","FFXIVClientStructs.FFXIV.Client.Game.UI.UIState"}){var t=typeof(RaptureGearsetModule).Assembly.GetType(name);if(t!=null)types.Add(t);}
foreach(var t in types){Console.WriteLine("TYPE "+t.FullName);if(t.IsEnum)Console.WriteLine(string.Join(", ",Enum.GetNames(t)));else{foreach(var p in t.GetProperties())Console.WriteLine(" P "+p.PropertyType+" "+p.Name);foreach(var f in t.GetFields())Console.WriteLine(" F "+f.FieldType+" "+f.Name);foreach(var m in t.GetMethods().Where(m=>m.DeclaringType==t&&!m.IsSpecialName))Console.WriteLine(" M "+m);}}
