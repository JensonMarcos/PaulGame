using UnityEngine;

public class PaulBoss : Boss
{
    [SerializeField] float damage = 25f;
    [SerializeField] float range = 5f;
    [SerializeField] float attackRadius = 0.5f;
    [SerializeField] float force = 15f;
    [SerializeField] float cooldown = 0.6f;

    float nextAttackTime;

    public override void UpdateCombat(PlayerState _state)
    {
        if(!inputs.FirePressed || Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + cooldown;

        Transform _cam = combat.Cam;
        if(combat.TryTrace(_cam.position, _cam.forward, attackRadius, range, out RaycastHit _hit))
            combat.MeleeHit(_hit, _cam.forward, damage, force, force, 0);
    }
}
