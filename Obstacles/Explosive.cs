using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class Explosive : IsOnGround
{
    [SerializeField] GameObject explosionPrefab;

    public abstract bool explodeOnRegCollision { get; }
    public abstract bool explodeOnExplosiveCollision { get; }
    public abstract bool explodeIndirectExplosive { get; }
    public abstract float explodeCraterSize { get; }
    public abstract float explodeDRadius { get; }
    public abstract float explodeDamage { get; }

    public bool exploded = false;

    public virtual void Explode(Tank reasonForExplosion)
    {
        exploded = true;
        
        GameManager.Singleton.Crater(transform.position, explodeCraterSize);

        //Create explosion
        GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        if (GameManager.MultiplayerGame) explosion.GetComponent<NetworkObject>().Spawn();

        if (explodeDamage > 0) GameManager.Singleton.DamageNearbyTanks(transform.position, explodeDRadius, explodeDamage, reasonForExplosion, showMessage: false);

        Destroy(gameObject);
    }

    public bool ExplodeIndirect(Vector3 pos, float radius, Tank reasonForExplosion)
    {
        if (!explodeIndirectExplosive || exploded) return false;

        if (Vector3.Distance(transform.position, pos) < radius)
        {
            Explode(reasonForExplosion);
            return true;
        }
        else return false;
    }

    /*IEnumerable DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        TerrainManager.Singleton.RemoveExplosive(this);
        Destroy(gameObject);
    }*/
}
