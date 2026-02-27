using Unity.Netcode;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class NetworkedBullet : NetworkBehaviour
{
    enum BulletType { normal, rocket, mortar }

    bool hitSomething;

    [SerializeField] GameObject trailPrefab;
    [SerializeField] GameObject explosionPrefab;
    Transform bulletTrailsParent;
    public Tank Shooter { get; private set; }

    const float trailFreq = 0.4f;
    float trailCounter = trailFreq;

    BulletType bulletType;
    bool triggerNextTurn;

    private void Awake()
    {
        bulletTrailsParent = GameObject.Find("------Bullet Trails").transform;

        if (name == "Net_Rocket Variant(Clone)") bulletType = BulletType.rocket;
        else if (name == "Net_MortarBomb Variant(Clone)") bulletType = BulletType.mortar;
        else bulletType = BulletType.normal;

        //Particle system not playing on its own?
        if (bulletType == BulletType.rocket) GetComponent<ParticleSystem>().Play();

        //Destroy(gameObject, 20);
    }
    public void Init(Tank shooter, bool isTrigger, Vector3 vel)
    {
        this.Shooter = shooter;
        triggerNextTurn = isTrigger;
        GetComponent<Rigidbody2D>().linearVelocity = vel;

        //print("Shooter is "+shooter.name);
    }
    private void Start()
    {
        if (TerrainManager.Singleton.PointUnderTerrain(transform.position) && !hitSomething)
        {
            print("Bullet spawned under terrain");

            hitSomething = true;

            //Make sure no underground bullets
            NormalCollision();

            //Disable bullet object
            Destroy(gameObject);

            //Disable collider to prevent duplicate impacts
            GetComponent<Rigidbody2D>().simulated = false;

            DoExplosion();
        }
    }

    void FixedUpdate()
    {
        if (hitSomething || !IsHost) return;

        Vector3 v = GetComponent<Rigidbody2D>().linearVelocity;
        transform.eulerAngles = Vector3.forward * Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        
    }
    void Update()
    {
        if (hitSomething || !IsHost) return;

        if (transform.position.y < -500 || TerrainManager.Singleton.PointVeryUnderTerrain(transform.position))
        {
            hitSomething = true;

            Debug.LogError("Bullet very under terrain: must have passed through collider");

            //Make sure no underground bullets
            NormalCollision();

            //Disable bullet object
            Destroy(gameObject);

            //Disable collider to prevent duplicate impacts
            GetComponent<Rigidbody2D>().simulated = false;
        }

        //Do trails
        trailCounter -= Time.deltaTime;
        if (trailCounter <= 0)
        {
            if (GameSettings.bulletTrails)
            {
                GameObject bT = Instantiate(trailPrefab);
                bT.GetComponent<NetworkObject>().Spawn();

                bT.transform.position = transform.position;
                bT.transform.rotation = transform.rotation;
                bT.GetComponent<NetworkObject>().TrySetParent(bulletTrailsParent);
            }

            trailCounter = trailFreq;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitSomething || !IsHost) return;

        hitSomething = true;

        if (Time.time - GameManager.Singleton.timeOfLastTurn < 0.01f)
        {
            //print("Bullet Duplicate impact!");
            return;
        }

        //print("Shield hit!");

        if (other.gameObject.layer == Layers.Shields)
        {
            //Tank is parent of parent of shield
            GameObject collidee = other.transform.parent.parent.gameObject;

            //Pass through own shield
            if (collidee == Shooter.gameObject)
            {
                hitSomething = false;
                return;
            }

            //print("Collided with " + collidee.name);

            //Hit other tank's shield

            DoExplosion();

            collidee.GetComponent<NetworkedTank>().DestroyShieldClientRpc();

            //Only middle bullet triggers next turn (trigger once only)
            GameManager.Singleton.BulletHit((int)bulletType, "Shield Hit!", triggerNextTurn);
        }
        else
        {
            GameObject collidee = other.gameObject;
            Vector3 collisionPos = other.ClosestPoint(transform.position);

            //print("Collided with " + collidee.name);

            //Pass through falling powerups
            if (collidee.layer == Layers.Powerups && collidee.transform.GetChild(0).gameObject.activeSelf)
            {
                hitSomething = false;
                return;
            }

            DoExplosion();

            if (collidee.CompareTag("Tank") && collidee.GetComponent<Tank>().health > 0)
            {
                //print("Direct hit!");
                Tank hurtTank = collidee.GetComponent<Tank>();

                if (hurtTank.Upgrade == GameManager.TankUpgrades.SuperArmor)
                {
                    //Redirect bullet
                    Vector3 collisionDir = (collisionPos - collidee.transform.position).normalized;
                    GetComponent<Rigidbody2D>().linearVelocity = GetComponent<Rigidbody2D>().linearVelocity.magnitude * 0.75f * collisionDir;

                    //Ricochet sound
                    transform.GetChild(0).GetComponent<AudioSource>().Play();

                    hitSomething = false;
                    return;
                }
                else
                {
                    if (GameManager.Singleton.Teams.TeamsFriendly(hurtTank.settings.team, Shooter.settings.team) && hurtTank != Shooter)
                    {
                        if (bulletType == BulletType.mortar) NormalCollision();
                        else GameManager.Singleton.BulletHit((int)bulletType, "Teammate Hit!", triggerNextTurn);
                        Shooter.aiMisses++;
                    }
                    else
                    {
                        hurtTank.aiHitLastTurn = true;

                        Vector3 softSpotPos = collidee.transform.GetChild(1).position;
                        float dstFract = Mathf.Abs(GameConstants.maxDstFromSoftSpot - Vector3.Distance(collisionPos, softSpotPos)) / GameConstants.maxDstFromSoftSpot;
                        float damageBonus = (dstFract - 0.5f) * 2 * GameConstants.softSpotBonus;

                        float damage = damageBonus + GameConstants.directHitDamage * Shooter.settings.damageMultiplier * GetComponent<Rigidbody2D>().linearVelocity.magnitude / GameConstants.bulletSpeed;
                        float r1 = Random.value, r2 = Random.value;
                        hurtTank.JustDamageTank(damage, triggerNextTurn);

                        if(hurtTank.health > 0) hurtTank.GetComponent<NetworkedTank>().TankTryBreakClientRpc(damage, collisionPos.x, collisionPos.y, dstFract, r1, r2, triggerNextTurn);
                        else Shooter.stats.victims++;

                        //Still make crater, splash damage for mortar
                        if (bulletType == BulletType.mortar) NormalCollision(hurtTank);

                        GameManager.Singleton.BulletHit((int)bulletType, "Direct Hit!", triggerNextTurn && bulletType != BulletType.mortar);

                        Shooter.stats.damageDealt += (int)damage;
                        Shooter.stats.hits++;
                        //print(shooter.settings.name + " dealt " + (int)damage + " damage");
                    }
                }
            }
            else
            {
                NormalCollision();
            }
        }

        Destroy(gameObject);
    }
    //Hit something other than a player
    void NormalCollision(Tank dontDamageDirectHitTank)
    {
        //print("Vel: " + GetComponent<Rigidbody2D>().velocity.magnitude);

        //Make craters on all clients
        GameManager.Singleton.CraterClientRpc(transform.position, bulletType == BulletType.rocket ? GameConstants.bulletSplashRadius : bulletType == BulletType.mortar ? GameConstants.bulletSplashRadius * 0.7f : GameConstants.bulletSplashRadius * 0.4f);

        float r;
        if (bulletType == BulletType.rocket) r = GameConstants.bulletSplashRadius * GameConstants.rocketMultiplier;
        else if (bulletType == BulletType.mortar) r = GameConstants.bulletSplashRadius * 2;
        else r = GameConstants.bulletSplashRadius;
        GameManager.Singleton.DamageNearbyTanks(transform.position, r, GameConstants.bulletSplashDamage * Shooter.settings.damageMultiplier * GetComponent<Rigidbody2D>().linearVelocity.magnitude / GameConstants.bulletSpeed, Shooter, true, triggerNextTurn, dontDamageDirectHitTank);

        GameManager.Singleton.BulletHit((int)bulletType, triggerBullet: triggerNextTurn);
    }
    void NormalCollision() => NormalCollision(null);

    void DoExplosion()
    {
        //Create explosion
        GameObject e = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        if (GameManager.MultiplayerGame) e.GetComponent<NetworkObject>().Spawn();

        //Trigger other explosions
        TerrainManager.Singleton.ExplosionTrigger(transform.position, GameConstants.bulletSplashRadius, Shooter);
    }
}
