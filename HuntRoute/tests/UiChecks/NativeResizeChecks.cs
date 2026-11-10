using System.Numerics;
using System.Reflection;
using HuntRoute;

static class NativeResizeChecks {
 static readonly MethodInfo Fix=typeof(Plugin).Assembly.GetType("HuntRoute.NativeFixedLayout")!
  .GetMethod("FixedFlags",BindingFlags.Static|BindingFlags.NonPublic)!;
 static readonly Type Flags=Fix.GetParameters()[0].ParameterType;
 static uint Bit(string name)=>Convert.ToUInt32(Enum.Parse(Flags,name));
 public static void Check(Vector2 window,IEnumerable<NativeHuntRect> rectangles){
  uint top=Bit("AnchorTop"),left=Bit("AnchorLeft"),bottom=Bit("AnchorBottom"),right=Bit("AnchorRight");
  uint visible=Bit("Visible"),enabled=Bit("Enabled"),events=Bit("EmitsEvents");
  for(int combination=0;combination<16;combination++){
   uint original=visible|enabled|events;
   if((combination&1)!=0)original|=top;
   if((combination&2)!=0)original|=left;
   if((combination&4)!=0)original|=bottom;
   if((combination&8)!=0)original|=right;
   uint fixedFlags=Convert.ToUInt32(Fix.Invoke(null,[Enum.ToObject(Flags,original)]));
   if((fixedFlags&(top|left|bottom|right))!=(top|left)||(fixedFlags&(visible|enabled|events))!=(visible|enabled|events))
    throw new Exception("Native content must remain top-left anchored without losing input flags");
   // A centered node moves by half the window delta. A right/bottom anchored
   // node moves by the full delta. Pinning prevents both when the game resizes.
   foreach(var previous in new[]{new Vector2(400,180),new Vector2(640,660),new Vector2(460,390),window}){
    var delta=window-previous;
    var shift=new Vector2((fixedFlags&left)!=0?0:(fixedFlags&right)!=0?delta.X:delta.X/2,
     (fixedFlags&top)!=0?0:(fixedFlags&bottom)!=0?delta.Y:delta.Y/2);
    foreach(var rect in rectangles){
     if(shift!=Vector2.Zero)throw new Exception("Window resizing moved native content");
     var position=rect.Position+shift;
     if(rect.Size.X>0&&rect.Size.Y>0&&(position.X<0||position.Y<30||position.X+rect.Size.X>window.X+.1f||position.Y+rect.Size.Y>window.Y+.1f))
      throw new Exception("Native content overlaps the title or leaves its window");
    }
   }
  }
 }
}
