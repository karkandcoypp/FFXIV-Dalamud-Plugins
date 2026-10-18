using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud.FullscreenCutscenes;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/pcutscenes";
    private const string LetterboxSignature =
        "E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ??";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly ICondition condition;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private Hook<UpdateLetterboxingDelegate>? updateLetterboxingHook;

    private delegate nint UpdateLetterboxingDelegate(nint thisPtr);

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        ISigScanner sigScanner,
        IGameInteropProvider gameInteropProvider,
        ICondition condition,
        IChatGui chatGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.condition = condition;
        this.chatGui = chatGui;
        this.log = log;

        this.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(pluginInterface);

        commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage =
                "/pcutscenes: toggle the plugin\n" +
                "/pcutscenes true|false: set the plugin state\n" +
                "/pcutscenes status: show the plugin and native hook state",
        });

        this.InitializeHook(sigScanner, gameInteropProvider);
    }

    public void Dispose()
    {
        this.updateLetterboxingHook?.Dispose();
        this.commandManager.RemoveHandler(CommandName);
    }

    private void InitializeHook(ISigScanner sigScanner, IGameInteropProvider gameInteropProvider)
    {
        try
        {
            if (!sigScanner.TryScanText(LetterboxSignature, out var address))
            {
                this.log.Error("Ultrawide Cutscenes: letterbox signature was not found; the plugin is inactive.");
                return;
            }

            this.updateLetterboxingHook =
                gameInteropProvider.HookFromAddress<UpdateLetterboxingDelegate>(address, this.UpdateLetterboxingDetour);
            this.updateLetterboxingHook.Enable();
            this.log.Information("Ultrawide Cutscenes: native hook enabled at {Address:X}.", address);
        }
        catch (Exception exception)
        {
            this.updateLetterboxingHook?.Dispose();
            this.updateLetterboxingHook = null;
            this.log.Error(exception, "Ultrawide Cutscenes: failed to initialize the native hook.");
        }
    }

    private unsafe nint UpdateLetterboxingDetour(nint thisPtr)
    {
        var hook = this.updateLetterboxingHook;
        if (hook is null)
            return 0;

        if (this.configuration.IsEnabled && this.IsWatchingCutscene())
        {
            var config = (CutsceneConfig*)thisPtr;
            config->LetterboxFlags &= ~(1 << 5);
        }

        return hook.Original(thisPtr);
    }

    private bool IsWatchingCutscene() =>
        this.condition[ConditionFlag.OccupiedInCutSceneEvent] ||
        this.condition[ConditionFlag.WatchingCutscene78];

    private void OnCommand(string command, string arguments)
    {
        var argument = arguments.Trim();

        if (argument.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            this.PrintStatus();
            return;
        }

        if (argument.Length == 0)
        {
            this.configuration.IsEnabled = !this.configuration.IsEnabled;
        }
        else if (bool.TryParse(argument, out var enabled))
        {
            this.configuration.IsEnabled = enabled;
        }
        else
        {
            this.chatGui.PrintError("[Ultrawide Cutscenes] Use /pcutscenes, true, false, or status.");
            return;
        }

        this.configuration.Save();
        this.PrintStatus();
    }

    private void PrintStatus()
    {
        var enabled = this.configuration.IsEnabled ? "enabled" : "disabled";
        var hookState = this.updateLetterboxingHook?.IsEnabled == true ? "ready" : "not available";
        this.chatGui.Print($"[Ultrawide Cutscenes] {enabled}; native hook: {hookState}.");
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CutsceneConfig
    {
        [FieldOffset(0x40)]
        public int LetterboxFlags;
    }
}
