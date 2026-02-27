using System.Linq;
using Unity.Burst.Intrinsics;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class NetworkedTank : NetworkBehaviour
{
    private Tank myTank;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        myTank = GetComponent<Tank>();
        GetComponent<PausableRigidbody2D>().Pause();
    }

    //---------Client Turn Rpcs--------
    [ClientRpc]
    public void MyTanksTurnClientRpc()
    {
        if (IsHost) return;

        print("Network: "+myTank.name + "'s turn!");

        myTank.MyTurn();
    }
    [ClientRpc]
    public void EndOfMyTanksTurnClientRpc()
    {
        if (IsHost) return;

        print("Network: " + myTank.name+"'s turn ended!");

        myTank.myTurn = false;
        GameManager.Singleton.bulletPreviewPool.DeactivateAllBullets();
    }
    //--------Client Event Rpcs-----------
    [ClientRpc]
    public void TankTryBreakClientRpc(float damage, float hitX, float hitY, float dstFract, float randVal1, float randVal2, bool endOfTurn = false) //Called from network bullet so execute on host as well
    {
        if (dstFract > 0.6f && randVal1 > 0.8f) myTank.SetOnFire(IsHost);

        Vector2 hitPos = new(hitX, hitY);
        //If bullet collision close to track
        float dstToLine = MathUtil2D.DstPointToLine(myTank.LeftTrackPos, myTank.RightTrackPos, hitPos);
        float dstToTrackEnd = Mathf.Min(Vector3.Distance(hitPos, myTank.LeftTrackPos), Vector3.Distance(hitPos, myTank.RightTrackPos));

        //Need dst to line segment because line goes forever past the track pts
        //If hit pos is closer to the middle of the track than the edges then both Dots will be positive
        float dstToLineSegment = (Vector3.Dot(transform.right + myTank.LeftTrackPos, hitPos) > 0 && Vector3.Dot(-transform.right + myTank.RightTrackPos, hitPos) > 0) ? dstToLine : dstToTrackEnd;


        if (dstToLineSegment < 0.3f && randVal2 > 0.7f) myTank.BreakTrack(IsHost);
    }
    [ClientRpc]
    public void DestroyDisconnectedPlayerClientRpc()
    {
        myTank.JustDamageTank(9999, endOfTurn:true, spawnDamageText:false);
    }

    [ClientRpc]
    public void TankDestroyedClientRpc()
    {
        if (!IsHost) myTank.TankDestroyedAppearence();
    }

    //-------Client Rpcs for powerups-----
    [ClientRpc]
    public void TurnInvisibleClientRpc()
    {
        if(!IsHost) myTank.TurnInvisible();
    }
    [ClientRpc]
    public void TankGotShieldClientRpc(bool largeShield)
    {
        if (IsHost) return;

        myTank.shieldPowerup = true;
        myTank.largeShield = largeShield;
        myTank.CanPlaceShield();
    }
    [ClientRpc]
    public void PlaceShieldClientRpc()
    {
        if (IsHost) return;

        //print("TURN OFF P KEY!!!!!!!!!");

        myTank.pKey.SetActive(false);
    }
    [ClientRpc]
    public void DestroyShieldClientRpc() => myTank.DestroyShield();
}
