using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct CombatInputs
{
    public bool FirePressed;
    public bool FireHeld;
    public bool FireReleased;
    public bool AltPressed;
    public bool AltHeld;
    public bool AltReleased;
    public bool ReloadPressed;
}

public struct ShotPellet : INetworkSerializable
{
    public Vector3 end;
    public Vector3 normal;
    public bool hit;
    public bool trail;
    public int decal;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref end);
        serializer.SerializeValue(ref normal);
        serializer.SerializeValue(ref hit);
        serializer.SerializeValue(ref trail);
        serializer.SerializeValue(ref decal);
    }
}

public struct ShotFx : INetworkSerializable
{
    public Vector3 muzzle;
    public Vector3 recoil;
    public float backKick;
    public float rotKick;
    public bool playRecoil;
    public bool doMuzzle;
    public ShotPellet[] pellets;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref muzzle);
        serializer.SerializeValue(ref recoil);
        serializer.SerializeValue(ref backKick);
        serializer.SerializeValue(ref rotKick);
        serializer.SerializeValue(ref playRecoil);
        serializer.SerializeValue(ref doMuzzle);

        int count = pellets == null ? 0 : pellets.Length;
        serializer.SerializeValue(ref count);
        if (serializer.IsReader) pellets = new ShotPellet[count];
        for (int i = 0; i < count; i++)
        {
            ShotPellet pellet = pellets[i];
            pellet.NetworkSerialize(serializer);
            pellets[i] = pellet;
        }
    }
}

