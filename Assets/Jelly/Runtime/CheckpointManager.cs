using UnityEngine;

namespace Jelly
{
    public class CheckpointManager : MonoBehaviour
    {
        public string ActiveCheckpointId { get; private set; }
        public Vector3 RespawnPosition { get; private set; }
        public float RespawnMass { get; private set; } = 100f;

        public void InitializeStart(Vector3 startPos, float startMass)
        {
            ActiveCheckpointId = "START";
            RespawnPosition = startPos;
            RespawnMass = startMass;
        }

        public void ActivateCheckpoint(string id, Vector3 pos, float mass)
        {
            ActiveCheckpointId = id;
            RespawnPosition = pos;
            RespawnMass = Mathf.Max(20f, mass);
        }

        public void RespawnPlayer(JellyController player)
        {
            if (player == null) return;
            player.ResetState(RespawnPosition, Quaternion.identity, RespawnMass);
        }
    }
}
