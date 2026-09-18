using BeaconPatch.GameObjectPoolSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace BeaconPatch.SpawnSystem
{
    public class SpawnPoint : MonoBehaviour
    {
        #region Parameters
        [Header("Registration")]
        [SerializeField] [Tooltip("")]
        private bool automaticallyRegister = true;
        
        [Header("Restrictions")]
        [SerializeField] [Tooltip("")]
        private ESpawnPointUsage spawnPointUsage = ESpawnPointUsage.GenericSpawn;
        [SerializeField] [Tooltip("")]
        private string[] requiredTags;
        [SerializeField] [Tooltip("")]
        private string[] blockedTags;
        [SerializeField] [Tooltip("Determine if the reservation should be held until the player is no longer in " +
                                  "sight of this SpawnPoint.")]
        private bool preventSpawningInSight = true;

        [Header("Interval")]
        [SerializeField] [Tooltip("")]
        private float minSpawnDelay = 0.0f;
        [SerializeField] [Tooltip("When failing to spawn something, try again after this interval.")]
        private float spawnAttemptDelay = 1.0f;
        [SerializeField] [Tooltip("Total amount of attempt that this spawn point can try to spawn something. After " +
                                  "failing this amount of tie, the reservation is abandoned.")]
        private uint spawnAttempt = 5;
        
        [Header("Dedicated Spawn")]
        [SerializeField] [Tooltip("A model of GameObject, a prefab, to have this SpawnPoint spawn itself without" +
                                  "outside request.")]
        private GameObject dedicatedGameObject = null;
        [SerializeField] [Tooltip("Optionally, provide a reference that implement an ISpawnDispatcher interface where " +
                                  "the dedicated item will need to be requested from. If none is provided, this " +
                                  "SpawnPoint will attempt to fetch the item directly from a GameObjectPoolMaster.")]
        private GameObject dedicatedSpawnManager = null;
        [SerializeField] [Tooltip("If true and no dedicatedSpawnManager is provided, this SpawnPoint will create and " +
                                  "instance of the dedicatedGameObject model. Recommended only for GameObject " +
                                  "are not already given a GameObjectPool.")]
        private bool directlyInstantiate = false;
        [SerializeField] [Tooltip("Determine a limit of dedicatedGameObject this SpawnPoint can spawn. If negative, " +
                                  "the SpawnPoint will try to spawn them infinitely.")]
        private int dedicatedSpawnLimit = 1;
        [SerializeField] [Tooltip("Determine if the current count of spawned item should be reset whenever this " +
                                  "SpawnPoint is enabled.")]
        private bool enablingResetsSpawnLimit = true;
        [SerializeField] [Tooltip("Determine the duration of an initial delay before attempting to spawn the initial " +
                                  "dedicatedGameObject.")]
        private float dedicatedSpawnInitialDelay = 0.0f;
        #endregion Parameters

        #region References
        private GameObject _reservation;
        private ISpawnDispatcher _reservingSpawnDispatcher;
        private GameObjectPoolManager _poolManager;
        #endregion References

        #region Flags and Data
        public float electionScore { get; private set; }
        private uint _currentSpawnAttempt = 0;
        private ISpawnDispatcher _spawnDispatcher;
        private int _dedicatedSpawnSuccess = 0;
        private bool _reservationFromDedicatedSpawn = false;
        #endregion
        
        
        #region MonoBehaviour
        private void OnEnable()
        {
            GetReferences();
            if (SpawnElector.instance == null) return;
            
            if (automaticallyRegister)
                TryRegistering();
            
            if (enablingResetsSpawnLimit)
                ResetSpawnPoint();
        }


        private void OnDisable()
        {
            if (_reservation)
                StopSpawningAttempt(false);
            
            TryUnregistering();
        }

        private void Start()
        {
            TryInitializeDedicatedSpawn();
        }
        #endregion MonoBehaviour
        
        
        #region Initialization
        /// <summary>
        /// Attempt to register this SpawnPoint in the SpawnElector.
        /// </summary>
        public void TryRegistering()
        {
            if (SpawnElector.instance.RequestSpawnPointRegistration(this))
                SpawnElector.instance.onElectionScoreUpdated += OnElectionScoreUpdated;
        }


        /// <summary>
        /// Attempt to unregister this SpawnPoint from the SpawnElector.
        /// </summary>
        /// <param name="deactivate">If true, also deactivate this SpawnPoint GameObject.</param>
        public void TryUnregistering(bool deactivate = false)
        {
            if (!SpawnElector.instance || !SpawnElector.instance.IsSpawnPointRegistered(this)) return;
            
            if (SpawnElector.instance.RequestSpawnPointUnregistration(this))
            {
                SpawnElector.instance.onElectionScoreUpdated -= OnElectionScoreUpdated;
                if (deactivate) gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning(this + " tried to get itself unregistered from the SpawnElector but failed.");
            }
        }


        private void TryInitializeDedicatedSpawn()
        {
            if (spawnPointUsage == ESpawnPointUsage.GenericSpawn ||
                dedicatedGameObject == null) return;
            
            if (dedicatedSpawnManager && !dedicatedSpawnManager.TryGetComponent<ISpawnDispatcher>(out ISpawnDispatcher _spawnManager))
            {
                Debug.LogWarning(this + " is set to associate with a GameObject that does not contains a ISpawnDispatcher.");
                dedicatedSpawnManager = null;
            }
            
            Invoke(nameof(TryReserveForDedicatedSpawn), dedicatedSpawnInitialDelay);
        }
        
        
        private void GetReferences()
        {
            _poolManager = GameObject.FindAnyObjectByType<GameObjectPoolManager>();
        }
        
        
        /// <summary>
        /// Reset this SpawnPoint, clearing any reservation and resetting from 0 the dedicated spawn.
        /// </summary>
        public void ResetSpawnPoint()
        {
            _dedicatedSpawnSuccess = 0;
            
            StopSpawningAttempt(false);
            RequestUnreservationForDedicatedSpawn();
            
            _dedicatedSpawnSuccess = 0;

            TryReserveForDedicatedSpawn();
        }
        #endregion

        #region Election
        private void OnElectionScoreUpdated(SpawnPoint spawnPoint, float newScore)
        {
            if (spawnPoint == this) electionScore = newScore;
        }
        #endregion Election

        #region Reservation
        /// <summary>
        /// Request this SpawnPoint to reserve itself to spawn a GameObject.
        /// </summary>
        /// <param name="objectToSpawn"></param>
        /// <param name="requestor"></param>
        /// <returns></returns>
        public bool RequestReservationForGameObject(GameObject objectToSpawn, ISpawnDispatcher requestor)
        {
            if (objectToSpawn == null || requestor == null) return false;

            if (spawnPointUsage == ESpawnPointUsage.DedicatedSpawn) return false;

            if (!CanBeReservedForGameObject(objectToSpawn)) return false;
            
            if (_reservation)
                if (spawnPointUsage != ESpawnPointUsage.DedicatedSpawn)
                    RequestUnreservationFromSpawnDispatcher(_reservingSpawnDispatcher);
            
            if (IsInvoking(nameof(TryReserveForDedicatedSpawn)))
                CancelInvoke(nameof(TryReserveForDedicatedSpawn));
            
            // Set object to spawn.
            _reservation = objectToSpawn;
            _reservation.transform.position = GetWaitingPosition();
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.SpawnReserved();
            }
            
            // Set manager
            _reservingSpawnDispatcher = requestor;
            
            // Start spawning process
            BeginSpawningAttempt();
            return true;
        }

        
        /// <summary>
        /// Request this SpawnPoint to remove any active reservation. Require to provide the original requestor 
        /// to let it know that its request has been abandoned. A request is automatically abandoned when disabling 
        /// or resetting this SpawnPoint.
        /// </summary>
        /// <param name="requestor"></param>
        /// <returns></returns>
        public bool RequestUnreservationFromSpawnDispatcher(ISpawnDispatcher requestor)
        {
            if (_reservingSpawnDispatcher != null &&
                _reservingSpawnDispatcher != requestor) return false;

            if (spawnPointUsage == ESpawnPointUsage.DedicatedSpawn) return false;
            
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.SpawnUnreserved();
            }
            
            StopSpawningAttempt(false);
            return true;
        }


        private void RequestUnreservationForDedicatedSpawn()
        {
            if (_reservation == null ||
                !_reservationFromDedicatedSpawn) return;
            
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.SpawnUnreserved();
            }
            
            _currentSpawnAttempt = 0;
            _reservationFromDedicatedSpawn = false;
            
            StopSpawningAttempt(false);
        }

        private void TryReserveForDedicatedSpawn()
        {
            if (dedicatedGameObject == null) return;

            if (spawnPointUsage == ESpawnPointUsage.GenericSpawn) return;
            
            if (_dedicatedSpawnSuccess == dedicatedSpawnLimit) return;
            
            if (_reservation) return;
            
            // Set object to spawn.
            if (directlyInstantiate)
            {
                _reservation = GameObject.Instantiate(dedicatedGameObject);
            }
            else
            {
                _reservation = _poolManager.RequestObject(dedicatedGameObject);
                if (_poolManager.GetPoolForGameObject(dedicatedGameObject) == null)
                {
                    Debug.LogWarning(this + " was requested to spawn " + dedicatedGameObject + " from the " +
                                     "GameObjectPoolManager, but it isn't declared into any of its GameObjectPools. " +
                                     "Abandoning all dedicated spawn attempts. " +
                                     "If this is intended, please check the 'skipGameObjectPoolQuery' parameters.");
                    _dedicatedSpawnSuccess = dedicatedSpawnLimit;
                    return;
                }
                
                if (_reservation == null) return;
            }
            
            _reservation.transform.position = GetWaitingPosition();
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.SpawnReserved();
            }
            
            // Set manager
            if (dedicatedSpawnManager)
                _reservingSpawnDispatcher = dedicatedSpawnManager.GetComponent<ISpawnDispatcher>();
            
            // Begin spawning process
            _reservationFromDedicatedSpawn = true;
            BeginSpawningAttempt();
        }

        private Vector3 GetWaitingPosition()
        {
            Vector3 waitingPosition = transform.position;
            waitingPosition.y -= 1000.0f;
            return waitingPosition;
        }
        
        
        private void DiscardReservation()
        {
            if (!_reservation) return;
            
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.SpawnUnreserved();
            }
            
            _reservation = null;
            _reservingSpawnDispatcher = null;
            _reservationFromDedicatedSpawn = false;
            _currentSpawnAttempt = 0;
        }
        #endregion Reservation
        
        #region Spawning
        private void BeginSpawningAttempt()
        {
            _currentSpawnAttempt = 0;
            InvokeRepeating(nameof(TrySpawning), 0.0f, spawnAttemptDelay);
        }
        
        
        private void StopSpawningAttempt(bool success)
        {
            CancelInvoke(nameof(TrySpawning));
            
            // Unassign the reference to the GameObject we were supposed to spawn and the responsible SpawnDispatcher.
            // Saving a temporary reference to allow potentially re-electing this spawn point to try again.
            GameObject oldReservation = _reservation;
            ISpawnDispatcher oldDispatcher = _reservingSpawnDispatcher;
            bool oldFromDedication = _reservationFromDedicatedSpawn;
            
            DiscardReservation();
            
            if (!success)
                RemoveObjectFromScene(oldFromDedication, oldReservation, oldDispatcher);
        }
        
        
        private void RemoveObjectFromScene(bool oldFromDedication, GameObject oldReservation, ISpawnDispatcher oldDispatcher)
        {
            if (oldFromDedication)
            {
                if (directlyInstantiate)
                    Destroy(oldReservation);
                else
                    _poolManager.ReturnObject(oldReservation);
            }
            else
            {
                oldDispatcher?.OnSpawnAbandoned(oldReservation, this);
            }
        }
        
        
        private void TrySpawning()
        {
            _currentSpawnAttempt++;

            if (CanSpawn())
            {
                DoSpawn();
                return;
            }
            
            if (_currentSpawnAttempt >= spawnAttempt) StopSpawningAttempt(false);
        }
        
        
        private void DoSpawn()
        {
            Vector3 spawnPosition = transform.position;

            if (_reservation.TryGetComponent<NavMeshAgent>(out NavMeshAgent navMeshAgent))
            {
                float heightOffset = 0.0f;
                foreach (Collider collider in _reservation.GetComponents<Collider>())
                {
                    if (collider.enabled && !collider.isTrigger)
                    {
                        heightOffset = Mathf.Max(collider.bounds.extents.y, heightOffset);
                    }
                }
                
                spawnPosition.y += heightOffset + 0.0001f;
            }
            else
            {
                spawnPosition.y += 1.0f;
            }
            
            _reservation.transform.position = spawnPosition;
            _reservation.gameObject.SetActive(true);
            
            foreach (ISpawnable spawnable in _reservation.GetComponents<ISpawnable>())
            {
                spawnable.Spawned();
            }
            
            StopSpawningAttempt(true);
            
            if (spawnPointUsage != ESpawnPointUsage.GenericSpawn &&
                _reservationFromDedicatedSpawn)
            {
                _dedicatedSpawnSuccess++;
                 
                // TODO: Initiate the spawn interval delay if dedicated.
                if (_dedicatedSpawnSuccess == dedicatedSpawnLimit) return;
                 
                Invoke(nameof(TryReserveForDedicatedSpawn), minSpawnDelay);
            }
        }
        #endregion Spawning
        
        #region Querries
        public bool CanBeReserved()
        {
            return (isActiveAndEnabled &&
                    _reservation == null &&
                    spawnPointUsage != ESpawnPointUsage.DedicatedSpawn);
        }


        public bool CanBeReservedForGameObject(GameObject target)
        {
            if (!CanBeReserved()) return false;
            if (target.CompareTag("Untagged")) return true;
            
            bool requirementFulfill = false;
            foreach (string requiredTag in requiredTags)
            {
                if (target.CompareTag(requiredTag))
                {
                    requirementFulfill = true;
                    break;
                }
            }

            if (!requirementFulfill) return false;

            foreach (string blockingTag in blockedTags)   
            {
                if (target.CompareTag(blockingTag))
                {
                    requirementFulfill = false;
                    break;
                }
            }
            
            return requirementFulfill;
        }


        private bool CanSpawn()
        {
            if (_reservation)
            {
                // Check if in sight.
                if (preventSpawningInSight && IsPlayerInSight()) return false;
                
                // Check if there's enough room to spawn the object.
                bool hasRoomToSpawn = true;
                foreach (Collider collider in _reservation.GetComponents<Collider>())
                {
                    if (collider.enabled && !collider.isTrigger)
                    {
                        var positionToCheck = GetPositionToCheck(collider);

                        if (Physics.CheckBox(positionToCheck, collider.bounds.extents, Quaternion.identity, 1 << _reservation.layer))
                        {
                            hasRoomToSpawn = false;
                            break;
                        }
                    }
                }
                return hasRoomToSpawn;
            }
            
            return false;
        }

        private Vector3 GetPositionToCheck(Collider collider)
        {
            Vector3 positionToCheck = transform.position;
            positionToCheck.y += collider.bounds.extents.y;
            positionToCheck.y += 0.0001f;
            return positionToCheck;
        }


        /// <summary>
        /// Check if this SpawnPoint has the player in Sight.
        /// </summary>
        /// <returns>Returns true if the line of sight is unobstructed.</returns>
        public bool IsPlayerInSight()
        {
            // Save on physics testing if nothing can be detected.
            LayerMask visibilityMask = SpawnElector.instance.GetVisibilityLayerMask();
            if (visibilityMask.value == 0) return true;
            
            if (!SpawnElector.instance.GetPlayerPosition(out Vector3 startPosition)) return false;
            startPosition.y += 1.0f;
            
            Vector3 endPosition = transform.position;
            endPosition.y += 1.0f;
            
            return !Physics.Linecast(startPosition, endPosition, visibilityMask);
        }
        #endregion Querries

        #region Getters
        public float GetElectionScore() { return electionScore; }
        #endregion Getters
        
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            GUIStyle style = new GUIStyle();
            Handles.color = Color.black;
            Handles.Label(transform.position, name, style);
            Handles.DrawWireDisc(transform.position, transform.up, 0.5f);
            
            if (_reservation)
            {
                bool hasFoundCollision = false;
                foreach (Collider collider in _reservation.GetComponents<Collider>())
                {
                    if (collider.enabled && !collider.isTrigger)
                    {
                        Vector3 positionToCheck = GetPositionToCheck(collider);
                        
                        if (Physics.CheckBox(positionToCheck, collider.bounds.extents * 0.5f, Quaternion.identity, 1 << _reservation.layer))
                        {
                            hasFoundCollision = true;
                            Gizmos.color = Color.red;
                        }
                        else
                        {
                            Gizmos.color = Color.green;
                        }
                        
                        Gizmos.DrawWireCube(positionToCheck, collider.bounds.size);
                    }
                }
                
                Handles.color = hasFoundCollision ? Color.red : Color.green;
                
                Vector3 textOffset = Vector3.up * 0.4f;
                Handles.Label(transform.position + textOffset, _reservation.name, style);
            }
        }
#endif // UNITY_EDITOR
    }
}
