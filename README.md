# Spawn System
_A system made in Unity for my video game developer program's final project._

## In short
The SpawnSystem is composed of two main parts, the SpawnElector and the SpawnPoints.

The SpawnPoint is in charge of spawning whatever is given to it until it fails or is told to abandon. The SpawnPoint itself may have a few restriction, such as not accepting item outside of its dedicated item if any or with GameObject tags not matching some conditions. 

The SpawnElector is in charge of keeping track of SpawnPoints. For this, they must be registered to the SpawnElector, which can be done automatically or manually by level designers. Once registered, the SpawnElector proceed to update an election score for each active and registered SpawnPoints. When requested to spawn something, it check with each SpawnPoints and finds the one with the highest election score that is also available to spawn the thing.

Once a SpawnPoint as an object to spawn, it tries to spawn it until it succeed, is told to abandon it, or fails a number of time due to be obstructed. Generally, the spawning is instant and our AI in the project, not my fault, usually free up the SpawnPoint right as they spawn. This has lead to some issues where, has a fix, SpawnPoints can be required to wait a minimum amount of time before being electable to spawn things again.

## Few changes for this repository
For the purpose of this repository, I've...  
... replaced the project namespace with my own when I made the file.
... removed a few work in progress notes and left overs TODOs.  
The rest remains as is, albeit outside of its full context.

## What would I change in the future
The SpawnSystem was originally build to spawn both collectible items and enemies. Sadly, collectible items did not ended up using this system as much and so part of the design didn't received as much attention as originally planned. As such, while SpawnPoint could be defined as Generic (for any other system to use it), dedicated (to a single specific item), or both, the dedicated side of things could use some attention.

When come to the SpawnSystem itself and of its components, I would change the followings:
1. SpawnPoint would include a set dimension. This would help level designer place them in the scene without needing to guess if the item is going to spawn in an obstructed way.
2. Specialized SpawnAreas and SpawnRestriction volumes, similar to what is offered in Halo where enemies may spawn freely in a given area or SpawnPoints in a area be limited or not considered when rivals are within bound or in sight, helping to prevent "spawn kill" and the like.
3. The SpawnSystem is made with the assumption that there will always be only a single player character. I would love to make it multiplayer-ready.
4. As a cheap workaround with enemies, an extra plane with a NavMeshSurface is created underneath the world's main stage where NavMeshAgent can rest without causing NavMeshAgent conflict due to not being attached to a NavMesh while waiting to be spawned. This is a flaw on many level that has lost to do with how objects are "parked" while waiting for the spawn to be finalized. Removing all this would greatly help simplify things.
