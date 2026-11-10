using System;
using System.Collections.Generic;
namespace HuntRoute;

public sealed record BillSnapshot(bool Taken,byte Order,int[] Kills);
// A successful native read can still be transiently empty. Require a second,
// spaced observation before removing/replacing a previously obtained contract.
public sealed class BillSnapshotGuard {
 private readonly Dictionary<byte,BillSnapshot> accepted=[];
 private readonly Dictionary<byte,(BillSnapshot Snapshot,long At)> pending=[];
 public BillSnapshot? Current(byte bill)=>accepted.GetValueOrDefault(bill);
 public bool Observe(byte bill,BillSnapshot value,long now,byte maximum,bool previouslyTaken=false,byte previousOrder=0){
  if(value.Order>maximum || value.Taken&&value.Order==0 || value.Kills.Length!=5 || Array.Exists(value.Kills,k=>k<0)){pending.Remove(bill);return false;}
  var previous=Current(bill);
  bool destructive=(previous?.Taken??previouslyTaken) && (!value.Taken || value.Order!=(previous?.Order??previousOrder));
  bool regressed=false;
  if(previous is {Taken:true} && value.Taken && previous.Order==value.Order)
   for(int i=0;i<value.Kills.Length;i++)regressed|=value.Kills[i]<previous.Kills[i];
  if(destructive||regressed){
   if(!pending.TryGetValue(bill,out var candidate)||candidate.Snapshot.Taken!=value.Taken||candidate.Snapshot.Order!=value.Order||regressed&&!candidate.Snapshot.Kills.AsSpan().SequenceEqual(value.Kills)||now<candidate.At){pending[bill]=(value with {Kills=(int[])value.Kills.Clone()},now);return false;}
   if(now-candidate.At<750)return false;
  }
  pending.Remove(bill);accepted[bill]=value with {Kills=(int[])value.Kills.Clone()};return true;
 }
}
