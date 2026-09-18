using UnityEngine;

namespace BeaconPatch.SpawnSystem
{
    [CreateAssetMenu(fileName = "Election Criteria", menuName = "Scriptable Objects/Spawn System/Election Criteria")]
    public class ElectionCriteria : ScriptableObject
    {
        #region Parameters
        [Header("Player Proximity")]
        [SerializeField] [Tooltip("The maximum score the SpawnPoint can get when the player is further away.")]
        private float playerProximityScore = 1.0f;
        [SerializeField] [Tooltip("The maximum distance from the player to the SpawnPoint at which the close score " +
                                  "is applied.")]
        private float playerProximityMinRange = 0.0f;
        [SerializeField] [Tooltip("The minimum distance from the player to the SpawnPoint at which the far score " +
                                  "is applied.")]
        private float playerProximityMaxRange = 10.0f;
        [SerializeField] [Tooltip("A modulation for the score between the close and far score, where the close score " +
                                  "is at Δ0 and the far score at Δ1. This curve also allows to modulate to a score " +
                                  "above or under the maximum scores. The lenght of the curve should remain between " +
                                  "0.0f and 1.0f.")]
        private AnimationCurve playerProximityScoreModifier = new AnimationCurve();
        [SerializeField] [Tooltip("Give the maximum score when the player is closer instead of farther away from the " +
                                  "SpawnPoint.")]
        private bool playerProximityInvertScoreAlpha = false;
        
        
        [Header("player Sight")] 
        [SerializeField] [Tooltip("A score malus given to the SpawnPoint whenever the player character can see it " +
                                  "(does not care about on screen visibility, only of object-to-object line of sight).")]
        private float playerInSightScore = -1.0f;
        [SerializeField] [Tooltip("Determine against which layer the line of sight is tested. If set to 0, the  " +
                                  "check is skipped and no score is given when in sight.")]
        private LayerMask playerInSightCollision;
        #endregion Parameters


        #region Getters
        // Player Proximity
        public float GetPlayerProximityScore() { return playerProximityScore; }
        public float GetPlayerProximityMinRange() { return playerProximityMinRange; }
        public float GetPlayerProximityMaxRange() { return playerProximityMaxRange; }
        public AnimationCurve GetPlayerProximityScoreModifier() { return playerProximityScoreModifier; }
        public bool GetPlayerProximityInvertScoreAlpha() { return playerProximityInvertScoreAlpha; }

        // Player Sight
        public float GetPlayerInSightScore() { return playerInSightScore; }
        public LayerMask GetPlayerInSightCollision() { return playerInSightCollision; }
        #endregion
    }
}
