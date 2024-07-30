using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.VisualBasic;

namespace WarpPlugin;

public class WarpConfig : BasePluginConfig
{
    [JsonPropertyName("teleportCost")] public int teleportCost { get; set; } = 1000;
    [JsonPropertyName("warpSetCost")] public int warpSetCost { get; set; } = 400;
    [JsonPropertyName("warpPersistsBetweenRounds")] public bool warpPersistsBetweenRounds { get; set; } = false;
}

/*
    Warp plugin by tem

    This plugin allows players to set a warp point and teleport to it (at cost of money, it can be changed in the config)

    Each player can only set one warp point at a time - and only for themselves.

    available commands:
    warp_place - sets a warp point
    warp_teleport - teleports to a warp point

    admin commands:
    warp_allow <name/part of name>- allows a player to set a warp point
    warp_disallow <name/part of name> - disallows a player to set a warp point
*/

public class WarpPlugin : BasePlugin, IPluginConfig<WarpConfig>
{
    public override string ModuleName => "WarpPlugin";
    public override string ModuleVersion => "0.0.0";
    public override string ModuleAuthor => "tem";
    // index, position
    private Dictionary<uint, Tuple<Vector, QAngle>> playerWarps = new Dictionary<uint, Tuple<Vector, QAngle>>();
    private List<uint> allowedPlayerIndexes = new List<uint>();
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundEnd>((@event, info) =>
        {
            if (!Config.warpPersistsBetweenRounds)
                playerWarps.Clear();

            var players = Utilities.GetPlayers();

            foreach (var p in players)
                if (allowedPlayerIndexes.Contains((uint)p.Slot))
                    p.PrintToChat($"Your warp was reset due to round end.");

            return HookResult.Continue;
        });
    }
    public override void Unload(bool hotReload)
    {
    }

    public required WarpConfig Config { get; set; }

    public void OnConfigParsed(WarpConfig config)
    {
        Config = config;
    }

    [RequiresPermissions(permissions: "@css/root")]
    [ConsoleCommand("warp_allow", "hiii")]
    [CommandHelper(minArgs: 1, usage: "<player_name/part_of_name>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnWarpAllow(CCSPlayerController? player, CommandInfo info)
    {
        var players = Utilities.GetPlayers();

        foreach (var p in players)
        {
            if (p.PlayerName.Contains(info.ArgByIndex(1)))
            {
                allowedPlayerIndexes.Add((uint)p.Slot);
                PrintSomewhere(player, $"added {p.PlayerName} to the allowed list");
                return;
            }
        }

        PrintSomewhere(player, $"Failed to find player with {info.ArgByIndex(1)} in name");
    }

    [RequiresPermissions(permissions: "@css/root")]
    [ConsoleCommand("warp_disallow", "hiii")]
    [CommandHelper(minArgs: 1, usage: "<player_name/part_of_name>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnWarpDisallow(CCSPlayerController? player, CommandInfo info)
    {
        var players = Utilities.GetPlayers();

        if (allowedPlayerIndexes.Count == 0)
        {
            PrintSomewhere(player, $"no players are allowed to warp :sadge:");
            return;
        }

        foreach (var p in players)
        {
            if (p.PlayerName.Contains(info.ArgByIndex(1)))
            {
                allowedPlayerIndexes.Remove((uint)p.Slot);
                PrintSomewhere(player, $"removed {p.PlayerName} from the allowed list");
                return;
            }
        }

        PrintSomewhere(player, $"Failed to find player with {info.ArgByIndex(1)} in name");
    }

    [ConsoleCommand("warp_place", "hiii")]
    [CommandHelper(minArgs: 0, usage: "hiii", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnWarpPlace(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.PlayerPawn == null || player.PlayerPawn.Value == null || player.PlayerPawn.Value.AbsOrigin == null)
        {
            Console.WriteLine("no player?");
            return;
        }

        if (!allowedPlayerIndexes.Contains((uint)player.Slot))
            return;

        if (!PaidAction(player, Config.warpSetCost, "you dont have enough money to set a warp :sadge:"))
            return;

        var vec = player.PlayerPawn.Value.AbsOrigin;
        var angle = player.PlayerPawn.Value.V_angle;

        var tupl = new Tuple<Vector, QAngle>(new Vector(vec.X, vec.Y, vec.Z), new QAngle(angle.X, angle.Y, angle.Z));
        playerWarps[(uint)player.Slot] = tupl;
    }

    [ConsoleCommand("warp_teleport", "hiii")]
    [CommandHelper(minArgs: 0, usage: "hiii", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnWarpTeleport(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.PlayerPawn == null || player.PlayerPawn.Value == null)
        {
            Console.WriteLine("no player?");
            return;
        }

        if (!allowedPlayerIndexes.Contains((uint)player.Slot))
            return;

        if (!PaidAction(player, Config.teleportCost, "you dont have enough money to teleport :sadge:"))
            return;

        var pawn = player.PlayerPawn.Value;
        var pos = playerWarps[(uint)player.Slot].Item1;
        var angle = playerWarps[(uint)player.Slot].Item2;

        pawn.Teleport(pos, angle, new Vector(0f, 0f, 0f));
    }

    private bool PaidAction(CCSPlayerController player, int amount, string denyMessage)
    {
        if (player.InGameMoneyServices!.Account < amount)
        {
            player.PrintToChat($"Failed transaction: need  {ChatColors.Red}${amount}  {ChatColors.White}but have  {ChatColors.Red}${player.InGameMoneyServices!.Account}");
            player.PrintToChat($"{denyMessage}");
            player.ExecuteClientCommand("play sounds/ui/weapon_cant_buy.vsnd");
            return false;
        }

        player.InGameMoneyServices!.Account -= amount;
        player.PrintToChat($"Successful transaction!  {ChatColors.Red}-${amount}");

        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
        player.ExecuteClientCommand("play sounds/ui/panorama/claim_gift_01.vsnd");
        return true;
    }

    private void PrintSomewhere(CCSPlayerController? player, string msg)
    {
        if (player != null)
            player.PrintToChat(msg);
        else
            Server.PrintToConsole(msg);
    }
}