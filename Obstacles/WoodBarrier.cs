using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WoodBarrier : Obstacle
{
    //Activate physics when hit then fade

    protected override Vector2 Size { get => new Vector2(0.4f, 1.2f); }

    public override bool FitToSlope { get => false; }

    //Also hit when collide with tank or others
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!hit && collision.collider.gameObject.layer == Layers.Tanks && controlMyState) ThisObstacleHit();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //Bullet hit
        if (!hit && controlMyState)
        {
            ThisObstacleHit();
            Vector2 closestPt = GetComponent<BoxCollider2D>().ClosestPoint(collision.transform.position);
            GetComponent<Rigidbody2D>().linearVelocity = (transform.position - new Vector3(closestPt.x, closestPt.y)).normalized * collision.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
        }
    }
}
