using UnityEngine;

namespace BeaconPatch.SpawnSystem
{
    public struct ElectionData
    {
        public Vector3 playerPosition { get; private set; }
        
        
        public ElectionData(Vector3 playerPos)
        {
            playerPosition = playerPos;
        }
    }
}
