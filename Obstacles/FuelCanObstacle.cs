using UnityEngine;

public class FuelCanObstacle : Explosive
{
    public override bool explodeOnRegCollision => false;

    public override bool explodeOnExplosiveCollision => true;

    public override bool explodeIndirectExplosive => true;

    public override float explodeCraterSize => 4f;
    public override float explodeDRadius => 8f;

    public override float explodeDamage => 20;


    void OnCollisionEnter2D(Collision2D collision)
    {
        //This happens when fuel can spawns on client but hasn't been diasbled yet-----This happens because the game starts asynchronously so not everything it at the same place at the same time
        if (GameManager.IsClientOnGame || !GameManager.Singleton.gameStarted || !controlMyState) return; 

        if (collision.gameObject.CompareTag("Tank") && collision.gameObject.GetComponent<Tank>() == GameManager.Singleton.PlayerInTurn)
        {
            if (GameManager.Singleton.FuelBoost(GameConstants.fuelBoost))
            {
                TerrainManager.Singleton.RemoveExplosive(this);
                Destroy(gameObject);
            }
        }
    }

    public override void Explode(Tank reasonForExplosion)
    {
        base.Explode(reasonForExplosion);

        TerrainManager.Singleton.FuelExplodeSound();
    }
}
