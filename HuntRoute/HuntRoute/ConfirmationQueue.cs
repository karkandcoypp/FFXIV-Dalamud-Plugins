using System;
using System.Collections.Generic;
namespace HuntRoute;

public sealed record ConfirmationRequest(string Owner,string Key,string Text,Func<bool> Valid,Func<bool> Ready,Func<bool> Accept,TravelDialogContent? Presentation=null,Action? OnShown=null);
public sealed class ConfirmationQueue {
    private readonly Dictionary<string,ConfirmationRequest> pending=[];
    private readonly HashSet<string> shown=[];
    private readonly Dictionary<string,long> retryAfter=[];
    public ConfirmationRequest? Active { get; private set; }
    public void Offer(ConfirmationRequest request) {
        if(shown.Contains(request.Key)) return;
        pending[request.Owner]=request;
    }
    public ConfirmationRequest? Activate(bool available,long now=0) {
        if(!available || Active!=null) return null;
        foreach(string owner in new[]{"travel","next","shortcut","pickup"}) {
            if(!pending.TryGetValue(owner,out var request)) continue;
            if(!request.Valid()) { pending.Remove(owner);continue; }
            if((retryAfter.TryGetValue(owner,out long until)&&now<until)||!request.Ready()) continue;
            retryAfter.Remove(owner);
            pending.Remove(owner);shown.Add(request.Key);Active=request;return request;
        }
        return null;
    }
    public ConfirmationRequest? Resolve(string key,bool yes) {
        if(Active is not {} request || request.Key!=key) return null;
        Active=null;
        return yes && request.Valid()?request:null;
    }
    public bool Cancel(string owner) {
        pending.Remove(owner);retryAfter.Remove(owner);
        if(Active?.Owner!=owner) return false;
        Active=null;return true;
    }
    public void Retry(ConfirmationRequest request) {
        if(!request.Valid()) return;
        shown.Remove(request.Key);pending[request.Owner]=request;
    }
    public void RetryOpen(ConfirmationRequest request,long now) {
        Retry(request);
        retryAfter[request.Owner]=now+10000;
    }
    public void Clear() { pending.Clear();Active=null;shown.Clear();retryAfter.Clear(); }
}
