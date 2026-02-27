using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public abstract class Obstacle : IsOnGround
{
    //Activate physics when hit then fade

    protected Rigidbody2D rb;

    protected SpriteRenderer img;
    protected float fade = 1f;

    public bool hit;

    protected abstract Vector2 Size { get; }
    public abstract bool FitToSlope { get; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        img = GetComponent<SpriteRenderer>();
    }
    public void Init()
    { 
        checkIsOnGround = false;
        isOnGround = true;
    }
    void Update()
    {
        if (fade < 1)
        {
            if (fade < 0)
            {
                if(!GameManager.MultiplayerGame || IsHost) Destroy(gameObject);
            }
            else
            {
                img.color = ColorManipulation.FadeColor(img.color, fade);
                fade -= Time.deltaTime / 3f;
            }
        }
    }
    /*private void OnDrawGizmos()
    {
        Gizmos.DrawCube(transform.position, Size);
    }*/
    public virtual void ThisObstacleHit(bool notifyTerrain = true)
    {
        //print("Hit");
        if(!GameManager.MultiplayerGame || IsHost)
        {
            UnFreezeRB();
            hit = true;

            checkIsOnGround = true;
        }

        StartCoroutine(FadeAfterDelay());

        if (notifyTerrain) TerrainManager.Singleton.ObstacleHit(this);

        if (GameManager.MultiplayerGame && IsHost) ObstacleHitClientRpc();
    }
    [ClientRpc]
    protected virtual void ObstacleHitClientRpc() //don't notify terrain since client obstacles are managed by host
    {
        //print("Obst hit. host: " + IsHost);
        if (!IsHost) ThisObstacleHit(false);
    }

    public bool CheckIfDislodged()
    {
        //print(name + " check if dislodged");

        LayerMask mask = LayerMask.GetMask("Ground");
        //print("Check "+(new Vector2(transform.position.x, transform.position.y) - (Size / 2)).ToString());
        Debug.DrawLine(new Vector2(transform.position.x, transform.position.y) - (Size / 2), new Vector2(transform.position.x, transform.position.y) + (Size / 2), Color.red, 15);
        if (!Physics2D.OverlapBox(transform.position, Size, 0, mask))
        {
            ThisObstacleHit();
            return true;
        }
        return false;
    }

    protected IEnumerator FadeAfterDelay()
    {
        yield return new WaitForSeconds(3);

        fade -= Time.deltaTime / 3f;
    }

    protected void UnFreezeRB()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
    }
}
