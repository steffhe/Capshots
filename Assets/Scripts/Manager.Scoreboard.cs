using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The scoreboard shown while holding the scoreboard key and at the end of the match.
public partial class Manager
{
    private void Leaderboard(Transform lb)
    {
        for (int i = 2; i < lb.childCount; i++)
        {
            Destroy(lb.GetChild(i).gameObject);
        }


        Text modeText = lb.Find("Header/Mode").GetComponent<Text>();
        modeText.supportRichText = true;
        modeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        modeText.text = IsTeamMode ? TeamHeader() : "FREE FOR ALL";
        lb.Find("Header/Map").GetComponent<Text>().text = CurrentMapName();

        GameObject playerCard = lb.GetChild(1).gameObject;
        playerCard.SetActive(false);

        List<PlayerInfo> sorted = SortPlayers(playerInfo);

        bool alternateColors = false;
        foreach(PlayerInfo a in sorted)
        {
            GameObject newcard = Instantiate(playerCard, lb) as GameObject;

            if (alternateColors) newcard.GetComponent<Image>().color = new Color32(0, 0,0, 100);
            if (IsTeamMode)
            {
                Color teamColor = TeamColors[a.team % 2];
                teamColor.a = 0.35f;
                newcard.GetComponent<Image>().color = teamColor;
            }

            newcard.transform.Find("Level").GetComponent<Text>().text = a.profile.level.ToString("00");
            Text usernameText = newcard.transform.Find("Username").GetComponent<Text>();
            usernameText.supportRichText = true;
            usernameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            usernameText.text = a.profile.username + PingLabel(a.actor);
            newcard.transform.Find("Score Value").GetComponent<Text>().text = (a.kills * 100).ToString();
            newcard.transform.Find("Kills Value").GetComponent<Text>().text = a.kills.ToString();
            newcard.transform.Find("Deaths Value").GetComponent<Text>().text = a.deaths.ToString();

            newcard.SetActive(true);
        }

        lb.gameObject.SetActive(true);

    }

    private List<PlayerInfo> SortPlayers (List<PlayerInfo> info)
    {
        List<PlayerInfo> sorted = new List<PlayerInfo>();

        // Team mode: blue team first, then red, each sorted by kills
        if (IsTeamMode)
        {
            List<PlayerInfo> byKills = new List<PlayerInfo>(info);
            byKills.Sort((x, y) => x.team != y.team ? x.team.CompareTo(y.team) : y.kills.CompareTo(x.kills));
            return byKills;
        }

        while (sorted.Count < info.Count)
        {
            short highest = -1;
            PlayerInfo selection = info[0];

            foreach (PlayerInfo a in info)
            {
                if (sorted.Contains(a)) continue;
                if (a.kills > highest)
                {
                    selection = a;
                    highest = a.kills;
                }
            }

            sorted.Add(selection);
        }

        return sorted;
    }
}
