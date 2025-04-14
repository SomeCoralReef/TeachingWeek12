using UnityEngine;

public class cameraFollow : MonoBehaviour
{
    public Transform player;
    public float followSpeed = 6f; // Controls how fast the camera trails behind
    public float maxDistanceAwayFromPlayer = 14f;

    void Update()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogWarning("Player not found!");
                return;
            }
        }

        Vector3 cameraPos = transform.position;
        Vector3 targetPos = new Vector3(player.position.x, player.position.y, -10f);

        // Lerp towards the player for smooth trailing
        Vector3 lerpedPos = Vector3.Lerp(cameraPos, targetPos, followSpeed * Time.deltaTime);

        // Clamp the camera so it doesn't get too far from the player
        Vector3 offset = lerpedPos - player.position;
        if (offset.magnitude > maxDistanceAwayFromPlayer)
        {
            offset = offset.normalized * maxDistanceAwayFromPlayer;
            lerpedPos = player.position + offset;
            lerpedPos.z = cameraPos.z; // Maintain original Z
        }

        transform.position = lerpedPos;
    }
}
