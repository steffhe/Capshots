using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Kill feed, text chat and ping.
public partial class Manager
{
    // Sent by the host when a player dies, so everyone can show it in the kill feed.
    // killer is -1 when nobody gets the kill.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillFeed(int killer, int victim, string weapon, NetworkBool headshot)
    {
        int me = LocalActor;
        if (IsTeamMode)
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me,
                TeamHex(GetTeam(killer)), TeamHex(GetTeam(victim)));
        }
        else
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me);
        }

        if (killer == me && victim != me)
        {
            FeedbackUI.Kill(headshot);
            // Kills during warmup and the countdown don't count
            if (state == GameState.Playing && !matchEnded) AwardXp(xpPerKill, "KILL");
        }

        if (victim == me)
        {
            FeedbackUI.Died();
            FeedbackUI.ShowDeath(GetPlayerName(killer), weapon, headshot, respawnTime);
        }
    }

    public void SendChat_S(string message)
    {
        message = message.Trim();
        if (message.Length == 0) return;
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        RPC_Chat(message);
    }

    // Sent to the host, which passes it on to everyone
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_Chat(string message, RpcInfo info = default)
    {
        if (string.IsNullOrEmpty(message)) return;
        int actor = info.Source.IsRealPlayer ? info.Source.PlayerId : LocalActor;
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        int team = GetTeam(actor);
        string color = team >= 0 ? TeamHex(team) : (actor == LocalActor ? "#ffd84a" : "#dddddd");
        ChatUI.AddMessage(GetPlayerName(actor), color, message);
    }

    // Host only, every second: measure everyone's round trip time for the scoreboard
    private void UpdatePings()
    {
        foreach (PlayerRef player in StatsKeys())
        {
            // Bots run on the host: no connection to measure
            if (IsBot(player)) continue;
            int ping = player == Runner.LocalPlayer ? 0 : Mathf.RoundToInt((float)Runner.GetPlayerRtt(player) * 1000f);
            PlayerStats stats = Stats[player];
            // Only send real changes, so the scoreboard isn't resent every second
            if (Mathf.Abs(stats.Ping - ping) < 5) continue;
            stats.Ping = (short)Mathf.Clamp(ping, 0, short.MaxValue);
            Stats.Set(player, stats);
        }
    }

    private string PingLabel(int actor)
    {
        int index = FindPlayerIndex(actor);
        if (index < 0) return "";
        // Bots' names already say BOT, and they have no connection to measure
        if (IsBotActor(actor)) return "";

        int ping = playerInfo[index].ping;
        // Our own round trip to the host is known here first hand
        if (actor == LocalActor && Runner != null && !HasStateAuthority) ping = Mathf.RoundToInt((float)Runner.GetPlayerRtt(Runner.LocalPlayer) * 1000f);
        if (ping < 0) return "";

        string color = ping < 80 ? "#7ddc6f" : ping < 150 ? "#e8c84a" : "#e8604a";
        return $"  <size=14><color={color}>{ping} ms</color></size>";
    }
}
