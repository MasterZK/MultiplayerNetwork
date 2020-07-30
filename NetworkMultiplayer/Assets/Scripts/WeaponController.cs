using Mirror;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject bullet;
    [SerializeField] private float bulletSpeed;
    [SerializeField] private Transform shootPosition;

    private void Update()
    {
        if (this.isLocalPlayer)
            if (Input.GetKeyDown(KeyCode.Mouse0))
                CmdShoot();
    }

    private Vector3 GetShootDirection()
    {
        var ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        return ray.direction.normalized;
    }

    [Command]
    void CmdShoot()
    {
        GameObject r = Instantiate(bullet, shootPosition.position, transform.rotation);
        r.transform.LookAt(this.transform.position + GetShootDirection());
        r.GetComponent<Rigidbody>().velocity = GetShootDirection() * bulletSpeed;
        NetworkServer.Spawn(r);
        Destroy(r, 2.0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawRay(playerCamera.ScreenPointToRay(Input.mousePosition));
    }
}
