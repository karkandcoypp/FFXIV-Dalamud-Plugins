using System;
using System.Collections.Generic;
namespace HuntRoute;

public sealed record BillSnapshot(bool Taken,byte Order,int[] Kills);
// A successful native read can still be transiently empty. Require a second,
// spaced observation before removing/replacing a previously obtained contract.
public sealed class BillSnapshotGuard {
 private readonly Dictionary<byte,BillSnapshot> accepted=[];
 private readonly Dictionary<byte,(bool Taken,byte Order,long At)> pending=[];
 public BillSnapshot? Current(byte bill)=>accepted.GetValueOrDefault(bill);
 public bool Observe(byte bill,BillSnapshot value,long now,byte maximum,bool previouslyTaken=false,byte previousOrder=0){
  if(value.Order>maximum || value.Taken&&value.Order==0 || value.Kills.Length!=5 || Array.Exists(value.Kills,k=>k<0)){pending.Remove(bill);return false;}
  var previous=Current(bill);
  bool destructive=(previous?.Taken??previouslyTaken) && (!value.Taken || value.Order!=(previous?.Order??previousOrder));
  if(destructive){
   if(!pending.TryGetValue(bill,out var candidate)||candidate.Taken!=value.Taken||candidate.Order!=value.Order||now<candidate.At){pending[bill]=(value.Taken,value.Order,now);return false;}
   if(now-candidate.At<750)return false;
  }
  pending.Remove(bill);accepted[bill]=value with {Kills=(int[])value.Kills.Clone()};return true;
 }
}
