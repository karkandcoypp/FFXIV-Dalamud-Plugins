using System.Reflection;
using System.Reflection.Emit;
using HuntRoute;

static class NativeTextLifecycleChecks {
 static IEnumerable<MethodBase> Calls(MethodInfo method){
  var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)
   .Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!)
   .ToDictionary(c=>unchecked((ushort)c.Value));
  var bytes=method.GetMethodBody()!.GetILAsByteArray()!;
  for(int at=0;at<bytes.Length;){
   ushort value=bytes[at++];
   if(value==0xfe)value=(ushort)(0xfe00|bytes[at++]);
   var code=codes[value];
   if(code.OperandType==OperandType.InlineMethod)
    yield return method.Module.ResolveMethod(BitConverter.ToInt32(bytes,at))!;
   at+=code.OperandType switch{
    OperandType.InlineNone=>0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
    OperandType.InlineVar=>2,
    OperandType.InlineI8 or OperandType.InlineR=>8,
    OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,at),
    _=>4
   };
  }
 }
 public static void Run(){
  var type=typeof(Plugin).Assembly.GetType("HuntRoute.NativeButtonText")!;
  var measure=type.GetMethod("Measure",BindingFlags.Static|BindingFlags.NonPublic)!;
  var measuredCalls=Calls(measure).ToArray();
  if(measuredCalls.Any(m=>m.Name.StartsWith("set_",StringComparison.Ordinal))
   ||!measuredCalls.Any(m=>m.Name=="GetTextDrawSize"&&m.GetParameters().Length==2))
   throw new Exception("Label measurement must use explicit text without modifying a drawable node");
  var apply=type.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
  var writes=Calls(apply).Where(m=>m.Name.StartsWith("set_",StringComparison.Ordinal)).ToArray();
  if(writes.Length==0||writes[^1].Name!="set_String"||!writes.Any(m=>m.Name=="set_Size"))
   throw new Exception("Full source caption must be restored after all native sizing and style changes");
  Console.WriteLine("PASS native label measurement side effects and caption restoration order");
 }
}
