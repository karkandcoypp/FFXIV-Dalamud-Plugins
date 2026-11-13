using System;
using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

public sealed record ConfirmationRequest(string Owner,string Key,string Text,Func<bool> Valid,Func<bool> Ready,Func<bool> Accept,TravelDialogContent? Presentation=null,Action? OnShown=null,string? DeclineKey=null,Func<bool>? ConfirmReady=null) {
    public bool CanConfirm()=>Ready()&&(ConfirmReady?.Invoke()??true);
}
public sealed class ConfirmationQueue {
    private readonly Dictionary<string,ConfirmationRequest> pending=[];
    private readonly HashSet<string> shown=[];
    private readonly HashSet<string> declined=[];
    public bool IsDeclined(string key)=>declined.Contains(key);
    private bool WasDeclined(ConfirmationRequest request)=>request.DeclineKey is {} key&&IsDeclined(key);
    private readonly Dictionary<string,long> retryAfter=[];
    public ConfirmationRequest? Active { get; private set; }
    public void Offer(ConfirmationRequest request) {
        if(shown.Contains(request.Key)||WasDeclined(request)) return;
        pending[request.Owner]=request;
    }
    public ConfirmationRequest? Activate(bool available,long now=0) {
        if(!available || Active!=null) return null;
        foreach(string owner in new[]{"travel","next","shortcut","pickup"}.Concat(pending.Keys.Where(k=>k.StartsWith("completion:",StringComparison.Ordinal)).ToArray())) {
            if(!pending.TryGetValue(owner,out var request)) continue;
            if(WasDeclined(request)||!request.Valid()) { pending.Remove(owner);continue; }
            if((retryAfter.TryGetValue(owner,out long until)&&now<until)||!request.Ready()) continue;
            retryAfter.Remove(owner);
            pending.Remove(owner);shown.Add(request.Key);Active=request;return request;
        }
        return null;
    }
    public ConfirmationRequest? Resolve(string key,bool yes,bool userResponse=true) {
        if(Active is not {} request || request.Key!=key) return null;
        Active=null;
        if(!yes&&request.DeclineKey is {} group){
            if(userResponse)declined.Add(group);
            else shown.Remove(request.Key);
        }
        return yes && request.Valid()?request:null;
    }
    public bool Cancel(string owner) {
        pending.Remove(owner);retryAfter.Remove(owner);
        if(Active?.Owner!=owner) return false;
        if(Active.DeclineKey!=null)shown.Remove(Active.Key);
        Active=null;return true;
    }
    public void Interrupt(string key) {
        if(Active is not {} request || request.Key!=key) return;
        Active=null;
        shown.Remove(key);
        if(request.Valid()&&!WasDeclined(request)) pending.TryAdd(request.Owner,request);
    }
    public void Retry(ConfirmationRequest request) {
        if(WasDeclined(request)||!request.Valid()) return;
        shown.Remove(request.Key);pending[request.Owner]=request;
    }
    public void RetryOpen(ConfirmationRequest request,long now) {
        Retry(request);
        retryAfter[request.Owner]=now+10000;
    }
    public void Clear() { pending.Clear();Active=null;shown.Clear();declined.Clear();retryAfter.Clear(); }
}
