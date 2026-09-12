using Unity.Netcode;
using UnityEngine;

public class Soccer : GamemodeScript
{
    [SerializeField] GameObject ballPrefab;   
    [SerializeField] Transform ballSpawnPoint;
    [SerializeField] Collider[] goals;          //indexed by the team that defends the net
    [SerializeField] string goalSound = "";
    [SerializeField] float goalTitleTime = 1.5f;

    NetworkProp ball;
    int[] scores = new int[2];
    float goalFlashUntil;
    string scoreText;

    public override void OnGameModeStart()
    {
        scores[0] = 0;
        scores[1] = 0;

        GameObject obj = Instantiate(ballPrefab, ballSpawnPoint.position, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn(true);
        gameManager.worldObjects.Add(obj);
        ball = obj.GetComponent<NetworkProp>();

        scoreText = "[" + gameManager.GetTeamName(0) + " " + scores[0] + " | " + gameManager.GetTeamName(1) + " " + scores[1] + "]";
    }

    public override void OnGameModeEnd()
    {
        if (ball != null)
        {
            NetworkObject netObj = ball.GetComponent<NetworkObject>();
            gameManager.worldObjects.Remove(ball.gameObject);
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            ball = null;
        }
    }

    bool resetball = false;

    public override void OnGameModeFixedUpdate()
    {
        if(Time.time <= goalFlashUntil) return;

        if(resetball)
        {
            ball.rb.position = ballSpawnPoint.position;
            ball.rb.linearVelocity = Vector3.zero;
            ball.rb.angularVelocity = Vector3.zero;
            resetball = false;
        }

        int secondsLeft = Mathf.Max(0, (int)gameManager.TimeLeft);
        SetTitle(scoreText + " : " + secondsLeft);

        Vector3 ballPos = ball.rb.position;

        for (int i = 0; i < 2; i++)
        {
            if (goals[i].ClosestPoint(ballPos) == ballPos) Score(1-i); //team i defending goal, so team 1-i scores
        }
    }

    void Score(int team)
    {
        scores[team]++;
        scoreText = "[" + gameManager.GetTeamName(0) + " " + scores[0] + " | " + gameManager.GetTeamName(1) + " " + scores[1] + "]";

        foreach (PlayerData p in playerManager.Players)
            if (p.team == team) p.score++;

        if (goalSound != "") SoundManager.Play(goalSound, ballSpawnPoint.position);

        goalFlashUntil = Time.time + goalTitleTime;
        SetTitle("GOAL");

        resetball = true;
    }

    void SetTitle(string text)
    {
        if (!gameManager.GameTitle.Value.Equals(text)) gameManager.GameTitle.Value = text;
    }
}
