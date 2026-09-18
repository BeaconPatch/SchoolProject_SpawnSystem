using UnityEngine;

namespace BeaconPatch.SpawnSystem
{
    public interface ISpawnable
    {
        public abstract void Spawned();
        public abstract void SpawnReserved();
        public abstract void SpawnUnreserved();
    }
}