public struct CombatHit
{
    public ShotPellet pellet;
    public bool isPlayer;
    public ulong playerId;
}

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] PlayerCharacter character;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] Transform cam;

    [SerializeField] LayerMask shootLayer;
    [SerializeField] float upForceMult = 0.5f;
    [SerializeField] int playerHitDecalIndex;
    [SerializeField] int crateHitDecalIndex;
    [SerializeField] string hitSound = "hitmarker";

    CombatInputs currentInputs;
    ItemClient prevItem;
    Player player;

    readonly RaycastHit[] shootHitsBuffer = new RaycastHit[32];
    readonly Collider[] explosionOverlapBuffer = new Collider[32];
    readonly HashSet<ulong> explosionHitNetIds = new HashSet<ulong>();

    public PlayerCharacter Character => character;
    public Transform Cam => cam;

    void Awake()
    {
        player = transform.root.GetComponent<Player>();
    }

    public void SetInputs(CombatInputs inputs, bool readyPull)
    {
        if (!readyPull) inputs = default;
        currentInputs = inputs;
    }

    public void UpdateCombat(PlayerState state, ItemClient item)
    {
        if (prevItem != item)
        {
            if (prevItem != null) prevItem.OnUnequip();
            prevItem = item;
        }

        item.SetInputs(currentInputs);
        item.Tick(this, state);
    }

    public bool TryTrace(Vector3 origin, Vector3 direction, float radius, float range, out RaycastHit hit)
    {
        int count = radius > 0
            ? Physics.SphereCastNonAlloc(origin, radius, direction, shootHitsBuffer, range, shootLayer)
            : Physics.RaycastNonAlloc(origin, direction, shootHitsBuffer, range, shootLayer);

        int best = -1;
        for (int i = 0; i < count; i++)
        {
            Transform root = shootHitsBuffer[i].transform.root;
            if (root == transform.root) continue;
            if (IsFriendly(root)) continue;
            if (best == -1 || shootHitsBuffer[i].distance < shootHitsBuffer[best].distance)
                best = i;
        }

        if (best < 0)
        {
            hit = default;
            return false;
        }

        hit = shootHitsBuffer[best];
        return true;
    }

    public int TraceAll(Vector3 origin, Vector3 direction, float radius, float range, List<RaycastHit> results)
    {
        results.Clear();
        int count = radius > 0
            ? Physics.SphereCastNonAlloc(origin, radius, direction, shootHitsBuffer, range, shootLayer)
            : Physics.RaycastNonAlloc(origin, direction, shootHitsBuffer, range, shootLayer);

        for (int i = 0; i < count; i++)
        {
            Transform root = shootHitsBuffer[i].transform.root;
            if (root == transform.root) continue;
            if (IsFriendly(root)) continue;
            results.Add(shootHitsBuffer[i]);
        }

        return results.Count;
    }

    public CombatHit HitscanHit(RaycastHit hit, Vector3 shootDir, float damage, float impactForcePlayer, float impactForceObject, float ragdollForceMult, int decal)
    {
        return ApplyHit(hit, shootDir, damage, impactForcePlayer, impactForceObject, ragdollForceMult, addAttackerVelocity: false, trail: true, decal);
    }

    public CombatHit MeleeHit(RaycastHit hit, Vector3 shootDir, float damage, float impactForcePlayer, float impactForceObject, int decal)
    {
        return ApplyHit(hit, shootDir, damage, impactForcePlayer, impactForceObject, 1f, addAttackerVelocity: true, trail: false, decal);
    }

    CombatHit ApplyHit(RaycastHit hitObject, Vector3 shootDir, float damage, float impactForcePlayer, float impactForceObject, float ragdollForceMult, bool addAttackerVelocity, bool trail, int decal)
    {
        Transform hitRoot = hitObject.transform.root;
        int decalIndex = decal;
        bool isPlayer = false;
        ulong playerId = 0;

        if (hitRoot.GetComponent<Player>())
        {
            float hitDamage = hitObject.transform.CompareTag("Head") ? damage * 2f : damage;
            Vector3 force = impactForcePlayer == 0f ? Vector3.zero : shootDir * impactForcePlayer + Vector3.up * upForceMult;
            Vector3 propForce = shootDir * impactForceObject * 0.4f * ragdollForceMult;

            if (addAttackerVelocity)
            {
                force += character.State.Velocity;
                propForce += character.State.Velocity;
            }

            playerId = hitRoot.GetComponent<NetworkObject>().OwnerClientId;
            PlayerManager.instance.DealDamageServerRpc(playerId, hitDamage, force, propForce);
            SoundManager.Play(hitSound);
            decalIndex = playerHitDecalIndex;
            isPlayer = true;
        }
        else if (hitRoot.TryGetComponent(out ItemCrate crate))
        {
            crate.BreakCrateServerRpc();
            decalIndex = crateHitDecalIndex;
        }
        else if (hitRoot.TryGetComponent(out NetworkProp prop))
        {
            Vector3 propImpulse = shootDir * impactForceObject;
            if (addAttackerVelocity) propImpulse += character.State.Velocity;
            prop.ApplyForce(propImpulse, hitObject.point);
            decalIndex = playerHitDecalIndex;
        }

        return new CombatHit {
            pellet = new ShotPellet {
                end = hitObject.point,
                normal = hitObject.normal,
                hit = true,
                trail = trail,
                decal = decalIndex
            },
            isPlayer = isPlayer,
            playerId = playerId
        };
    }

    public bool IsFriendly(Transform root)
    {
        if (root == transform.root) return false;
        if (!root.TryGetComponent(out Player other)) return false;
        int myTeam = player.Team.Value;
        return myTeam >= 0 && myTeam == other.Team.Value;
    }

    public void Explosion(Vector3 center, float explosionRadius, float explosionDamage, float explosionSelfDamage, float impactForcePlayer, float impactForceObject)
    {
        VFXManager.instance.PlayExplosion(center);
        explosionHitNetIds.Clear();

        int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, explosionOverlapBuffer, shootLayer);
        for (int i = 0; i < count; i++)
        {
            Collider col = explosionOverlapBuffer[i];
            Transform root = col.transform.root;
            if (!root.TryGetComponent(out NetworkObject netObj)) continue;
            if (explosionHitNetIds.Contains(netObj.NetworkObjectId)) continue;

            Vector3 hitPoint = col.ClosestPoint(center);
            Vector3 toHit = hitPoint - center;
            float dist = toHit.magnitude;

            if (dist > 0.0001f
                && Physics.Raycast(center, toHit / dist, out RaycastHit losHit, dist, shootLayer)
                && losHit.transform.root != root
                && !losHit.transform.root.TryGetComponent<Player>(out _))
                continue;

            float falloff = 1f - Mathf.Pow(Mathf.Clamp01(dist / explosionRadius), 4);
            if (falloff <= 0.0001f) continue;

            explosionHitNetIds.Add(netObj.NetworkObjectId);
            Vector3 blastDir = dist > 0.0001f ? toHit / dist : Vector3.up;

            if (root.GetComponent<Player>() != null)
            {
                bool friendly = root == transform.root || IsFriendly(root);
                float dmg = (friendly ? explosionSelfDamage : explosionDamage) * falloff;
                Vector3 playerForce = impactForcePlayer == 0f ? Vector3.zero : blastDir * impactForcePlayer * falloff;
                Vector3 ragdollForce = impactForceObject == 0f ? Vector3.zero : blastDir * impactForceObject * falloff;
                PlayerManager.instance.DealDamageServerRpc(netObj.OwnerClientId, dmg, playerForce, ragdollForce);
                if (!friendly) SoundManager.Play(hitSound);
            }
            else if (root.TryGetComponent(out ItemCrate crate))
            {
                crate.BreakCrateServerRpc();
            }
            else if (root.TryGetComponent(out NetworkProp prop) && impactForceObject != 0f)
            {
                prop.ApplyForce(blastDir * (impactForceObject * falloff), hitPoint);
            }
        }
    }

    public void PlayShotFx(ShotFx fx)
    {
        PlayShotFxLocal(fx);
        SendShotServerRpc(fx);
    }

    public void ReplicateAttack()
    {
        ReplicateAttackServerRpc();
    }

    void PlayShotFxLocal(ShotFx fx)
    {
        if (fx.playRecoil)
            animations.Shoot(fx.recoil, fx.backKick, fx.rotKick);

        if (fx.pellets == null) return;

        for (int i = 0; i < fx.pellets.Length; i++)
        {
            ShotPellet pellet = fx.pellets[i];
            if (fx.doMuzzle && i == 0) VFXManager.instance.PlayMuzzleFlash(fx.muzzle);
            if (pellet.trail) VFXManager.instance.PlayTrail(fx.muzzle, pellet.end);
            if (pellet.hit) VFXManager.instance.PlayDecal(pellet.end, pellet.normal, pellet.decal);
        }
    }

    [Rpc(SendTo.Server)]
    void SendShotServerRpc(ShotFx fx, RpcParams rpcParams = default)
    {
        SendShotClientRpc(fx, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void SendShotClientRpc(ShotFx fx, RpcParams rpcParams = default)
    {
        PlayShotFxLocal(fx);
    }

    [Rpc(SendTo.Server)]
    void ReplicateAttackServerRpc(RpcParams rpcParams = default)
    {
        ReplicateAttackClientRpc(RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void ReplicateAttackClientRpc(RpcParams rpcParams = default)
    {
        player.playerInventory.ClientInventory[player.playerState.InventoryIndex].PlayAttack();
    }
}
