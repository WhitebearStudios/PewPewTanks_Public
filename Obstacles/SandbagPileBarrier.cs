using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SandbagPileBarrier : Obstacle
{
    [SerializeField] GameObject sandbagPrefab;

    protected override Vector2 Size { get => new Vector2(3, 1.4f); }

    public override bool FitToSlope { get => true; }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!hit && collision.collider.gameObject.layer == Layers.Tanks && controlMyState) ThisObstacleHit();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //Bullet
        if (!hit && controlMyState) ThisObstacleHit();
    }

    public override void ThisObstacleHit(bool notifyTerrain = true)
    {
        //This gets called before obj realizes it spawned in as a networkobj because unity reasons so we need a check
        if (GameManager.MultiplayerGame && !spawned) return;

        if (notifyTerrain) TerrainManager.Singleton.ObstacleHit(this);

        //Split into individual sandbags
        if (GameManager.MultiplayerGame) GetComponent<NetworkObject>().Despawn(true);

        Destroy(gameObject);
        rb.simulated = false;

        List<SandbagBarrier> bags = new()
        {
            Instantiate(sandbagPrefab, new Vector3(0, 0.38f) + transform.position, Quaternion.identity).GetComponent<SandbagBarrier>(),
            Instantiate(sandbagPrefab, new Vector3(-0.7f, -0.28f) + transform.position, Quaternion.identity).GetComponent<SandbagBarrier>(),
            Instantiate(sandbagPrefab, new Vector3(-0.7f, 0.28f) + transform.position, Quaternion.identity).GetComponent<SandbagBarrier>()
        };

        for (int i = 0; i < bags.Count; i++)
        {
            if(GameManager.MultiplayerGame) bags[i].GetComponent<NetworkObject>().Spawn();
            bags[i].ThisObstacleHit(false);
        }
    }
}
