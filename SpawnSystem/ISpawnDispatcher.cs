using UnityEngine;

namespace BeaconPatch.SpawnSystem
{
    public interface ISpawnDispatcher
    {
        public void OnSpawnAbandoned(GameObject objectToSpawn, SpawnPoint spawnPoint);
    }
}
