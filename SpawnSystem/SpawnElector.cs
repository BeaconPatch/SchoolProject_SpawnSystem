using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeaconPatch.SpawnSystem
{
    public class SpawnElector : MonoBehaviour
    {
        #region Delegates
        public delegate void SpawnPointElectionScoreUpdated(SpawnPoint spawnPoint, float newScore);
        public event SpawnPointElectionScoreUpdated onElectionScoreUpdated;
        #endregion
        
        #region Parameters
        [SerializeField] [Tooltip("The ElectionCriteria data table to use to update the election score of" +
                                  "registered SpawnPoints.")]
        private ElectionCriteria electionCriteria;
        [SerializeField] [Tooltip("Determine the interval at which the registered SpawnPoints election score are" +
                                  "updated.")]
        private float electionScoreUpdateInterval = 1.0f;
        #endregion
        
        #region References
        public static SpawnElector instance { get; private set; }
        
        private List<SpawnPoint> _spawnPoints = new List<SpawnPoint>();
        private Coroutine _electionScoreUpdateCoroutine;
        #endregion
        
        
        #region MonoBehaviour
        private void Awake()
        {
            TryInitializeSingleton();
        }
        
        
        private void OnDisable()
        {
            StopAllCoroutines();
        }
        #endregion MonoBehaviour
        
        
        #region Initialization
        private bool TryInitializeSingleton()
        {
            if (instance != null)
            {
                Debug.LogError(this + "(" + gameObject + ")" + " tried to register itself as the SpawnElector " +
                               "singleton, but it was already defined. Destroying and aborting initialization.");
                gameObject.name = "! " + gameObject.name;
                Destroy(this);
                return false;
            }
            
            instance = this;
            return true;
        }
        #endregion Initialization
        
        #region Registration
        /// <summary>
        /// Request a SpawnPoint to be registered, allowing it to be updated by the SpawnElector and considered for use.
        /// </summary>
        /// <param name="spawnPoint">The SpawnPoint to attempt reregister.</param>
        /// <returns>Returns true if the SpawnPoint is successfully registered into the SpawnElector.</returns>
        public bool RequestSpawnPointRegistration(SpawnPoint spawnPoint)
        {
            if (_spawnPoints.Contains(spawnPoint))
            {
                Debug.LogWarning(spawnPoint.name + " requested to be registered into " + this.name + ", but it is already registered.");
                return false;
            }
            
            _spawnPoints.Add(spawnPoint);
            UpdateElectionScoreForSpawnPoint(spawnPoint);
            
            // Start the election interval if theirs at least one SpawnPoint registered.
            if (_spawnPoints.Count == 1)
            {
                _electionScoreUpdateCoroutine = StartCoroutine(ProcessElectionScoreUpdate());
            }
            return true;
        }
        
        
        /// <summary>
        /// Request a SpawnPoint to be unregistered, preventing further updates for it.
        /// </summary>
        /// <param name="spawnPoint">The SpawnPoint to attempt unregistering.</param>
        /// <returns>Returns true if the SpawnPoint is successfully unregistered from the SpawnElector.</returns>
        public bool RequestSpawnPointUnregistration(SpawnPoint spawnPoint)
        {
            if (_spawnPoints.Remove(spawnPoint))
            {
                if (_spawnPoints.Count == 1)
                {
                    StopCoroutine(_electionScoreUpdateCoroutine);
                }
                
                return true;
            }
            
            Debug.LogWarning(spawnPoint.name + " requested to be unregistered from " + this.name + ", but wasn't registered.");
            return false;
        }

        
        /// <summary>
        /// Ask if the provided SpawnPoint is currently registered.
        /// </summary>
        /// <param name="spawnPoint">The SpawnPoint to ask about its registration status.</param>
        /// <returns>Return true if the SpawnPoint is currently registered.</returns>
        public bool IsSpawnPointRegistered(SpawnPoint spawnPoint)
        {
            return _spawnPoints.Contains(spawnPoint);
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="spawnDispatcher"></param>
        public void RequestAllReservationAbandonFromDispatcher(ISpawnDispatcher spawnDispatcher)
        {
            foreach (SpawnPoint spawnPoint in _spawnPoints)
            {
                spawnPoint.RequestUnreservationFromSpawnDispatcher(spawnDispatcher);
            }
        }
        #endregion
        
        #region Election Score Update
        private IEnumerator ProcessElectionScoreUpdate()
        {
            yield return new WaitForSeconds(electionScoreUpdateInterval);
            
            UpdateElectionScoreForSpawnPoints();
        }
        
        
        private void UpdateElectionScoreForSpawnPoints(bool resetIntervalDelay = true)
        {
            if (_spawnPoints.Count == 0) return;
            if (electionCriteria == null) return;
            if (!GetPlayerPosition(out var playerPosition)) return;
            
            ElectionData data = new ElectionData(playerPosition);
            
            foreach (SpawnPoint spawnPoint in _spawnPoints)
            {
                float score = CalculateElectionScore(electionCriteria, data, spawnPoint);
                if (onElectionScoreUpdated != null)
                {
                    onElectionScoreUpdated(spawnPoint, score);
                }
            }
            
            if (resetIntervalDelay)
            {
                StopCoroutine(_electionScoreUpdateCoroutine);
                _electionScoreUpdateCoroutine = StartCoroutine(ProcessElectionScoreUpdate());
            }
        }


        private void UpdateElectionScoreForSpawnPoint(SpawnPoint spawnPoint)
        {
            if (electionCriteria == null) return;
            if (!GetPlayerPosition(out var playerPosition)) return;
            
            ElectionData data = new ElectionData(playerPosition);
            
            float score = CalculateElectionScore(electionCriteria, data, spawnPoint);
            if (onElectionScoreUpdated != null)
            {
                onElectionScoreUpdated(spawnPoint, score);
            }
        }
        
        
        private float CalculateElectionScore(ElectionCriteria criteria, ElectionData data, SpawnPoint spawnPoint)
        {
            Vector3 playerPosition = data.playerPosition;
            Vector3 spawnPointPosition = spawnPoint.transform.position;
            
            float score = CalculateDistanceScore(playerPosition, spawnPointPosition);

            score += CalculateInSightScore(playerPosition, spawnPointPosition, electionCriteria.GetPlayerInSightCollision());
            
            return score;
        }
        
        
        private float CalculateDistanceScore(Vector3 playerPosition, Vector3 spawnPointPosition)
        {
            float distance = Vector3.Distance(playerPosition, spawnPointPosition);
            float alpha = (distance - electionCriteria.GetPlayerProximityMinRange()) / Mathf.Max(electionCriteria.GetPlayerProximityMaxRange(), float.Epsilon);
            alpha = Mathf.Clamp(alpha, 0.0f, 1.0f);
            if (electionCriteria.GetPlayerProximityInvertScoreAlpha()) alpha = 1 - alpha;
            
            float score = electionCriteria.GetPlayerProximityScore();
            
            score *= electionCriteria.GetPlayerProximityScoreModifier().Evaluate(alpha);
            
            return score;
        }
        
        
        private float CalculateInSightScore(Vector3 playerPosition, Vector3 spawnPointPosition, LayerMask collisionMask)
        {
            // Save on physics testing if nothing can be detected.
            if (collisionMask == 0) return 0.0f;
            
            Vector3 startPosition = playerPosition;
            startPosition.y += 1.0f;

            Vector3 endPosition = spawnPointPosition;
            endPosition.y += 1.0f;
            
            return Physics.Linecast(startPosition, endPosition, collisionMask) ? 0.0f : electionCriteria.GetPlayerInSightScore();
        }
        #endregion Election Score Update

        #region Getters
        public List<SpawnPoint> GetAvailableSpawnPoints()
        {
            List<SpawnPoint> availableSpawnPoints = new List<SpawnPoint>();

            foreach (SpawnPoint point in _spawnPoints)
            {
                if (point.CanBeReserved())
                    availableSpawnPoints.Add(point);
            }
            
            return availableSpawnPoints;
        }
        
        
        public bool GetPlayerPosition(out Vector3 playerPosition)
        {
            playerPosition = Vector3.zero;
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return false;

            playerPosition = player.transform.position;
            return true;
        }


        public LayerMask GetVisibilityLayerMask()
        {
            return electionCriteria.GetPlayerInSightCollision();
        }
        #endregion Getters
    }
}
