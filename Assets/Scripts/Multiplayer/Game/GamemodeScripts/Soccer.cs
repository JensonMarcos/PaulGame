using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Soccer : Gamemode
{
    [SerializeField] GameObject ballPrefab;
    [SerializeField] Transform ballSpawnPoint;
    [SerializeField] Collider[] goals;
    [SerializeField] string goalSound = "";
    [SerializeField] string explosionSound = "explosion";
    [SerializeField] float goalTitleTime = 1.5f;

    [Header("Explosion Knockback")]
    [SerializeField] LayerMask explosionLayer;
    [SerializeField] float explosionRadius = 10f;
    [SerializeField] float explosionForcePlayer = 20f;
    [SerializeField] float explosionForceProp = 80f;

    const float RoundDuration = 90f;

    readonly Collider[] explosionOverlapBuffer = new Collider[32];
    readonly HashSet<ulong> explosionHitNetIds = new HashSet<ulong>();

    NetworkProp ball;
    int[] scores = new int[2];
    float endTime;
    float goalFlashUntil;
    string scoreText;
    bool resetBall;

    public override bool RespawnOnDeath => true;

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;
        scores[0] = 0;
        scores[1] = 0;
        playerManager.AssignTeamsRandomly(2);

        GameObject obj = Instantiate(ballPrefab, ballSpawnPoint.position, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn(true);
        gameManager.worldObjects.Add(obj);
        ball = obj.GetComponent<NetworkProp>();

        RefreshScoreText();
        SetTitle(scoreText);
    }

    public override void End()
    {
        if (ball == null) return;

        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        gameManager.worldObjects.Remove(ball.gameObject);
        if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        ball = null;
    }

    public override void Tick()
    {
        if (Time.time <= goalFlashUntil)
        {
            if (endTime - Time.time <= 0f)
            {
                gameManager.DeclareWinners(playerManager.Players, true);
                EndRound();
            }
            return;
        }

        if (resetBall)
        {
            ball.rb.position = ballSpawnPoint.position;
            ball.rb.linearVelocity = Vector3.zero;
            ball.rb.angularVelocity = Vector3.zero;
            resetBall = false;
        }

        int secondsLeft = Mathf.Max(0, (int)(endTime - Time.time));
        SetTitle(scoreText + " : " + secondsLeft);

        Vector3 ballPos = ball.rb.position;
        for (int i = 0; i < 2; i++)
        {
            if (goals[i].ClosestPoint(ballPos) == ballPos)
                Score(1 - i);
        }

        if (endTime - Time.time <= 0f)
        {
            gameManager.DeclareWinners(playerManager.Players, true);
            EndRound();
        }
    }

    void Score(int team)
    {
        scores[team]++;
        RefreshScoreText();

        foreach (PlayerData player in playerManager.Players)
            if (player.team == team) player.score++;

        VFXManager.instance.PlayExplosion(ball.rb.position);
        SoundManager.Play(explosionSound, ball.rb.position);
        ExplosionKnockback(ball.rb.position);
        ball.rb.linearVelocity = Random.onUnitSphere * explosionForceProp;

        if (goalSound != "") SoundManager.Play(goalSound, ballSpawnPoint.position);

        goalFlashUntil = Time.time + goalTitleTime;
        SetTitle("GOAL");
        resetBall = true;
    }

    void RefreshScoreText()
    {
        scoreText = "[" + gameManager.GetTeamName(0) + " " + scores[0] + " | " + gameManager.GetTeamName(1) + " " + scores[1] + "]";
    }

    void ExplosionKnockback(Vector3 center)
    {
        explosionHitNetIds.Clear();

        int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, explosionOverlapBuffer, explosionLayer);
        for (int i = 0; i < count; i++)
        {
            Collider col = explosionOverlapBuffer[i];
            Transform root = col.transform.root;
            if (!root.TryGetComponent(out NetworkObject netObj)) continue;
            if (explosionHitNetIds.Contains(netObj.NetworkObjectId)) continue;
            if (root == ball.transform.root) continue;

            Vector3 hitPoint = col.ClosestPoint(center);
            Vector3 toHit = hitPoint - center;
            float dist = toHit.magnitude;
            if (dist < 0.001f) continue;

            if (Physics.Raycast(center, toHit / dist, out RaycastHit losHit, dist, explosionLayer) && losHit.transform.root != root && !losHit.transform.root.TryGetComponent<Player>(out _))
                continue;

            float falloff = 1f - Mathf.Pow(Mathf.Clamp01(dist / explosionRadius), 4);
            if (falloff <= 0.0001f) continue;

            explosionHitNetIds.Add(netObj.NetworkObjectId);

            Vector3 blastDir = toHit / dist;

            if (root.TryGetComponent(out Player player))
            {
                if (explosionForcePlayer != 0f) player.RecieveForceClientRpc(blastDir * explosionForcePlayer * falloff);
            }
            else if (root.TryGetComponent(out NetworkProp prop) && explosionForceProp != 0f)
            {
                prop.ApplyForce(blastDir * (explosionForceProp * falloff), hitPoint);
            }
        }
    }
}
