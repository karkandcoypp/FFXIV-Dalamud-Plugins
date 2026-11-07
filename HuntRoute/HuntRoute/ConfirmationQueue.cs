using System;
using System.Collections.Generic;
namespace HuntRoute;

public sealed record ConfirmationRequest(string Owner,string Key,string Text,Func<bool> Valid,Func<bool> Ready,Func<bool> Accept);
public sealed class ConfirmationQueue {
    private readonly Dictionary<string,ConfirmationRequest> pending=[];
    private readonly HashSet<string> shown=[];
    public ConfirmationRequest? Active { get; private set; }
    public void Offer(ConfirmationRequest request) {
        if(shown.Contains(request.Key)) return;
        pending[request.Owner]=request;
    }
    public ConfirmationRequest? Activate(bool available) {
        if(!available || Active!=null) return null;
        foreach(string owner in new[]{"next","shortcut"}) {
            if(!pending.TryGetValue(owner,out var request)) continue;
            if(!request.Valid()) { pending.Remove(owner);continue; }
            if(!request.Ready()) continue;
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
        pending.Remove(owner);
        if(Active?.Owner!=owner) return false;
        Active=null;return true;
    }
    public void Retry(ConfirmationRequest request) {
        if(!request.Valid()) return;
        shown.Remove(request.Key);pending[request.Owner]=request;
    }
    public void Clear() { pending.Clear();Active=null;shown.Clear(); }
}
