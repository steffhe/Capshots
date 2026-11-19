using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;

// Kill feed, text chat and ping.
public partial class Manager
{
    // Sent by the player who died, so everyone can show it in the kill feed
    public void KillFeed_S(int killer, int victim, string weapon, bool headshot)
    {
        object[] package = new object[] { killer, victim, weapon, headshot };

        PhotonNetwork.RaiseEvent((byte)EventCodes.KillFeed, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void KillFeed_R(object[] data)
    {
        int killer = (int)data[0];
        int victim = (int)data[1];
        string weapon = (string)data[2];
        bool headshot = (bool)data[3];

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (IsTeamMode)
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me,
                TeamHex(GetTeam(killer)), TeamHex(GetTeam(victim)));
        }
        else
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me);
        }

        if (killer == me && victim != me) FeedbackUI.Kill();
    }

    public void SendChat_S(string message)
    {
        message = message.Trim();
        if (message.Length == 0) return;
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        object[] package = new object[] { PhotonNetwork.LocalPlayer.ActorNumber, message };
        PhotonNetwork.RaiseEvent((byte)EventCodes.ChatMessage, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void ChatMessage_R(object[] data)
    {
        int actor = (int)data[0];
        string message = (string)data[1];
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        int team = GetTeam(actor);
        string color = team >= 0 ? TeamHex(team) : (actor == PhotonNetwork.LocalPlayer.ActorNumber ? "#ffd84a" : "#dddddd");
        ChatUI.AddMessage(GetPlayerName(actor), color, message);
    }

    // Every couple of seconds, share this player's ping so others can see it on the scoreboard
    private IEnumerator SharePing()
    {
        int lastShared = -1;
        while (true)
        {
            int ping = PhotonNetwork.GetPing();
            if (PhotonNetwork.InRoom && Mathf.Abs(ping - lastShared) >= 5)
            {
                lastShared = ping;
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "ping", ping } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private string PingLabel(int actor)
    {
        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        if (player == null) return "";

        int ping = actor == PhotonNetwork.LocalPlayer.ActorNumber ? PhotonNetwork.GetPing() : -1;
        object value;
        if (ping < 0 && player.CustomProperties.TryGetValue("ping", out value)) ping = (int)value;
        if (ping < 0) return "";

        string color = ping < 80 ? "#7ddc6f" : ping < 150 ? "#e8c84a" : "#e8604a";
        return $"  <size=14><color={color}>{ping} ms</color></size>";
    }
}
